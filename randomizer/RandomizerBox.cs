using System;
using System.Collections.Generic;
using System.Globalization;
using Game;
using UnityEngine;

// A box in the world, one .bfr line, read wherever seed lines are:
//   BX|<type>|x1,y1,x2,y2|<color>|<payload>
// split on | at most four times, so a payload keeps its own pipes. Types: goal (ends a
// practice attempt), kill, solid (ground and walls), item (gives once), ritem (gives on
// every entry), none (a deleted box, holding its place). An empty color is the type's;
// none or 0 is invisible.
public class RandomizerBox {
    public enum Kind {
        Goal,
        Kill,
        Solid,
        Item,
        RepeatItem,
        None
    }

    public Kind Type;

    public Rect Area;

    // the color field as written, so a line survives a round trip unchanged
    public string Color = "";

    // null is invisible
    public Color? Paint;

    public RandomizerAction Give;

    // the consumed bit of a one-shot item box; -1 for every other kind
    public int Bit = -1;

    // entry fires once until Ori leaves
    public bool Inside;

    public const string Prefix = "BX|";

    private static readonly string[] Names = { "goal", "kill", "solid", "item", "ritem", "none" };

    private static readonly Color[] Defaults = {
        new Color(0.2f, 0.9f, 0.35f, 0.25f),
        new Color(0.9f, 0.2f, 0.2f, 0.25f),
        new Color(0.55f, 0.58f, 0.62f, 0.55f),
        new Color(0.25f, 0.75f, 1f, 0.25f),
        new Color(0.5f, 0.85f, 1f, 0.25f),
        new Color(0f, 0f, 0f, 0f)
    };

    public static bool IsLine(string line) {
        return line != null && line.StartsWith(Prefix);
    }

    public string Name {
        get { return Names[(int)Type]; }
    }

    // A box that has had its turn: an item box taken, or anything switched off with
    // BM. Not drawn, not fired, and no collider if it was solid.
    public bool Off {
        get { return Bit >= 0 && RandomizerBoxes.IsOff(Bit); }
    }

    public static RandomizerBox Parse(string line) {
        var fields = line.Trim().Split(new[] { '|' }, 5);
        if (fields.Length < 3 || fields[0] != "BX") {
            throw new FormatException("a box line is BX|type|x1,y1,x2,y2|color|payload");
        }

        var box = new RandomizerBox();
        var kind = Array.IndexOf(Names, fields[1].Trim().ToLowerInvariant());
        if (kind < 0) {
            throw new FormatException("'" + fields[1] + "' is not a box type (goal, kill, solid, item, ritem)");
        }

        box.Type = (Kind)kind;
        var corners = fields[2].Split(',');
        if (corners.Length != 4) {
            throw new FormatException("a box needs four corners, x1,y1,x2,y2");
        }

        var c = new float[4];
        for (var i = 0; i < 4; i++) {
            if (!float.TryParse(corners[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out c[i])) {
                throw new FormatException("'" + corners[i] + "' is not a number");
            }
        }

        box.Area = Between(c[0], c[1], c[2], c[3]);
        box.SetColor(fields.Length > 3 ? fields[3].Trim() : "");
        var payload = fields.Length > 4 ? fields[4].Trim() : "";
        if (box.Type == Kind.Item || box.Type == Kind.RepeatItem) {
            var bar = payload.IndexOf('|');
            if (bar < 1) {
                throw new FormatException("an item box gives a pickup, like SK|5 or SH|text");
            }

            box.Give = new RandomizerAction(payload.Substring(0, bar), payload.Substring(bar + 1));
        } else if (box.Type == Kind.Kill) {
            box.Give = new RandomizerAction("RB", "3");
        }

        return box;
    }

    public string ToLine() {
        var line = Prefix + Name + "|" + Corners();
        var payload = Type == Kind.Item || Type == Kind.RepeatItem ? Payload : "";
        if (Color != "" || payload != "") {
            line += "|" + Color;
        }

        if (payload != "") {
            line += "|" + payload;
        }

        return line;
    }

    public string Payload {
        get { return Give == null ? "" : Give.Action + "|" + Give.Value; }
    }

    public string Corners() {
        return Num(Area.xMin) + "," + Num(Area.yMin) + "," + Num(Area.xMax) + "," + Num(Area.yMax);
    }

    private static string Num(float value) {
        return Math.Round(value, 1).ToString(CultureInfo.InvariantCulture);
    }

    public static Rect Between(float x1, float y1, float x2, float y2) {
        return new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    }

    // rrggbb or rrggbbaa; a bad value falls back to the type's color, out loud
    public void SetColor(string text) {
        Color = text ?? "";
        if (Type == Kind.None) {
            Paint = null;
            return;
        }

        if (Color == "") {
            Paint = Defaults[(int)Type];
            return;
        }

        var lower = Color.ToLowerInvariant();
        if (lower == "none" || lower == "0") {
            Paint = null;
            return;
        }

        var hex = Color.TrimStart('#');
        try {
            if (hex.Length != 6 && hex.Length != 8) {
                throw new FormatException();
            }

            var r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            var g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            var b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            var a = hex.Length == 8 ? Convert.ToInt32(hex.Substring(6, 2), 16) / 255f : Defaults[(int)Type].a;
            Paint = new Color(r, g, b, a);
        } catch (Exception) {
            Randomizer.LogError("box color '" + Color + "' is not rrggbb, rrggbbaa or none");
            Color = "";
            Paint = Defaults[(int)Type];
        }
    }

    // --- the page's shape of a box ---

    public JsonValue ToJson() {
        var json = JsonValue.NewObject();
        json.Set("type", JsonValue.Of(Name));
        var corners = JsonValue.NewArray();
        corners.Add(JsonValue.Of(Math.Round(Area.xMin, 1)));
        corners.Add(JsonValue.Of(Math.Round(Area.yMin, 1)));
        corners.Add(JsonValue.Of(Math.Round(Area.xMax, 1)));
        corners.Add(JsonValue.Of(Math.Round(Area.yMax, 1)));
        json.Set("box", corners);
        if (Color != "") {
            json.Set("color", JsonValue.Of(Color));
        }

        if (Give != null && Type != Kind.Kill) {
            json.Set("give", JsonValue.Of(Payload));
        }

        return json;
    }

    public static RandomizerBox FromJson(JsonValue json) {
        var type = json["type"].IsString ? json["type"].Str : "kill";
        var corners = json["box"];
        if (!corners.IsArray || corners.Count != 4) {
            throw new FormatException("a box needs four corners");
        }

        var line = Prefix + type + "|" + string.Join(",", new[] {
            Num((float)corners[0].Num), Num((float)corners[1].Num), Num((float)corners[2].Num), Num((float)corners[3].Num)
        });
        var color = json["color"].IsString ? json["color"].Str.TrimStart('#') : "";
        var give = json["give"].IsString ? json["give"].Str : "";
        if (color != "" || give != "") {
            line += "|" + color;
        }

        if (give != "") {
            line += "|" + give;
        }

        return Parse(line);
    }
}

// The boxes in force: a seed's, or a practice segment's while one runs. Entry fires
// the box; a one-shot item box remembers being taken in the inventory, RB 1700-1999
// as bitfields, so a checkpoint restore takes the memory back with the item.
public static class RandomizerBoxes {
    public static readonly List<RandomizerBox> Seed = new List<RandomizerBox>();

    public static List<RandomizerBox> Active = Seed;

    // bumped on every change of set, for the colliders to rebuild
    public static int Version;

    public const int FirstBitId = 1700;

    public const int LastBitId = 1999;

    public const int Capacity = (LastBitId - FirstBitId + 1) * 32;

    public static void Use(List<RandomizerBox> boxes) {
        Active = boxes ?? Seed;
        var bit = 0;
        foreach (var box in Active) {
            box.Bit = bit < Capacity ? bit : -1;
            bit++;
            box.Inside = false;
        }

        // past Capacity a box has nowhere to remember being off, so it stays on
        if (bit > Capacity) {
            Randomizer.LogError("seed has " + bit + " boxes; only the first " + Capacity
                + " can be taken once or switched off, the rest are always on");
        }

        Version++;
    }

    // the seed's own boxes, unless a practice segment has the floor
    public static void SeedLoaded() {
        if (!PracticeController.Active) {
            Use(null);
        }
    }

    public static bool IsOff(int bit) {
        var id = FirstBitId + bit / 32;
        if (id > LastBitId || Characters.Sein == null) {
            return false;
        }

        return (Characters.Sein.Inventory.GetRandomizerItem(id) & (1 << (bit % 32))) != 0;
    }

    // Version so the solid colliders notice; nothing else watches a single box.
    public static void SetOff(int bit, bool off) {
        var id = FirstBitId + bit / 32;
        if (bit < 0 || id > LastBitId || Characters.Sein == null) {
            return;
        }

        var mask = 1 << (bit % 32);
        var value = Characters.Sein.Inventory.GetRandomizerItem(id);
        var next = off ? value | mask : value & ~mask;
        if (next != value) {
            Characters.Sein.Inventory.SetRandomizerItem(id, next);
            Version++;
        }
    }

    // BM|n switches a box off or on by its place in the seed's box lines; =1 and =0
    // say which, ={slot} is on when that slot holds anything but zero, and =(a OP b)
    // is on when the comparison holds (RandomizerInventory.Value)
    public static void Modify(string value) {
        value = value ?? "";
        var eq = value.IndexOf('=');
        var name = (eq < 0 ? value : value.Substring(0, eq)).Trim();
        int which;
        if (!int.TryParse(name, out which) || which < 0 || which >= Active.Count) {
            Randomizer.LogError("BM|" + value + ": this seed has no box " + name);
            return;
        }

        var bit = Active[which].Bit;
        if (eq < 0) {
            SetOff(bit, !IsOff(bit));
            return;
        }

        int on;
        if (!RandomizerInventory.Value(value.Substring(eq + 1), out on)) {
            Randomizer.LogError("BM|" + value + ": after = comes 1, 0, {slot} or (a OP b)");
            return;
        }

        SetOff(bit, on == 0);
    }

    public static void ClearOff() {
        if (Characters.Sein == null) {
            return;
        }

        for (var id = FirstBitId; id <= LastBitId; id++) {
            if (Characters.Sein.Inventory.GetRandomizerItem(id) != 0) {
                Characters.Sein.Inventory.SetRandomizerItem(id, 0);
                Version++;
            }
        }
    }

    // Boxes fire on entry and re-arm once Ori leaves, which is what keeps a kill box
    // from killing the respawn it just caused. Boxes are not locations: nothing is
    // reported, counted or tracked, only given.
    public static void Check(Vector2 at, List<RandomizerBox> boxes) {
        foreach (var box in boxes) {
            if (box.Type == RandomizerBox.Kind.Goal || box.Type == RandomizerBox.Kind.Solid
                    || box.Type == RandomizerBox.Kind.None) {
                continue;
            }

            var inside = box.Area.Contains(at);
            if (inside && !box.Inside && !box.Off) {
                if (box.Type == RandomizerBox.Kind.Item) {
                    SetOff(box.Bit, true);
                }

                if (box.Give != null) {
                    RandomizerSwitch.GivePickup(box.Give, 0, false);
                }
            }

            box.Inside = inside;
        }
    }

    // every frame; practice runs its own check with its own gating
    public static void Tick() {
        if (Characters.Sein == null || GameController.Instance == null) {
            return;
        }

        if (Active.Count > 0) {
            RandomizerBoxView.Attach();
        }

        RandomizerBoxSolids.Tick();
        if (!PracticeController.Active && !GameController.Instance.GameInTitleScreen
                && !Game.UI.MainMenuVisible && !Characters.Sein.IsSuspended) {
            Check(Characters.Sein.Position, Active);
        }
    }
}

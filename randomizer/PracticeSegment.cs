using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

// The parts of segment.json that act during a run: the boxes you can walk
// into, and the condition that ends the attempt. Parsed once at Begin, then
// checked against Ori's position every frame.
public class PracticeSegment {
    public class Box {
        public Rect Area;

        public RandomizerAction Give;

        public bool Repeat;

        public bool Goal;

        public Color? Paint;

        // repeatable boxes re-arm on exit; one-shot boxes never fire twice
        public bool Inside;

        public bool Spent;
    }

    public List<Box> Boxes = new List<Box>();

    public Rect? GoalArea;

    public List<string> EndItems = new List<string>();

    public List<int> EndLocations = new List<int>();

    public int EndCount = -1;

    public bool HasEnd {
        get { return GoalArea.HasValue || EndItems.Count > 0 || EndLocations.Count > 0 || EndCount >= 0; }
    }

    // What a variant adds to its segment: its own items to start with, its own
    // boxes on top of the shared ones, and an end condition that replaces the
    // shared one outright rather than adding clauses to it.
    public List<RandomizerAction> StartingItems = new List<RandomizerAction>();

    // Variants share the segment's ending: they differ by what you start with
    // and what stands in the way, not by what counts as done.
    public static PracticeSegment Parse(JsonValue json, JsonValue variant) {
        var seg = Parse(json);
        if (!variant.IsObject) {
            return seg;
        }

        if (variant["end"].IsObject) {
            Randomizer.LogError("practice: variants share the segment's end condition; ignoring this one's");
        }

        seg.Boxes.AddRange(Parse(variant).Boxes);
        var items = variant["inventory"];
        for (var i = 0; i < items.Count; i++) {
            var action = Action(items[i]);
            if (action != null) {
                seg.StartingItems.Add(action);
            }
        }

        return seg;
    }

    private static RandomizerAction Action(JsonValue value) {
        if (!value.IsString) {
            return null;
        }

        var bar = value.Str.IndexOf('|');
        if (bar < 1) {
            Randomizer.LogError("practice: '" + value.Str + "' is not a pickup");
            return null;
        }

        return new RandomizerAction(value.Str.Substring(0, bar), value.Str.Substring(bar + 1));
    }

    // the pause menu stays the game's own, and Exit keeps the session, when this is set
    public bool QuitToMenu;

    public static PracticeSegment Parse(JsonValue json) {
        var seg = new PracticeSegment();
        seg.QuitToMenu = json["qtm_enabled"].IsBool && json["qtm_enabled"].Flag;
        var end = json["end"];
        if (end.IsObject) {
            if (end["box"].IsArray && end["box"].Count == 4) {
                seg.GoalArea = ToRect(end["box"]);
                var goal = new Box();
                goal.Area = seg.GoalArea.Value;
                goal.Goal = true;
                goal.Repeat = true;
                goal.Paint = new Color(0.2f, 0.9f, 0.35f, 0.25f);
                seg.Boxes.Add(goal);
            }

            for (var i = 0; i < end["items"].Count; i++) {
                seg.EndItems.Add(end["items"][i].Str);
            }

            // Checked once here rather than every frame: an unknown coord makes
            // HaveCoord shout, and a typo would bury the screen in it.
            for (var i = 0; i < end["locations"].Count; i++) {
                var key = (int)end["locations"][i].Num;
                if (RandomizerLocationManager.LocationsByKey.ContainsKey(key)) {
                    seg.EndLocations.Add(key);
                } else {
                    Randomizer.LogError("practice: segment wants location " + key + ", which is not a place");
                }
            }

            if (end["count"].IsNumber) {
                seg.EndCount = (int)end["count"].Num;
            }
        }

        var boxes = json["boxes"];
        for (var i = 0; i < boxes.Count; i++) {
            var b = boxes[i];
            if (!b["box"].IsArray || b["box"].Count != 4) {
                continue;
            }

            var box = new Box();
            box.Area = ToRect(b["box"]);
            box.Repeat = b["repeat"].IsBool && b["repeat"].Flag;
            var type = b["type"].IsString ? b["type"].Str : "raw";
            if (type == "death") {
                box.Give = new RandomizerAction("RB", "3");
                box.Repeat = true;
                box.Paint = new Color(0.9f, 0.2f, 0.2f, 0.25f);
            } else if (type == "hint") {
                box.Give = new RandomizerAction("SH", b["text"].IsString ? b["text"].Str : "");
            } else {
                if (!b["give"].IsString) {
                    continue;
                }

                var bar = b["give"].Str.IndexOf('|');
                if (bar < 1) {
                    continue;
                }

                box.Give = new RandomizerAction(b["give"].Str.Substring(0, bar),
                    b["give"].Str.Substring(bar + 1));
                box.Paint = ToColor(b["color"]);
            }

            seg.Boxes.Add(box);
        }

        return seg;
    }

    private static Rect ToRect(JsonValue box) {
        var x1 = (float)box[0].Num;
        var y1 = (float)box[1].Num;
        var x2 = (float)box[2].Num;
        var y2 = (float)box[3].Num;
        return new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    }

    // #rrggbb or #rrggbbaa; anything else means the box is not drawn
    private static Color? ToColor(JsonValue value) {
        if (!value.IsString) {
            return null;
        }

        var hex = value.Str.TrimStart('#');
        if (hex.Length != 6 && hex.Length != 8) {
            return null;
        }

        try {
            var r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            var g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            var b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            var a = hex.Length == 8 ? Convert.ToInt32(hex.Substring(6, 2), 16) / 255f : 0.25f;
            return new Color(r, g, b, a);
        } catch (Exception) {
            return null;
        }
    }

    // Boxes fire on entry. A repeatable one re-arms once Ori leaves it, which
    // is what keeps a death box from killing the respawn it just caused.
    public void Check(Vector2 at) {
        foreach (var box in Boxes) {
            var inside = box.Area.Contains(at);
            if (inside && !box.Inside && !box.Spent) {
                if (!box.Repeat) {
                    box.Spent = true;
                }

                if (box.Give != null) {
                    RandomizerSwitch.GivePickup(box.Give, 0, false);
                }
            }

            box.Inside = inside;
        }
    }

    // Every clause present must hold at once; a goal box is only satisfied
    // while Ori is standing in it.
    public bool Met(Vector2 at) {
        if (!HasEnd) {
            return false;
        }

        if (GoalArea.HasValue && !GoalArea.Value.Contains(at)) {
            return false;
        }

        if (EndCount >= 0 && PracticeController.Get(PracticeController.Pickups) < EndCount) {
            return false;
        }

        foreach (var location in EndLocations) {
            if (!Randomizer.HaveCoord(location)) {
                return false;
            }
        }

        foreach (var item in EndItems) {
            if (!Holds(item)) {
                return false;
            }
        }

        return true;
    }

    // The skill ids a seed uses, as the ability enum the player actually holds
    private static readonly Dictionary<int, AbilityType> Abilities = new Dictionary<int, AbilityType> {
        { 0, AbilityType.Bash },
        { 2, AbilityType.ChargeFlame },
        { 3, AbilityType.WallJump },
        { 4, AbilityType.Stomp },
        { 5, AbilityType.DoubleJump },
        { 8, AbilityType.ChargeJump },
        { 12, AbilityType.Climb },
        { 14, AbilityType.Glide },
        { 50, AbilityType.Dash },
        { 51, AbilityType.Grenade }
    };

    // "SK|3" and "EV|0" shaped: the two families v1 lets a segment ask for
    private static bool Holds(string item) {
        var bar = item.IndexOf('|');
        if (bar < 1) {
            return false;
        }

        var kind = item.Substring(0, bar);
        int id;
        if (!int.TryParse(item.Substring(bar + 1), out id)) {
            return false;
        }

        if (kind == "SK") {
            return Characters.Sein != null && Abilities.ContainsKey(id)
                && Characters.Sein.PlayerAbilities.HasAbility(Abilities[id]);
        }

        if (kind != "EV") {
            return false;
        }

        switch (id) {
            case 0: return Sein.World.Keys.GinsoTree;
            case 1: return Sein.World.Events.WaterPurified;
            case 2: return Sein.World.Keys.ForlornRuins;
            case 3: return Sein.World.Events.WindRestored;
            case 4: return Sein.World.Keys.MountHoru;
            case 5: return Sein.World.Events.WarmthReturned;
        }

        return false;
    }
}

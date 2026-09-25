#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game;
using RandoExts;
using UnityEngine;
using Object = UnityEngine.Object;

// A box in the world, one .bfr line, read wherever seed lines are:
//   BX|<flags>|x1,y1,x2,y2|<color>|<pickup>
// split on | at most four times, so a pickup keeps its own pipes.
public class RandomizerBox {
    public void Update() {
        if (UnityObject != null) {
            UnityObject.UpdateActive();
        }
    }

    public void Init(int boxNumber) {
        BoxNumber = boxNumber;
        if (UnityObject != null) {
            Object.Destroy(UnityObject.gameObject);
        }

        UnityObject = RandomizerBoxPrefab.Create(this);
    }

    public void DeInit() {
        if (UnityObject != null) {
            Object.Destroy(UnityObject.gameObject);
            UnityObject = null;
        }

        BoxNumber = -1;
    }

    public static bool IsLine(string line) {
        return line.StartsWith(Prefix);
    }

    public bool IsOff => BoxNumber >= 0 && RandomizerBoxes.IsOff(BoxNumber);

    public void Collect() {
        if (Once && BoxNumber >= 0) {
            RandomizerBoxes.BoxOffStates.Set(BoxNumber);
        }

        if (Item != null) {
            RandomizerSwitch.GivePickup(Item, 0, false);
        }
    }

    public static RandomizerBox Parse(string line) {
        var fields = line.Split(['|'], 5);
        if (fields.Length < 3 || fields[0] != "BX") {
            throw new FormatException("a box line is BX|flags|x1,y1,x2,y2|color|payload");
        }

        var box = new RandomizerBox {
            Line = line,
        };

        box.ParseFlags(fields[1], line);

        var corners = fields[2].Split(',');
        if (corners.Length != 4) {
            throw new FormatException("a box needs four corners, x1,y1,x2,y2");
        }

        var c = new float[4];
        for (var i = 0; i < 4; i++) {
            if (!float.TryParse(corners[i], NumberStyles.Number, CultureInfo.InvariantCulture, out c[i])) {
                throw new FormatException("'" + corners[i] + "' is not a number");
            }
        }

        box.Rect = Between(c[0], c[1], c[2], c[3]);
        if (fields.Length > 3 && !string.IsNullOrEmpty(fields[3])) {
            box.SetColor(fields[3], line);
        }

        if (fields.Length > 4 && !string.IsNullOrEmpty(fields[4])) {
            var parts = fields[4].Split(['|'], 2);
            if (parts.Length == 1) {
                Randomizer.log($"Item for box contains only an action, missing the value (the part after the '|') in line {line}");
            } else {
                box.Item = new RandomizerAction(parts[0], parts[1]);
            }
        }

        return box;
    }

    public void ParseFlags(string flagsString, string line) {
        var flags = flagsString.Split(',').Select(f => f.Split(['='], 2));

        foreach (var flag in flags) {
            switch (flag[0].Trim().ToLowerInvariant()) {
                case "once":
                    Once = true;
                    break;
                case "on": {
                    if (flag.Length == 1) {
                        Randomizer.log("box flag \"on\" requires a value (Enter, Tick, Frame)");
                        break;
                    }

                    if (!flag[1].TryParseEnum(true, out BoxTrigger trigger)) {
                        Randomizer.log($"invalid value for box on= flag \"{flag[1]}\". Must be one of (Enter, Tick, Frame)");
                        break;
                    }

                    Trigger = trigger;
                    break;
                }
                case "damage": {
                    if (flag.Length == 1) {
                        Randomizer.log("box flag \"damage\" requires a value");
                        break;
                    }

                    var parts = flag[1].Split(['/'], 4);
                    if (!float.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var damageAmount)) {
                        Randomizer.log($"box flag \"damage\" has invalid value \"{parts[0]}\" (must be a number) in line {line}");
                        break;
                    }

                    DamageAmount = damageAmount;

                    if (parts.Length > 1) {
                        if (!parts[1].TryParseEnum(true, out DamageType damageType)) {
                            Randomizer.log($"box flag \"damage\" has invalid damage type value \"{parts[1]}\" in line {line}");
                            break;
                        }

                        DamageType = damageType;
                    }

                    if (parts.Length > 2) {
                        if (!parts[2].TryParseEnum(true, out BoxDamageTarget damageTarget)) {
                            Randomizer.log($"box flag \"damage\" has invalid damage target value \"{parts[2]}\" in line {line}");
                            break;
                        }

                        DamageTarget = damageTarget;
                    }

                    if (parts.Length > 3) {
                        if (parts[3].Equals("normal", StringComparison.InvariantCultureIgnoreCase)) {
                            ExtendedHitboxes = false;
                        }
                        else if (parts[3].Equals("extended", StringComparison.InvariantCultureIgnoreCase)) {
                            ExtendedHitboxes = true;
                        } else {
                            Randomizer.log($"box flag \"damage\" has invalid hitbox value \"{parts[3]}\" (must be normal,extended) in line {line}");
                            break;
                        }
                    }

                    break;
                }
                case "solid":
                    Solid = true;
                    Color = new Color(0.55f, 0.58f, 0.62f, 0.55f);
                    break;
                case "unsafe":
                    Unsafe = true;
                    break;
                case "renderdepth": {
                    if (flag.Length == 1) {
                        Randomizer.log("box flag \"renderDepth\" requires a value");
                        break;
                    }

                    if (!float.TryParse(flag[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var renderDepth)) {
                        Randomizer.log($"box flag \"renderDepth\" has invalid value \"{flag[1]}\" (must be a number) in line {line}");
                        break;
                    }

                    RenderDepth = renderDepth;
                    break;
                }
                case "parallaxdepth": {
                    if (flag.Length == 1) {
                        Randomizer.log("box flag \"parallaxDepth\" requires a value");
                        break;
                    }

                    if (!float.TryParse(flag[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var parallaxDepth)) {
                        Randomizer.log($"box flag \"parallaxDepth\" has invalid value \"{flag[1]}\" (must be a number) in line {line}");
                        break;
                    }

                    ParallaxDepth = parallaxDepth;
                    break;
                }
                case "goal":
                    Goal = true;
                    Color = new Color(0.2f, 0.9f, 0.35f, 0.25f);
                    break;
                case "kill":
                    Item = new RandomizerAction("RB", "3");
                    Color = new Color(0.9f, 0.2f, 0.2f, 0.25f);
                    break;
                case "item":
                    Once = true;
                    Color = new Color(0.25f, 0.75f, 1f, 0.25f);
                    break;
                case "ritem":
                    Color = new Color(0.5f, 0.85f, 1f, 0.25f);
                    break;
                case "none":
                    Color = new Color(0.5f, 0.5f, 0.5f, 0.25f);
                    break;
                default:
                    Randomizer.log($"Invalid box flag \"{flag[0]}\" in box line {line}");
                    break;
            }
        }
    }

    private string GetName() {
        if (Goal) {
            return "goal";
        }

        var payload = GetPayloadString();
        if (payload == "RB|3") {
            return "kill";
        }

        if (Solid) {
            return "solid";
        }

        if (Once) {
            return "item";
        }

        if (payload != "") {
            return "ritem";
        }

        return "none";
    }

    public string GetPayloadString() => Item == null ? "" : Item.Action + "|" + Item.Value;

    private static string Num(float value) {
        return Math.Round(value, 1).ToString(CultureInfo.InvariantCulture);
    }

    public static Rect Between(float x1, float y1, float x2, float y2) {
        return new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    }

    public void SetColor(string text, string line) {
        if (text == "none") {
            Invisible = true;
            return;
        }

        var hex = text.TrimStart('#');
        try {
            if (hex.Length != 6 && hex.Length != 8) {
                throw new FormatException();
            }

            var r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            var defaultAlpha = (byte)(Solid ? 140 : 64);
            var a = hex.Length == 8 ? byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber) : defaultAlpha;
            Color = new Color32(r, g, b, a);
        } catch (Exception) {
            Randomizer.LogError($"box color '{text}' is not rrggbb, rrggbbaa or none in line {line}");
        }
    }

    public JsonValue ToJson() {
        var json = JsonValue.NewObject();
        var type = GetName();
        json.Set("type", JsonValue.Of(type));
        var corners = JsonValue.NewArray();
        corners.Add(JsonValue.Of(Math.Round(Rect.xMin, 1)));
        corners.Add(JsonValue.Of(Math.Round(Rect.yMin, 1)));
        corners.Add(JsonValue.Of(Math.Round(Rect.xMax, 1)));
        corners.Add(JsonValue.Of(Math.Round(Rect.yMax, 1)));
        json.Set("box", corners);
        json.Set("color", JsonValue.Of(Invisible ? "none" : $"{Color.r:X2}{Color.g:X2}{Color.b:X2}{Color.a:X2}"));

        if (Item != null && type != "kill") {
            json.Set("give", JsonValue.Of(GetPayloadString()));
        }

        return json;
    }

    public static RandomizerBox FromJson(JsonValue json) {
        var type = json["type"].IsString ? json["type"].Str : "kill";
        var corners = json["box"];
        if (!corners.IsArray || corners.Count != 4) {
            throw new FormatException("a box needs four corners");
        }

        var line = Prefix + type + "|" + string.Join(
            ",",
            new[] {
                Num((float)corners[0].Num), Num((float)corners[1].Num), Num((float)corners[2].Num), Num((float)corners[3].Num)
            }
        );
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

    // Keep the original parsed line to be able to save it again.
    // Properties must only be modified during parsing for that reason.
    public string Line { get; private set; } = "";

    public Rect Rect { get; private set; }

    public Color32 Color { get; private set; } = new(128, 128, 128, 128);

    public bool Invisible { get; private set; }

    public RandomizerAction? Item { get; private set; }

    public bool Once { get; private set; }

    public BoxTrigger Trigger { get; private set; }

    public float DamageAmount { get; private set; }

    public BoxDamageTarget DamageTarget { get; private set; } = BoxDamageTarget.All;

    public DamageType DamageType { get; private set; } = DamageType.Spikes;

    public bool ExtendedHitboxes { get; private set; } = true;

    public bool Solid { get; private set; }

    public bool Unsafe { get; private set; }

    public float RenderDepth { get; private set; } = 0.5f;

    public float ParallaxDepth { get; private set; }

    public bool Goal { get; private set; }

    public RandomizerBoxPrefab? UnityObject;

    public int BoxNumber = -1;

    public const string Prefix = "BX|";

    public enum BoxTrigger {
        Enter,
        Tick,
        Frame,
    }

    public enum BoxDamageTarget {
        Player,
        All,
    }
}

// The boxes in force: a seed's, or a practice segment's while one runs. Entry fires
// the box; a one-shot item box remembers being taken in the inventory, RB 1700-1999
// as bitfields, so a checkpoint restore takes the memory back with the item.
public static class RandomizerBoxes {
    public static readonly List<RandomizerBox> Seed = new();

    public static RandomizerBox[] ActiveBoxes = [];

    public const int FirstBitId = 1700;

    public const int LastBitId = 1999;

    public const int Capacity = (LastBitId - FirstBitId + 1) * 32;

    public const int FirstActiveId = 2651;

    public const int LastActiveId = FirstActiveId + LastBitId - FirstBitId;

    public static bool Enabled;

    public static RandomizerBitfield BoxOffStates = new(FirstBitId, LastBitId - FirstBitId + 1);

    public static RandomizerBitfield ActiveStates = new(FirstActiveId, LastActiveId - FirstActiveId + 1);

    public static Bitfield NewActiveStates = new(Capacity);

    public static List<int> RequiresUpdate = [];

    public static List<int> RequiresTick = [];

    private static bool _updateLast;

    public static void MarkActive(int boxNumber) {
        NewActiveStates.Set(boxNumber);
        RequiresTick.Add(boxNumber);
    }

    public static void FixedUpdate() {
        if (!Characters.Sein || Characters.Sein.IsSuspended) {
            return;
        }

        // Unity update order each frame is FixedUpdate -> Collisions -> FixedUpdate -> ... -> Collisions -> Update
        // So if the last called function was Update, no (physics) tick could have happened yet.
        // Additionally, FixedUpdate must be called at the start of Update to process the last physics tick.
        if (RequiresTick.Count == 0 || _updateLast) {
            _updateLast = false;
            return;
        }

        _updateLast = false;

        var collected = new List<RandomizerBox>();

        RequiresTick.Sort();
        var toTick = RequiresTick.ToArray();
        RequiresTick.Clear();
        var lastN = -1;
        foreach (var n in toTick) {
            if (n == lastN) {
                continue;
            }

            lastN = n;

            var wasActive = ActiveStates.Get(n);
            var active = NewActiveStates.Get(n) && !BoxOffStates.Get(n);
            // Because OnCollisionExit is not guaranteed to be called
            // we instead mark each active box to be deactivated the next tick
            // unless the collision persists
            NewActiveStates.Clear(n);
            ActiveStates.Set(n, active);
            if (active) {
                RequiresTick.Add(n);

                var box = ActiveBoxes[n];
                switch (box.Trigger) {
                    case RandomizerBox.BoxTrigger.Enter:
                        if (!wasActive) {
                            collected.Add(box);
                        }

                        break;
                    case RandomizerBox.BoxTrigger.Tick:
                        collected.Add(box);
                        break;
                    case RandomizerBox.BoxTrigger.Frame:
                        RequiresUpdate.Add(n);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        foreach (var box in collected) {
            box.Collect();
        }
    }

    public static void Update() {
        var enabled = GameStateMachine.Instance?.CurrentState == GameStateMachine.State.Game;

        if (enabled != Enabled) {
            Enabled = enabled;

            foreach (var box in ActiveBoxes) {
                box.Update();
            }
        }

        if (!Characters.Sein || Characters.Sein.IsSuspended) {
            return;
        }

        // There was a physics tick, process that first (see FixedUpdate comment for details)
        if (!_updateLast) {
            FixedUpdate();
        }

        _updateLast = true;

        if (RequiresUpdate.Count == 0) {
            return;
        }

        RequiresUpdate.Sort();
        var toUpdate = RequiresUpdate.ToArray();
        RequiresUpdate.Clear();
        var lastN = -1;
        foreach (var n in toUpdate) {
            if (n == lastN) {
                continue;
            }

            lastN = n;

            var box = ActiveBoxes[n];

            if (box.Trigger == RandomizerBox.BoxTrigger.Frame && ActiveStates.Get(n)) {
                RequiresUpdate.Add(n);
                box.Collect();
            }
        }
    }

    public static void Use(List<RandomizerBox>? boxes) {
        foreach (var box in ActiveBoxes) {
            box.DeInit();
        }

        ActiveBoxes = (boxes ?? Seed).ToArray();

        for (var i = 0; i < ActiveBoxes.Length; ++i) {
            ActiveBoxes[i].Init(i < Capacity ? i : -1);
        }

        if (ActiveBoxes.Length > Capacity) {
            Randomizer.LogError(
                "seed has " + ActiveBoxes.Length + " boxes; only the first " + Capacity
                + " can be taken once or switched off, the rest are always on"
            );
        }
    }

    // the seed's own boxes, unless a practice segment has the floor
    public static void SeedLoaded() {
        if (!PracticeController.Active) {
            Use(null);
        }
    }

    public static bool IsOff(int boxNumber) {
        return BoxOffStates.Get(boxNumber);
    }

    public static void SetOff(int boxNumber, bool off) {
        BoxOffStates.Set(boxNumber, off);
    }

    // BM|n switches a box off or on by its place in the seed's box lines; =1 and =0
    // say which, ={slot} is on when that slot holds anything but zero, and =(a OP b)
    // is on when the comparison holds (RandomizerInventory.Value)
    public static void EvalBM(string value) {
        var eq = value.IndexOf('=');
        var name = (eq < 0 ? value : value.Substring(0, eq)).Trim();
        if (!int.TryParse(name, out var bit) || bit < 0 || bit >= ActiveBoxes.Length) {
            Randomizer.LogError("BM|" + value + ": this seed has no box " + name);
            return;
        }

        if (eq < 0) {
            SetOff(bit, !IsOff(bit));
            return;
        }

        if (!RandomizerInventory.ParseValue(value.Substring(eq + 1), out var on)) {
            Randomizer.LogError("BM|" + value + ": after = comes 1, 0, {slot} or (a OP b)");
            return;
        }

        SetOff(bit, on <= 0);
    }

    public static void ClearOff() {
        for (var id = FirstBitId; id <= LastBitId; id++) {
            Randomizer.Inventory.SetRandomizerItem(id, 0);
        }
    }

    public static void SaveLoaded() {
        foreach (var box in ActiveBoxes) {
            box.Update();
        }

        NewActiveStates.ReadFrom(ActiveStates);

        for (var i = 0; i < Capacity; ++i) {
            if (NewActiveStates.Get(i)) {
                RequiresTick.Add(i);
            }
        }
    }

    public static void MaskUpdated(int code) {
        var start = (code - FirstBitId) * 32;
        var end = Math.Min(start + 32, ActiveBoxes.Length);
        for (var i = start; i < end; ++i) {
            ActiveBoxes[i].Update();
        }
    }
}

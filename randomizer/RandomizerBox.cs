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
    public void UpdateActive() {
        if (UnityObject != null) {
            UnityObject.UpdateActive();
        }
    }

    public void UpdatePosition() {
        if (UnityObject != null) {
            UnityObject.Place();
        }
    }

    // where the box is now: its line's rect, moved by any BR
    public Rect CurrentRect {
        get {
            RandomizerBoxes.Offset(BoxNumber, out var x, out var y);
            return new Rect(Rect.x + x / 100f, Rect.y + y / 100f, Rect.width, Rect.height);
        }
    }

    public void Init(int boxNumber) {
        DeInit();

        if (Deleted) {
            return;
        }

        BoxNumber = boxNumber;
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
        if (Deleted) {
            return;
        }

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
        box.ParseShape(fields[2], line);

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

        if (box.ShapeType != BoxShapeType.Rectangle && box.Unsafe) {
            Randomizer.log($"Unsafe boxes must be rectangular; error is in line {line}");
        }

        if (box.ShapeType != BoxShapeType.Rectangle && box.Goal) {
            Randomizer.log($"Goal boxes must be rectangular; error is in line {line}");
        }

        if (box.ShapeType == BoxShapeType.Polygon && !box.Solid && (box.DamageAmount > 0f || box.Item != null || box.Once)) {
            Randomizer.log($"Boxes with \"loop\" shape must be solid or decorative; error is in line {line}");
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

                    var parts = flag[1].Split(['/'], 3);
                    if (parts[0].Equals("Enter", StringComparison.OrdinalIgnoreCase)) {
                        Trigger = BoxTrigger.Tick;
                        RecollectCooldown = int.MaxValue;
                        InitialCollectCooldown = 1;
                        break;
                    }

                    if (!parts[0].TryParseEnum(true, out BoxTrigger trigger)) {
                        Randomizer.log($"invalid value for box on= flag \"{parts[0]}\". Must be one of (Enter, Tick, Frame)");
                        break;
                    }

                    Trigger = trigger;

                    if (trigger == BoxTrigger.Tick) {
                        RecollectCooldown = 1;
                        InitialCollectCooldown = 1;

                        if (parts.Length > 1) {
                            if (parts[1].Equals("never", StringComparison.OrdinalIgnoreCase)) {
                                InitialCollectCooldown = int.MaxValue;
                            } else if (int.TryParse(parts[1], out var cooldown)) {
                                InitialCollectCooldown = cooldown;
                            } else {
                                Randomizer.log($"box flag \"on\" has invalid delay value \"{parts[1]}\" (must be an integer) in line {line}");
                                break;
                            }
                        }

                        if (parts.Length > 2) {
                            if (parts[2].Equals("never", StringComparison.OrdinalIgnoreCase)) {
                                RecollectCooldown = int.MaxValue;
                            } else if (int.TryParse(parts[2], out var cooldown)) {
                                RecollectCooldown = cooldown;
                            } else {
                                Randomizer.log($"box flag \"on\" has invalid repeat value \"{parts[2]}\" (must be an integer) in line {line}");
                                break;
                            }
                        }
                    }

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
                        } else if (parts[3].Equals("extended", StringComparison.InvariantCultureIgnoreCase)) {
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
                case "unlit":
                    Unlit = true;
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
                case "tombstone":
                    Deleted = true;
                    break;
                case "":
                    break;
                default:
                    Randomizer.log($"Invalid box flag \"{flag[0]}\" in box line {line}");
                    break;
            }
        }
    }

    private void ParseShape(string shape, string line) {
        var parts = shape.Split(',');

        if (parts[0].Equals("convex",  StringComparison.InvariantCultureIgnoreCase)) {
            ShapeType = BoxShapeType.ConvexHull;
            ParsePoints(parts, line, true, false);
            return;
        }

        if (parts[0].Equals("loop",  StringComparison.InvariantCultureIgnoreCase)) {
            ShapeType = BoxShapeType.Polygon;
            ParsePoints(parts, line, true, true);
            return;
        }

        if (parts[0].Equals("orientedLoop", StringComparison.InvariantCultureIgnoreCase)) {
            ShapeType = BoxShapeType.OrientedPolygon;
            ParsePoints(parts, line, true, true);
            return;
        }

        if (parts[0].Equals("previewLoop", StringComparison.InvariantCultureIgnoreCase)) {
            // ONLY for use in the practice editor. Do not use in seed files.
            ShapeType = BoxShapeType.PreviewPolygon;
            ParsePoints(parts, line, false, true);
            return;
        }

        if (parts.Length != 4) {
            throw new FormatException("a box needs four corners, x1,y1,x2,y2");
        }

        var c = new float[4];
        for (var i = 0; i < 4; i++) {
            if (!float.TryParse(parts[i], NumberStyles.Number, CultureInfo.InvariantCulture, out c[i])) {
                throw new FormatException("'" + parts[i] + "' is not a number");
            }
        }

        var a = new Vector2(c[0], c[1]);
        var b = new Vector2(c[2], c[3]);
        SimplifiedShape = [a, b];
        Rect = Between(a.x, a.y, b.x, b.y);
    }

    private void ParsePoints(string[] values, string line, bool checkLength, bool loops) {
        Points = [[]];
        SimplifiedShape = [values[0]];

        for (var i = 1; i < values.Length;) {
            if (loops && values[i].Equals("loop", StringComparison.InvariantCultureIgnoreCase)) {
                SimplifiedShape.Add("loop");
                Points.Add([]);
                i += 1;
                continue;
            }

            if (i == values.Length - 1) {
                Randomizer.log($"Missing y-coordinate for last point in loop in line {line}");
                break;
            }

            if (!float.TryParse(values[i], NumberStyles.Number, CultureInfo.InvariantCulture, out var x)) {
                throw new FormatException($"'{values[i]}' is not a number");
            }

            if (!float.TryParse(values[i + 1], NumberStyles.Number, CultureInfo.InvariantCulture, out var y)) {
                throw new FormatException($"'{values[i + 1]}' is not a number");
            }

            Points[^1].Add(new Vector2(x, y));
            SimplifiedShape.Add(new Vector2(x, y));
            i += 2;
        }

        if (checkLength && Points.Any(l => l.Count < 3)) {
            throw new ArgumentException($"A loop needs at least three points. This box has only {Points.Count} points.");
        }

        using var allPoints = Points.SelectMany(l => l).GetEnumerator();
        if (!allPoints.MoveNext()) {
            Rect = new Rect();
            return;
        }

        var bounds = new Bounds(allPoints.Current, Vector3.zero);
        while (allPoints.MoveNext()) {
            bounds.Encapsulate(allPoints.Current);
        }

        Rect = new Rect(bounds.min, bounds.size);
    }

    private static string Num(float value) {
        return Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
    }

    public static Rect Between(float x1, float y1, float x2, float y2) {
        return new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    }

    public void SetColor(string text, string line) {
        if (text == "none" || text == "0") {
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

    // the editors' {flags, box, color, give}, from the line as written; a deleted box is only a tombstone
    public JsonValue ToJson() {
        var fields = (Deleted ? Tombstone : Line).Split(['|'], 5);
        var flags = string.Join(",", fields[1].Split(',').Select(f => f.Trim()).Where(f => f != "").ToArray());
        var json = JsonValue.NewObject();
        json.Set("flags", JsonValue.Of(flags));
        var rect = Deleted ? new Rect(0f, 0f, 0f, 0f) : Rect;
        var corners = JsonValue.NewArray();
        corners.Add(JsonValue.Of(Math.Round(rect.xMin, 2)));
        corners.Add(JsonValue.Of(Math.Round(rect.yMin, 2)));
        corners.Add(JsonValue.Of(Math.Round(rect.xMax, 2)));
        corners.Add(JsonValue.Of(Math.Round(rect.yMax, 2)));
        json.Set("box", corners);
        if (ShapeType != BoxShapeType.Rectangle) {
            json.Set("shape", JsonValue.Of(fields[2]));
        }
        json.Set("color", JsonValue.Of(fields.Length > 3 ? fields[3] : ""));
        json.Set("give", JsonValue.Of(fields.Length > 4 ? fields[4] : ""));
        return json;
    }

    public static RandomizerBox FromJson(JsonValue json) {
        var corners = json["box"];
        if (!corners.IsArray || corners.Count != 4) {
            throw new FormatException("a box needs four corners");
        }

        if (json["shape"] is not { Str: {} shape }) {
            shape = string.Join(
                ",",
                new[] {
                    Num((float)corners[0].Num), Num((float)corners[1].Num), Num((float)corners[2].Num), Num((float)corners[3].Num)
                }
            );
        }

        var flags = json["flags"].IsString ? json["flags"].Str : LegacyFlags(json);
        if (Deleting(flags)) {
            return Parse(Tombstone);
        }

        var line = Prefix + flags + "|" + shape;
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

    // a page from before flat flags sent {type, extra}; no type at all meant kill
    private static string LegacyFlags(JsonValue json) {
        var type = json["type"].IsString ? json["type"].Str : "kill";
        var extra = json["extra"].IsString ? json["extra"].Str : "";
        return type == "" || extra == "" ? type + extra : type + "," + extra;
    }

    // how every editor writes a deleted box
    public const string Tombstone = "BX|tombstone|0,0,0,0";

    private static bool Deleting(string flags) {
        return flags.Split(',').Select(f => f.Trim().ToLowerInvariant()).Any(f => f == "none" || f == "tombstone");
    }

    // any deleted box as the editors write one now; any other line as it was
    public static string Modern(string line) {
        var fields = line.Split('|');
        return IsLine(line) && fields.Length > 1 && Deleting(fields[1]) ? Tombstone : line;
    }

    public bool Convex => ShapeType is BoxShapeType.Rectangle or BoxShapeType.ConvexHull;

    // Keep the original parsed line to be able to save it again.
    // Properties must only be modified during parsing for that reason.
    public string Line { get; private set; } = "";

    public Rect Rect { get; private set; }

    public BoxShapeType ShapeType { get; private set; } = BoxShapeType.Rectangle;

    public List<List<Vector2>>? Points { get; private set; }

    // objects are all either string or Vector2.
    // May not be simplified if the box was never (successfully) initialized.
    public List<object> SimplifiedShape = [];

    public Color32 Color { get; private set; } = new(128, 128, 128, 64);

    public bool Invisible { get; private set; }

    public bool Unlit { get; private set; }

    public RandomizerAction? Item { get; private set; }

    public bool Once { get; private set; }

    public BoxTrigger Trigger { get; private set; }

    public int InitialCollectCooldown { get; private set; } = 1;

    public int RecollectCooldown { get; private set; } = int.MaxValue;

    public float DamageAmount { get; private set; }

    public BoxDamageTarget DamageTarget { get; private set; } = BoxDamageTarget.All;

    public DamageType DamageType { get; private set; } = DamageType.Spikes;

    public bool ExtendedHitboxes { get; private set; } = true;

    public bool Solid { get; private set; }

    public bool Unsafe { get; private set; }

    public float RenderDepth { get; private set; } = 0.5f;

    public float ParallaxDepth { get; private set; }

    public bool Goal { get; private set; }

    // a tombstone: a deleted box, kept only so the boxes after it keep their numbers
    public bool Deleted { get; private set; }

    public RandomizerBoxPrefab? UnityObject;

    public int BoxNumber = -1;

    public const string Prefix = "BX|";

    public enum BoxTrigger {
        Tick,
        Frame,
    }

    public enum BoxDamageTarget {
        Player,
        All,
    }

    public enum BoxShapeType {
        Rectangle,
        ConvexHull,
        Polygon,
        OrientedPolygon,
        PreviewPolygon, // ONLY for use in the practice editor. Do not use in seed files.
    }
}

// The boxes in force: a seed's, or a practice segment's while one runs. Entry fires
// the box; a one-shot item box remembers being taken in the inventory, RB 1700-1999
// as bitfields, so a checkpoint restore takes the memory back with the item.
public static class RandomizerBoxes {
    public static readonly List<RandomizerBox> Seed = new();

    public static RandomizerBox[] LoadedBoxes = [];

    public const int FirstBitId = 1700;

    public const int LastBitId = 1999;

    public const int Capacity = (LastBitId - FirstBitId + 1) * 32;

    public static bool Enabled;

    public static RandomizerBitfield BoxOffStates = new(FirstBitId, LastBitId - FirstBitId + 1);

    public static Dictionary<int, ActiveBoxState> ActiveBoxes = [];

    public static List<int> ActiveThisTick = [];

    public static List<int> CollectEachUpdate = [];

    private static bool _updateLast;

    // each moved box's offset from where its line puts it, in hundredths; saved with the box data
    private static readonly Dictionary<int, (int, int)> Offsets = [];

    public static void Offset(int box, out int x, out int y) {
        (x, y) = Offsets.TryGetValue(box, out var at) ? at : (0, 0);
    }

    public static void SetOffset(int box, int x, int y) {
        if (x == 0 && y == 0) {
            Offsets.Remove(box);
        } else {
            Offsets[box] = (x, y);
        }

        Moved(box);
    }

    // version 2 adds the offsets; a version 1 save has every box unmoved
    public static void Serialize(Archive? ar) {
        if (ar != null && !ar.Reading) {
            ar.Serialize(2);
            ar.Serialize(ActiveBoxes.Count);
            foreach (var (boxNr, activeState) in ActiveBoxes) {
                ar.Serialize(boxNr);
                ar.Serialize(activeState.Cooldown);
            }

            ar.Serialize(Offsets.Count);
            foreach (var (boxNr, (x, y)) in Offsets) {
                ar.Serialize(boxNr);
                ar.Serialize(x);
                ar.Serialize(y);
            }

            return;
        }

        ActiveBoxes.Clear();
        ActiveThisTick.Clear();
        CollectEachUpdate.Clear();
        Offsets.Clear();
        if (ar != null) {
            Read(ar);
        }

        // a load swaps the box state in whole, so every box is placed and shown again
        foreach (var box in LoadedBoxes) {
            box.UpdateActive();
            box.UpdatePosition();
        }
    }

    private static void Read(Archive ar) {
        var serializedVersion = ar.Serialize(0);
        if (serializedVersion is not (1 or 2)) {
            Randomizer.LogError($"Error reading saved box states. SerializedVersion has invalid value '{serializedVersion}'");
            return;
        }

        var count = ar.Serialize(0);
        for (var i = 0; i < count; i++) {
            var boxNr = ar.Serialize(0);
            ActiveBoxes.Add(boxNr, new ActiveBoxState(ar.Serialize(0)));
            ActiveThisTick.Add(boxNr);

            if (boxNr < LoadedBoxes.Length && LoadedBoxes[boxNr].Trigger == RandomizerBox.BoxTrigger.Frame) {
                CollectEachUpdate.Add(boxNr);
            }
        }

        if (serializedVersion < 2) {
            return;
        }

        var moved = ar.Serialize(0);
        for (var i = 0; i < moved; i++) {
            var boxNr = ar.Serialize(0);
            var x = ar.Serialize(0);
            Offsets[boxNr] = (x, ar.Serialize(0));
        }
    }

    public static void MarkActive(int boxNumber) {
        ActiveThisTick.Add(boxNumber);
    }

    public static void FixedUpdate() {
        if (!Characters.Sein || Characters.Sein.IsSuspended) {
            ActiveThisTick.Clear();
            return;
        }

        // Unity update order each frame is FixedUpdate -> Collisions -> FixedUpdate -> ... -> Collisions -> Update
        // So if the last called function was Update, no (physics) tick could have happened yet.
        // Additionally, FixedUpdate must be called at the start of Update to process the last physics tick.
        if (_updateLast) {
            _updateLast = false;
            return;
        }

        _updateLast = false;
        RandomizerInventory.CountTick();

        var lastActiveBoxes = ActiveBoxes;
        ActiveBoxes = [];
        CollectEachUpdate.Clear();

        if (ActiveThisTick.Count == 0) {
            return;
        }

        var collected = new List<RandomizerBox>();

        ActiveThisTick.Sort();
        var lastN = -1;
        foreach (var n in ActiveThisTick) {
            if (n == lastN) {
                continue;
            }

            lastN = n;

            if (n < 0 || n >= LoadedBoxes.Length || BoxOffStates.Get(n)) {
                continue;
            }

            var box = LoadedBoxes[n];

            if (!lastActiveBoxes.TryGetValue(n, out var activeState)) {
                activeState = new ActiveBoxState(box.InitialCollectCooldown);
            }

            ActiveBoxes.Add(n, activeState);

            switch (box.Trigger) {
                case RandomizerBox.BoxTrigger.Tick:
                    if (--activeState.Cooldown <= 0) {
                        activeState.Cooldown = box.RecollectCooldown;
                        collected.Add(box);
                    }

                    break;
                case RandomizerBox.BoxTrigger.Frame:
                    CollectEachUpdate.Add(n);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        ActiveThisTick.Clear();

        foreach (var box in collected) {
            box.Collect();
        }
    }

    public static void Update() {
        var enabled = GameStateMachine.Instance?.CurrentState == GameStateMachine.State.Game;

        if (enabled != Enabled) {
            Enabled = enabled;

            foreach (var box in LoadedBoxes) {
                box.UpdateActive();
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

        foreach (var n in CollectEachUpdate) {
            if (n >= LoadedBoxes.Length) {
                continue;
            }

            var box = LoadedBoxes[n];

            if (box.Trigger == RandomizerBox.BoxTrigger.Frame) {
                box.Collect();
            }
        }
    }

    public static void Use(List<RandomizerBox>? boxes) {
        foreach (var box in LoadedBoxes) {
            box.DeInit();
        }

        LoadedBoxes = (boxes ?? Seed).ToArray();

        for (var i = 0; i < LoadedBoxes.Length; ++i) {
            LoadedBoxes[i].Init(i < Capacity ? i : -1);
        }

        if (LoadedBoxes.Length > Capacity) {
            Randomizer.LogError(
                "seed has " + LoadedBoxes.Length + " boxes; only the first " + Capacity
                + " can be taken once or switched off, the rest are always on"
            );
        }
    }

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

    // BM|n toggles the seed's nth box line; BM|n=v turns it on if v > 0, else off (v as RI Format.txt says)
    public static void EvalBM(string value) {
        var eq = value.IndexOf('=');
        var name = (eq < 0 ? value : value.Substring(0, eq)).Trim();
        var key = "BM|" + value;
        if (!int.TryParse(name, out var bit) || bit < 0 || bit >= LoadedBoxes.Length) {
            RandomizerInventory.Complain(key, key + ": this seed has no box " + name);
            return;
        }

        if (LoadedBoxes[bit].Deleted) {
            return;
        }

        if (eq < 0) {
            SetOff(bit, !IsOff(bit));
            return;
        }

        if (!RandomizerInventory.Evaluate(value.Substring(eq + 1), out var on, out var problem)) {
            RandomizerInventory.Complain(key, key + ": " + problem);
            return;
        }

        SetOff(bit, on <= 0);
    }

    // true when any data was cleared
    public static bool ClearState() {
        var cleared = false;
        for (var id = FirstBitId; id <= LastBitId; id++) {
            if (Randomizer.Inventory.GetRandomizerItem(id) != 0) {
                Randomizer.Inventory.SetRandomizerItem(id, 0);
                cleared = true;
            }
        }

        cleared |= Offsets.Count > 0;
        Offsets.Clear();
        cleared |= ActiveBoxes.Count > 0;
        ActiveBoxes.Clear();
        ActiveThisTick.Clear();
        CollectEachUpdate.Clear();

        return cleared;
    }

    public static bool Exists(int box) {
        return box >= 0 && box < LoadedBoxes.Length;
    }

    public static bool Deleted(int box) {
        return LoadedBoxes[box].Deleted;
    }

    // a box past Capacity has no off bit, so it is always on
    public static bool IsOn(int box) {
        var it = LoadedBoxes[box];
        return !it.Deleted && (it.BoxNumber < 0 || !IsOff(it.BoxNumber));
    }

    // the top-left corner its line gives, in hundredths; false past what a move can reach
    public static bool Corner(int box, out long x, out long y) {
        var rect = LoadedBoxes[box].Rect;
        x = y = 0;
        if (!(Math.Abs(rect.xMin) < 1e7f && Math.Abs(rect.yMax) < 1e7f)) {
            return false;
        }

        x = (long)Math.Round(rect.xMin * 100.0);
        y = (long)Math.Round(rect.yMax * 100.0);
        return true;
    }

    public static void Moved(int box) {
        if (box < LoadedBoxes.Length) {
            LoadedBoxes[box].UpdatePosition();
        }
    }

    // the box editors draw every box where its line puts it; moves come back when they close
    public static bool ShowingLines { get; private set; }

    public static void ShowLines(bool on) {
        ShowingLines = on;
        foreach (var box in LoadedBoxes) {
            box.UpdatePosition();
        }
    }

    public static void MaskUpdated(int code) {
        var start = (code - FirstBitId) * 32;
        var end = Math.Min(start + 32, LoadedBoxes.Length);
        for (var i = start; i < end; ++i) {
            LoadedBoxes[i].UpdateActive();
        }
    }

    public class ActiveBoxState(int cooldown) {
        public int Cooldown = cooldown;
    }
}

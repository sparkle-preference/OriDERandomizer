// Randomizer.initialize()'s seed read without the engine, and the seed statics as plain values.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public static class SeedfileGlue {
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    static readonly MethodInfo ParseLine = typeof(Randomizer).GetMethod("ParseSeedLine", BindingFlags.NonPublic | BindingFlags.Static);

    static bool located;

    // the statics initialize() sets before reading a seed; engine-bound ones (UI, sync, boxes' prefabs) are left out
    public static void Reset() {
        if (!located) {
            // RandomizerLocationManager.Initialize without its areas thread
            foreach (var line in RandomizerLocationData.All.Split('\n')) {
                if (line.Trim().Length == 0) {
                    continue;
                }

                var loc = new RandomizerLocationManager.Location(line.Trim());
                RandomizerLocationManager.LocationsByName[loc.Name] = loc;
                RandomizerLocationManager.LocationsByKey[loc.Key] = loc;
            }

            located = true;
        }

        Randomizer.OHKO = false;
        Randomizer.ZeroXP = false;
        Randomizer.BonusActive = true;
        Randomizer.Sync = false;
        Randomizer.SyncId = "";
        Randomizer.ForceMaps = false;
        Randomizer.SyncMode = 4;
        Randomizer.Message = "Good luck on your rando!";
        Randomizer.ForceTrees = false;
        Randomizer.CluesMode = false;
        Randomizer.Shards = false;
        Randomizer.WorldTour = false;
        Randomizer.SeedMeta = "";
        Randomizer.SeedFormat = Randomizer.MIN_SEED_FORMAT;
        Randomizer.PlayerCount = 0;
        Randomizer.Entrance = false;
        Randomizer.DoorTable = new Hashtable();
        Randomizer.MessageQueue = new Queue<RandomizerUI.Message>();
        Randomizer.fragsEnabled = false;
        Randomizer.HotCold = false;
        Randomizer.HotColdTypes = new HashSet<string> { "EV", "RB17", "RB19", "RB21", "RB28", "SK", "TPForlorn", "TPHoru", "TPGinso", "TPValley", "TPSorrow" };
        Randomizer.HotColdItems = new Dictionary<int, RandomizerHotColdItem>();
        Randomizer.HotColdFrags = new Dictionary<int, RandomizerHotColdItem>();
        Randomizer.HotColdMaps = new List<int>();
        Randomizer.HotColdMapsWithFrags = new List<int>();
        Randomizer.HotColdSaveId = 2000;
        Randomizer.OpenMode = true;
        Randomizer.OpenWorld = false;
        Randomizer.RelicZoneLookup = new Dictionary<string, string>();
        Randomizer.RelicCount = 0;
        Randomizer.GrenadeZone = "MIA";
        Randomizer.StompZone = "MIA";
        Randomizer.GrenadeSlot = -1;
        Randomizer.StompSlot = -1;
        Randomizer.StompTriggers = false;
        Randomizer.AltRDisabled = false;
        Randomizer.GoalModeFinish = false;
        Randomizer.SpawnWith = "";
        Randomizer.IgnoreEnemyExp = false;
        Randomizer.RelicCountOverride = false;
        Randomizer.AllowOrbWarps = false;
        Randomizer.InLogicWarps = false;
        Randomizer.TeleportersLockedByClues = false;
        Randomizer.WarpLogicLocations = new Hashtable();
        if (Randomizer.Keysanity == null) {
            typeof(Randomizer).GetProperty("Keysanity").SetValue(null, new RandomizerKeysanity(null), null);
        }

        Randomizer.Keysanity.Initialize();
        RandomizerMW.Reset();
        RandomizerDeathLink.Reset();
        Randomizer.EnhancedMode = false;
        Randomizer.EnhancedSeinInSeed = false;
        Randomizer.VanillaRespawns = false;
        Randomizer.ShareSaves = false;
        RandomizerEnemies.ResetOptions();
        RandomizerEnemies.Reset();
        RandomizerLocationManager.KeyTiers = null;
        // keeps InitializeLogic to its resets: the areas parse runs on its own thread in game
        RandomizerLocationManager.HaveDownloadedAreas = false;
    }

    // Reset, plus what the end of a previous load leaves cleared, for reading lines outside a whole load
    public static void Fresh() {
        Reset();
        RandomizerClues.Clues = null;
        RandomizerClues.ClueSlots = null;
        RandomizerClues.RevealOrder = null;
        RandomizerBoxes.Seed.Clear();
        RandomizerLocationManager.ClearPickups();
        RandomizerLocationManager.InitializeLogic();
    }

    // initialize()'s read of a seed file after Reset: {threw, bad, logged, first, bingo}
    public static Dictionary<string, object> Load(string path) {
        var report = new Dictionary<string, object> {
            { "threw", null }, { "bad", new List<int>() }, { "logged", new List<int>() }, { "first", "" }, { "bingo", false },
        };
        var bad = (List<int>)report["bad"];
        var logged = (List<int>)report["logged"];
        var lastLineNum = -1;
        try {
            var allLines = File.ReadAllLines(path).ToList();
            RandomizerLocationManager.ClearPickups();
            lastLineNum = 0;
            var flagLine = allLines[0].Split('|');
            var s = flagLine[1];
            var flags = flagLine[0].Split(',');
            Randomizer.SeedMeta = allLines[0];
            var bingo = Randomizer.ParseFlags(s, flags);
            report["bingo"] = bingo;
            if (bingo && allLines[allLines.Count - 1].StartsWith("Goals")) {
                allLines.RemoveAt(allLines.Count - 1);
            }

            if (Randomizer.CluesMode) {
                RandomizerClues.initialize();
            }

            RandomizerBoxes.Seed.Clear();
            foreach (var line in allLines.Skip(1)) {
                lastLineNum += 1;
                if (line.Trim().Length == 0) {
                    continue;
                }

                var err = Line(line);
                if (err != null && err.StartsWith("logged: ")) {
                    logged.Add(lastLineNum + 1);
                } else if (err != null) {
                    if (bad.Count == 0) {
                        report["first"] = "line " + (lastLineNum + 1) + ": " + err;
                    }

                    bad.Add(lastLineNum + 1);
                }
            }

            Randomizer.HotColdMaps.Sort();
            Randomizer.HotColdMapsWithFrags.Sort();
            if (Randomizer.CluesMode) {
                RandomizerClues.FinishClues();
            }

            RandomizerLocationManager.InitializeLogic();
        } catch (Exception e) {
            report["threw"] = "line " + (lastLineNum + 1) + ": " + e.GetType().FullName + ": " + e.Message;
        }

        return report;
    }

    // ParseSeedLine(line): null, or what it threw; "logged: ..." when it reached LogError, which only the engine can draw
    public static string Line(string line) {
        try {
            ParseLine.Invoke(null, new object[] { line });
            return null;
        } catch (TargetInvocationException e) {
            var inner = e.InnerException;
            var logged = inner is System.Security.SecurityException && (inner.StackTrace ?? "").Contains("Randomizer.LogError(");
            return (logged ? "logged: " : "") + inner.GetType().FullName + ": " + inner.Message;
        }
    }

    // the statics a flag line can set
    public static Dictionary<string, object> Flags() {
        return new Dictionary<string, object> {
            { "ZeroXP", Randomizer.ZeroXP }, { "IgnoreEnemyExp", Randomizer.IgnoreEnemyExp },
            { "Entrance", Randomizer.Entrance },
            { "ForceMaps", Randomizer.ForceMaps }, { "ForceTrees", Randomizer.ForceTrees }, { "OHKO", Randomizer.OHKO },
            { "OpenMode", Randomizer.OpenMode }, { "OpenWorld", Randomizer.OpenWorld }, { "WorldTour", Randomizer.WorldTour },
            { "RelicCountOverride", Randomizer.RelicCountOverride }, { "RelicCount", Randomizer.RelicCount },
            { "fragsEnabled", Randomizer.fragsEnabled }, { "fragKeyFinish", Randomizer.fragKeyFinish }, { "maxFrags", Randomizer.maxFrags },
            { "StompTriggers", Randomizer.StompTriggers }, { "GoalModeFinish", Randomizer.GoalModeFinish },
            { "InLogicWarps", Randomizer.InLogicWarps }, { "TeleportersLockedByClues", Randomizer.TeleportersLockedByClues },
            { "Keysanity", Randomizer.Keysanity.IsActive }, { "EnhancedMode", Randomizer.EnhancedMode },
            { "VanillaRespawns", Randomizer.VanillaRespawns }, { "EnemyRando", RandomizerEnemies.Active },
            { "CluesMode", Randomizer.CluesMode }, { "Shards", Randomizer.Shards }, { "SyncMode", Randomizer.SyncMode },
            { "DeathLink", RandomizerDeathLink.Enabled }, { "ShareSaves", Randomizer.ShareSaves },
            { "BonusActive", Randomizer.BonusActive }, { "AltRDisabled", Randomizer.AltRDisabled }, { "AllowOrbWarps", Randomizer.AllowOrbWarps },
            { "HotCold", Randomizer.HotCold }, { "HotColdTypes", Randomizer.HotColdTypes.OrderBy(t => t, StringComparer.Ordinal).ToList() },
            { "SeedMeta", Randomizer.SeedMeta },
        };
    }

    public static string Pickup(int key) {
        RandomizerLocationManager.Location loc;
        if (!RandomizerLocationManager.LocationsByKey.TryGetValue(key, out loc)) {
            return null;
        }

        return loc.Pickup == null ? "" : loc.Pickup.Action + "|" + loc.Pickup.ValAsStr() + (loc.Repeatable ? "|repeatable" : "");
    }

    public static List<int> Keys() {
        return RandomizerLocationManager.LocationsByKey.Keys.OrderBy(k => k).ToList();
    }

    // every placed pickup, by location key
    public static Dictionary<string, string> Pickups() {
        var out_ = new Dictionary<string, string>();
        foreach (var kv in RandomizerLocationManager.LocationsByKey) {
            if (kv.Value.Pickup != null) {
                out_[kv.Key.ToString()] = Pickup(kv.Key);
            }
        }

        return out_;
    }

    public static Dictionary<string, object> Manifest() {
        var out_ = new Dictionary<string, object>();
        foreach (var kv in RandomizerMW.Manifest) {
            var e = kv.Value;
            out_[kv.Key.ToString()] = new List<object> { e.Finder, e.Code, e.Id, e.Zone };
        }

        return out_;
    }

    public static Dictionary<string, List<string>> ApItems() {
        return RandomizerMW.ApItems.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.ToList());
    }

    public static int[] Rolls() {
        return (int[])typeof(RandomizerEnemies).GetField("s_table", Hidden).GetValue(null);
    }

    public static string EnemySeedKey() {
        return (string)typeof(RandomizerEnemies).GetMethod("SeedKey", Hidden).Invoke(null, null);
    }

    public static Dictionary<string, List<string>> KeysanityClues() {
        var map = (IDictionary)typeof(RandomizerKeysanity).GetField("keyClueMap", Hidden).GetValue(Randomizer.Keysanity);
        var out_ = new Dictionary<string, List<string>>();
        foreach (DictionaryEntry kv in map) {
            out_[kv.Key.ToString()] = ((IEnumerable<RandomizerKeysanityHintInfo>)kv.Value).Select(h => h.Coords + "@" + h.Area).ToList();
        }

        return out_;
    }

    static List<string> Pairs(IDictionary d) {
        var out_ = new List<string>();
        foreach (DictionaryEntry kv in d) {
            out_.Add(kv.Key + "=" + kv.Value);
        }

        out_.Sort(StringComparer.Ordinal);
        return out_;
    }

    // what one seed line can leave behind, outside the location table
    public static Dictionary<string, object> Side() {
        return new Dictionary<string, object> {
            { "clues", RandomizerClues.Clues == null ? null : new List<string>(RandomizerClues.Clues) },
            { "clueSlots", RandomizerClues.ClueSlots == null ? null : new List<int>(RandomizerClues.ClueSlots) },
            { "revealOrder", RandomizerClues.RevealOrder == null ? null : RandomizerClues.RevealOrder.ToList() },
            { "hotColdItems", Randomizer.HotColdItems.Keys.OrderBy(k => k).ToList() },
            { "hotColdFrags", Randomizer.HotColdFrags.Keys.OrderBy(k => k).ToList() },
            { "hotColdMaps", new List<int>(Randomizer.HotColdMaps) },
            { "hotColdMapsWithFrags", new List<int>(Randomizer.HotColdMapsWithFrags) },
            { "hotColdSaveId", Randomizer.HotColdSaveId },
            { "doors", Pairs(Randomizer.DoorTable) },
            { "warps", Pairs(Randomizer.WarpLogicLocations) },
            { "relics", Pairs(Randomizer.RelicZoneLookup) },
            { "relicCount", Randomizer.RelicCount },
            { "zones", new List<object> { Randomizer.GrenadeZone, Randomizer.StompZone, Randomizer.GrenadeSlot, Randomizer.StompSlot } },
            { "spawnWith", Randomizer.SpawnWith },
            { "seinInSeed", Randomizer.EnhancedSeinInSeed },
            { "keysanity", KeysanityClues() },
            { "manifest", Manifest() },
            { "apItems", RandomizerMW.ApItems.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + string.Join(",", kv.Value)).ToList() },
            { "apSelf", Pairs(RandomizerMW.ApSelfSlots) },
            { "boxes", RandomizerBoxes.Seed.Count },
        };
    }

    // the boxes a seed file numbers, as initialize() and SeedBoxes read it: their lines in box order
    public static List<string> LoadedBoxLines() {
        return RandomizerBoxes.Seed.Select(b => b.Line).ToList();
    }

    public static List<string> EditorBoxLines(string path) {
        return SeedBoxes.Load(path).Boxes.Select(b => b.Line).ToList();
    }

    // a box as plain values: corners, shape and its loops of [x, y] (null for a rectangle), RGBA (null when invisible), flags, payload
    public static Dictionary<string, object> Box(string line) {
        var box = RandomizerBox.Parse(line);
        var c = box.Color;
        return new Dictionary<string, object> {
            { "corners", new List<double> { box.Rect.xMin, box.Rect.yMin, box.Rect.xMax, box.Rect.yMax } },
            { "shape", box.ShapeType.ToString() },
            { "points", box.Points == null ? null : box.Points.Select(l => l.Select(p => new List<double> { p.x, p.y }).ToList()).ToList() },
            { "color", box.Invisible ? null : new List<int> { c.r, c.g, c.b, c.a } },
            { "once", box.Once }, { "solid", box.Solid }, { "goal", box.Goal }, { "deleted", box.Deleted },
            { "unsafe", box.Unsafe }, { "trigger", box.Trigger.ToString() },
            { "item", box.Item == null ? null : box.Item.Action + "|" + box.Item.ValAsStr() },
        };
    }

    // RandomizerBox.FromJson for the editors' {flags, box, color, give}: the line it keeps
    public static string BoxFromJson(string flags, double[] corners, string color, string give) {
        return ShapedBoxFromJson(flags, corners, null, color, give);
    }

    // the same for a box with a shape, which the page sends beside its corners
    public static string ShapedBoxFromJson(string flags, double[] corners, string shape, string color, string give) {
        var json = JsonValue.NewObject();
        json.Set("flags", JsonValue.Of(flags));
        var arr = JsonValue.NewArray();
        foreach (var x in corners) {
            arr.Add(JsonValue.Of(x));
        }

        json.Set("box", arr);
        if (shape != null) {
            json.Set("shape", JsonValue.Of(shape));
        }

        json.Set("color", JsonValue.Of(color));
        json.Set("give", JsonValue.Of(give));
        return RandomizerBox.FromJson(json).Line;
    }

    // QuickHull.Compute over the points (xs[i], ys[i]), as [x, y] in its order
    public static List<List<double>> Hull(double[] xs, double[] ys) {
        var points = xs.Select((x, i) => new UnityEngine.Vector2((float)x, (float)ys[i])).ToList();
        return QuickHull.Compute(points).Select(p => new List<double> { p.x, p.y }).ToList();
    }

    static readonly MethodInfo MapShape = typeof(PracticeEditor).GetMethod("MapShape", BindingFlags.NonPublic | BindingFlags.Static);

    // a shaped box as RandomizerBoxGeometry builds it, in world units, and the shape the in-game editor rewrites it as; null where the game draws the rectangle
    public static Dictionary<string, object> Geometry(string line) {
        var box = RandomizerBox.Parse(line);
        var offset = box.Rect.center;
        var shape = RandomizerBoxGeometry.Compute(box.ShapeType, RandomizerBoxGeometry.Loops(box), offset);
        if (shape == null) {
            return null;
        }

        Func<Clipper2Lib.Paths64, List<List<List<double>>>> world = paths => paths.Select(path => path.Select(p => new List<double> {
            p.X * (double)RandomizerBoxGeometry.ScaleDown + offset.x, p.Y * (double)RandomizerBoxGeometry.ScaleDown + offset.y,
        }).ToList()).ToList();
        Func<UnityEngine.Vector3, List<double>> placed = v => new List<double> { v.x + (double)offset.x, v.y + (double)offset.y, v.z };
        RandomizerBoxGeometry.VisualVertices(shape, 0f, UnityEngine.Color.white, UnityEngine.Color.black, out var vertices, out var triangles, out var colors);
        RandomizerBoxGeometry.ColliderVertices(shape.Outline, out var walls, out var wallTriangles);
        Func<UnityEngine.Vector2, UnityEngine.Vector2> same = p => p;
        var raw = RandomizerBoxGeometry.ToRawShape(box.ShapeType, shape.Outline, offset) ?? box.SimplifiedShape;
        var centroid = RandomizerBoxGeometry.Centroid(shape.Outline);
        return new Dictionary<string, object> {
            { "centroid", new List<double> { centroid.x + (double)offset.x, centroid.y + (double)offset.y } },
            { "outline", world(shape.Outline) },
            { "edge", world(shape.EdgeTriangles) },
            { "fill", world(shape.FillTriangles) },
            { "vertices", vertices.Select(placed).ToList() },
            { "triangles", triangles.ToList() },
            { "edgeColored", colors.Count(c => c == UnityEngine.Color.black) },
            { "walls", walls.Select(placed).ToList() },
            { "wallTriangles", wallTriangles },
            { "rewrite", (string)MapShape.Invoke(null, new object[] { raw, same }) },
        };
    }
}

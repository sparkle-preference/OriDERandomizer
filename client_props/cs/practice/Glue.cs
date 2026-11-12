// Turns the practice formats into plain values for the properties, and drives the file-based APIs.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

static class PracticeGlue {
    // --- json ---

    // a JsonValue as ascii json: numbers as {"n": bits}, objects as {"o": [[key, value]...]}, every string char \u-escaped
    public static string Canon(string text) {
        var sb = new StringBuilder();
        Canon(JsonValue.Parse(text), sb);
        return sb.ToString();
    }

    static void Canon(JsonValue v, StringBuilder sb) {
        if (v.IsNull) {
            sb.Append("null");
        } else if (v.IsBool) {
            sb.Append(v.Flag ? "true" : "false");
        } else if (v.IsNumber) {
            sb.Append("{\"n\":\"").Append(BitConverter.DoubleToInt64Bits(v.Num).ToString("x16", CultureInfo.InvariantCulture)).Append("\"}");
        } else if (v.IsString) {
            Escaped(v.Str, sb);
        } else if (v.IsArray) {
            sb.Append('[');
            for (var i = 0; i < v.Count; i++) {
                if (i > 0) {
                    sb.Append(',');
                }

                Canon(v[i], sb);
            }

            sb.Append(']');
        } else {
            sb.Append("{\"o\":[");
            var first = true;
            foreach (var key in v.Keys) {
                if (!first) {
                    sb.Append(',');
                }

                first = false;
                sb.Append('[');
                Escaped(key, sb);
                sb.Append(',');
                Canon(v[key], sb);
                sb.Append(']');
            }

            sb.Append("]}");
        }
    }

    static void Escaped(string s, StringBuilder sb) {
        sb.Append('"');
        foreach (var c in s) {
            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
        }

        sb.Append('"');
    }

    public static string Ser(string text, bool pretty) {
        return JsonValue.Parse(text).Serialize(pretty);
    }

    // a plain value arrives as [value]: the bridge unwraps that value one level, and leaves everything inside it wrapped
    public static string SerPlain(List<object> one, bool pretty) {
        return FromPlain(one[0], true).Serialize(pretty);
    }

    static JsonValue FromPlain(object o, bool top) {
        if (o == null) {
            return JsonValue.Null();
        }

        if (o is bool) {
            return JsonValue.Of((bool)o);
        }

        if (o is string) {
            return JsonValue.Of((string)o);
        }

        var d = o as Dictionary<string, object>;
        if (d != null && !top && d.ContainsKey("$d64")) {
            return JsonValue.Of(BitConverter.Int64BitsToDouble(long.Parse((string)d["$d64"], NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
        }

        if (d != null) {
            var map = top ? d : (Dictionary<string, object>)d["$map"];
            var obj = JsonValue.NewObject();
            foreach (var kv in map) {
                obj.Set(kv.Key, FromPlain(kv.Value, false));
            }

            return obj;
        }

        var list = o as IList;
        if (list != null) {
            var arr = JsonValue.NewArray();
            foreach (var x in list) {
                arr.Add(FromPlain(x, false));
            }

            return arr;
        }

        return JsonValue.Of(Convert.ToDouble(o, CultureInfo.InvariantCulture));
    }

    // --- containers ---

    public static List<object> ZipRead(string path) {
        var store = ZipStore.Read(path);
        return store.Names.Select(n => (object)new List<object> { n, store.Get(n) }).ToList();
    }

    public static void ZipWrite(string path, List<string> names, List<byte[]> datas) {
        var store = new ZipStore();
        for (var i = 0; i < names.Count; i++) {
            store.Set(names[i], datas[i]);
        }

        store.Write(path);
    }

    public static List<object> FolderRead(string path) {
        var store = FolderStore.Read(path);
        return store.Names.Select(n => (object)new List<object> { n, store.Get(n) }).ToList();
    }

    public static void FolderWrite(string path, List<string> names, List<byte[]> datas) {
        var store = new FolderStore();
        for (var i = 0; i < names.Count; i++) {
            store.Set(names[i], datas[i]);
        }

        store.Write(path);
    }

    // where an accepted name's file resolves, or null when Parts refuses it
    public static string Inside(string root, string name) {
        try {
            FolderStore.Parts(name);
        } catch (IOException) {
            return null;
        }

        return Path.GetFullPath(FolderStore.Where(root, name));
    }

    // --- defaults ---

    // how[name]: ok, garbage, missing (200, nothing written), 404, throw, half; unnamed ones are ok
    public static List<object> Sync(string folder, List<string> offered, Dictionary<string, string> how, byte[] good) {
        var calls = new List<string>();
        Func<string, string, int> download = (name, to) => {
            calls.Add(name);
            string kind;
            if (!how.TryGetValue(name, out kind)) {
                kind = "ok";
            }

            switch (kind) {
                case "garbage":
                    File.WriteAllBytes(to, Encoding.UTF8.GetBytes("<html>bad gateway</html>"));
                    return 200;
                case "missing":
                    return 200;
                case "404":
                    File.WriteAllBytes(to, Encoding.UTF8.GetBytes("not found"));
                    return 404;
                case "throw":
                    File.WriteAllBytes(to, good);
                    throw new IOException("connection reset");
                case "half":
                    File.WriteAllBytes(to, good.Take(good.Length / 2).ToArray());
                    return 200;
                default:
                    File.WriteAllBytes(to, good);
                    return 200;
            }
        };
        var added = PracticeDefaults.Sync(folder, offered, download);
        return new List<object> { added, calls };
    }

    // --- bfrp ---

    // the root's boxes, then each variant's, as [variant, [[line, deleted]...]]
    public static List<object> Boxes(string path) {
        var file = BfrpFile.Load(path);
        var ids = file.Variants;
        ids.Insert(0, "");
        return ids.Select(id => (object)new List<object> {
            id, file.Boxes(id).Select(b => (object)new List<object> { b.Line, b.Deleted }).ToList()
        }).ToList();
    }

    public static void Save(string path) {
        BfrpFile.Load(path).Save();
    }

    static BfrpFile Scratch() {
        return BfrpFile.Create("scratch.bfrp", JsonValue.Parse("{}"), new byte[1]);
    }

    static IEntryStore Store(BfrpFile file) {
        return (IEntryStore)typeof(BfrpFile).GetField("store", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(file);
    }

    public static List<string> PlacementRoundTrip(List<string> lines) {
        var file = Scratch();
        file.SetPlacementLines("", lines);
        return file.PlacementLines("");
    }

    // a box as the page sends it, stored and read back: [its line, the placement lines, the lines Boxes reads]
    public static List<object> PageBox(string json) {
        var box = RandomizerBox.FromJson(JsonValue.Parse(json));
        var file = Scratch();
        file.SetBoxes("", new List<RandomizerBox> { box });
        return new List<object> { box.Line, file.PlacementLines(""), file.Boxes("").Select(b => b.Line).ToList() };
    }

    public static List<object> Runs(byte[] csv) {
        var file = Scratch();
        Store(file).Set(BfrpFile.RunsEntry, csv);
        return file.RunsFor("").Select(r => (object)new List<object> { r.Date, r.Pickups, r.Ms }).ToList();
    }

    public static byte[] AppendRun(byte[] csv, string date, int pickups, long ms) {
        var file = Scratch();
        if (csv != null) {
            Store(file).Set(BfrpFile.RunsEntry, csv);
        }

        file.AppendRun(DateTime.ParseExact(date, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), pickups, ms);
        return Store(file).Get(BfrpFile.RunsEntry);
    }

    public static bool Parses(string line) {
        try {
            RandomizerBox.Parse(line);
            return true;
        } catch (Exception) {
            return false;
        }
    }

    // --- segment ---

    // a code as the attempt holds it ("SK|3"), or null when it is not a pickup
    public static string PickupText(string code) {
        var bar = code.IndexOf('|');
        if (bar < 1 || !PracticeSegment.IsPickup(code)) {
            return null;
        }

        return new RandomizerAction(code.Substring(0, bar), code.Substring(bar + 1)).ToString();
    }

    public static Dictionary<string, object> Segment(string json) {
        var seg = PracticeSegment.Parse(JsonValue.Parse(json));
        return new Dictionary<string, object> {
            { "items", seg.EndItems }, { "locations", seg.EndLocations }, { "count", seg.EndCount },
            { "qtm", seg.QuitToMenu }, { "has_end", seg.HasEnd }
        };
    }

    // an attempt of variant "v": its boxes as [line, deleted], its starting items, whether it has a goal
    public static Dictionary<string, object> Attempt(List<string> shared, string variantJson, List<string> own) {
        var file = Scratch();
        file.SetPlacementLines("", shared);
        file.SetVariantSegment("v", JsonValue.Parse(variantJson));
        file.SetPlacementLines("v", own);
        var seg = PracticeSegment.Parse(file, "v");
        return new Dictionary<string, object> {
            { "boxes", seg.Boxes.Select(b => (object)new List<object> { b.Line, b.Deleted }).ToList() },
            { "items", seg.StartingItems.Select(a => a.ToString()).ToList() }, { "goal", seg.GoalBox != null }
        };
    }

    public static List<object> ReadPlacements(string text, bool variant) {
        var skipped = new List<string>();
        var kept = PracticeSegment.ReadPlacements(text, variant, skipped);
        return new List<object> { kept, skipped };
    }

    // what an attempt's locations hold, by coord, from a segment.json and root placement lines
    public static Dictionary<int, string> Resolve(string json, List<string> lines) {
        var file = BfrpFile.Create("scratch.bfrp", JsonValue.Parse(json), new byte[1]);
        file.SetPlacementLines("", lines);
        return PracticeSegment.ResolvePlacements(file, "").ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
    }

    // fills LocationsByKey as the game does; [[key, type, special index]...]
    public static List<object> Locations() {
        var byKey = RandomizerLocationManager.LocationsByKey;
        if (byKey.Count == 0) {
            foreach (var line in RandomizerLocationData.All.Split('\n')) {
                if (line.Trim().Length > 0) {
                    var loc = new RandomizerLocationManager.Location(line.Trim());
                    byKey[loc.Key] = loc;
                }
            }
        }

        return byKey.Values.Select(l => (object)new List<object> { l.Key, l.Type.ToString(), l.SpecialIndex }).ToList();
    }

    // --- seed boxes ---

    public static List<string> SeedBoxLines(string path) {
        return SeedBoxes.Load(path).Boxes.Select(b => b.Line).ToList();
    }

    public static void SeedEdit(string path, List<string> boxLines) {
        var seed = SeedBoxes.Load(path);
        seed.SetBoxes(boxLines.Select(l => RandomizerBox.Parse(l)).ToList());
        seed.Save();
    }
}

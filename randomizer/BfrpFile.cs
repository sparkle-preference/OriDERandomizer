using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// A practice container: segment.json, base save, run history, placements and ghosts, held in memory.
// A .bfrp is a ZipStore that Save() rewrites whole; a folder is a FolderStore that writes what changed.
public class BfrpFile {
    public const string SegmentEntry = "segment.json";

    public const string SaveEntry = "save.sav";

    public const string RunsEntry = "runs.csv";

    public const string PlacementsEntry = "placements.bfr";

    public string Path;

    public JsonValue Segment;

    private IEntryStore store;

    // a save since loading wrote something an export carries; only a folder ever says so
    public bool Edited;

    public bool IsFolder {
        get { return store is FolderStore; }
    }

    public struct Run {
        public string Date;

        public int Pickups;

        public long Ms;
    }

    public static BfrpFile Load(string path) {
        var file = new BfrpFile();
        file.Path = path;
        file.store = System.IO.Directory.Exists(path) ? (IEntryStore)FolderStore.Read(path) : ZipStore.Read(path);
        if (!file.store.Has(SegmentEntry) || !file.store.Has(SaveEntry)) {
            throw new System.IO.IOException(path + ": not a practice segment (missing "
                + (file.store.Has(SegmentEntry) ? SaveEntry : SegmentEntry) + ")");
        }

        file.Segment = JsonValue.Parse(Encoding.UTF8.GetString(file.store.Get(SegmentEntry)));
        return file;
    }

    // a path ending in .bfrp makes a zip, any other a folder
    public static BfrpFile Create(string path, JsonValue segment, byte[] baseSave) {
        var file = new BfrpFile();
        file.Path = path;
        file.store = path.EndsWith(".bfrp", StringComparison.OrdinalIgnoreCase) ? (IEntryStore)new ZipStore() : new FolderStore();
        file.Segment = segment;
        file.store.Set(SegmentEntry, new byte[0]);
        file.store.Set(SaveEntry, baseSave);
        return file;
    }

    public byte[] BaseSave {
        get { return store.Get(SaveEntry); }
    }

    // a new start for every attempt from here on; history and ghosts stay as they were
    public void SetBaseSave(byte[] save) {
        store.Set(SaveEntry, save);
    }

    // --- placements.bfr, the root's or a variant's: seed lines and box lines ---

    public List<string> PlacementLines(string variant) {
        var lines = new List<string>();
        var raw = store.Get(Where(variant, PlacementsEntry));
        if (raw == null) {
            return lines;
        }

        foreach (var line in Encoding.UTF8.GetString(raw).Split('\n')) {
            var trimmed = line.Trim();
            if (trimmed != "") {
                lines.Add(trimmed);
            }
        }

        return lines;
    }

    public void SetPlacementLines(string variant, List<string> lines) {
        var entry = Where(variant, PlacementsEntry);
        if (lines.Count == 0) {
            if (store.Has(entry)) {
                store.Remove(entry);
            }

            return;
        }

        store.Set(entry, Encoding.UTF8.GetBytes(string.Join("\n", lines.ToArray()) + "\n"));
    }

    // a bad box line is reported and skipped, never fatal to the file
    public List<RandomizerBox> Boxes(string variant) {
        var boxes = new List<RandomizerBox>();
        foreach (var line in PlacementLines(variant)) {
            if (!RandomizerBox.IsLine(line)) {
                continue;
            }

            try {
                boxes.Add(RandomizerBox.Parse(line));
            } catch (Exception e) {
                PracticeSegment.Report(e.Message + " in '" + line + "'");
            }
        }

        return boxes;
    }

    // every box line that parses is replaced; one that does not stays as written, like any other line
    public void SetBoxes(string variant, List<RandomizerBox> boxes) {
        var lines = new List<string>();
        foreach (var line in PlacementLines(variant)) {
            if (!RandomizerBox.IsLine(line) || !Parses(line)) {
                lines.Add(line);
            }
        }

        foreach (var box in boxes) {
            lines.Add(box.Line);
        }

        SetPlacementLines(variant, lines);
    }

    // trailing tombstones go, but not up to a number a BM| names, nor the shared ones while variant boxes count on from them
    private void TrimTombstones() {
        int named;
        try {
            named = HighestBoxNamed();
        } catch (Exception) {
            // an unreadable segment might name any box
            return;
        }

        var shared = QuietBoxes("");
        var live = false;
        foreach (var id in Variants) {
            var own = QuietBoxes(id);
            live |= own.Exists(box => !box.Deleted);
            Trim(id, own, named - shared.Count);
        }

        if (!live) {
            Trim("", shared, named);
        }
    }

    // keeps every box up to the last live one and up to number `named` in this list's own count
    private void Trim(string variant, List<RandomizerBox> boxes, int named) {
        var keep = Math.Max(boxes.FindLastIndex(box => !box.Deleted), named) + 1;
        if (keep < boxes.Count) {
            SetBoxes(variant, boxes.GetRange(0, keep));
        }
    }

    // Boxes without reporting a bad line, which Boxes has done already
    private List<RandomizerBox> QuietBoxes(string variant) {
        var boxes = new List<RandomizerBox>();
        foreach (var line in PlacementLines(variant)) {
            if (RandomizerBox.IsLine(line) && Parses(line)) {
                boxes.Add(RandomizerBox.Parse(line));
            }
        }

        return boxes;
    }

    // the highest box number a BM| anywhere in the container names, or -1
    private int HighestBoxNamed() {
        var ids = Variants;
        ids.Insert(0, "");
        var named = -1;
        foreach (var id in ids) {
            foreach (var line in PlacementLines(id)) {
                if (RandomizerBox.IsLine(line)) {
                    var box = Parses(line) ? RandomizerBox.Parse(line) : null;
                    if (box != null && !box.Deleted && box.Item != null) {
                        named = Math.Max(named, Named(box.Item));
                    }
                } else if (!line.StartsWith("//")) {
                    var parts = line.Split('|');
                    named = parts.Length < 3 ? named : Math.Max(named, Named(parts[1], parts[2]));
                }
            }

            var json = id == "" ? Segment : VariantSegment(id);
            named = Math.Max(named, Math.Max(Named(json["inventory"]), Named(json["end"]["items"])));
            for (var g = 0; g < json["shuffle"].Count; g++) {
                named = Math.Max(named, Named(json["shuffle"][g]["give"]));
            }
        }

        return named;
    }

    private static int Named(JsonValue codes) {
        var named = -1;
        for (var i = 0; i < codes.Count; i++) {
            var bar = codes[i].IsString ? codes[i].Str.IndexOf('|') : -1;
            if (bar > 0) {
                named = Math.Max(named, Named(codes[i].Str.Substring(0, bar), codes[i].Str.Substring(bar + 1)));
            }
        }

        return named;
    }

    private static int Named(string code, string value) {
        try {
            return Named(new RandomizerAction(code, value));
        } catch (Exception) {
            return -1;
        }
    }

    // BM|n, BM|n=... name box n; MU and RP name what their pieces do
    private static int Named(RandomizerAction action) {
        if (action.Action == "MU" || action.Action == "RP") {
            var named = -1;
            foreach (var piece in action.Decompose()) {
                named = Math.Max(named, Named(piece));
            }

            return named;
        }

        if (action.Action != "BM") {
            return -1;
        }

        var value = (string)action.Value;
        var eq = value.IndexOf('=');
        int n;
        return int.TryParse((eq < 0 ? value : value.Substring(0, eq)).Trim(), out n) ? n : -1;
    }

    private static bool Parses(string line) {
        try {
            RandomizerBox.Parse(line);
            return true;
        } catch (Exception) {
            return false;
        }
    }

    // --- variants ---
    // With variants every attempt runs one; each keeps its own json, history and ghosts under variants/<id>/.
    public List<string> Variants {
        get {
            var found = new List<string>();
            foreach (var name in store.Names) {
                if (!name.StartsWith(VariantRoot) || !name.EndsWith("/" + SegmentEntry)) {
                    continue;
                }

                var id = name.Substring(VariantRoot.Length);
                id = id.Substring(0, id.Length - SegmentEntry.Length - 1);
                if (id.Length > 0 && id.IndexOf('/') < 0) {
                    found.Add(id);
                }
            }

            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }
    }

    public const string VariantRoot = "variants/";

    // no variant is no variant: the root is not one of its own
    public JsonValue VariantSegment(string variant) {
        if (string.IsNullOrEmpty(variant)) {
            return JsonValue.Null();
        }

        var raw = store.Get(Where(variant, SegmentEntry));
        return raw == null ? JsonValue.Null() : JsonValue.Parse(Encoding.UTF8.GetString(raw));
    }

    public void SetVariantSegment(string variant, JsonValue json) {
        store.Set(Where(variant, SegmentEntry), Encoding.UTF8.GetBytes(json.Serialize(true)));
    }

    // where a variant's own files live; the root when there is no variant
    private string Where(string variant, string entry) {
        return string.IsNullOrEmpty(variant) ? entry : VariantRoot + variant + "/" + entry;
    }

    public byte[] GetGhost(string variant, string slot) {
        return store.Get(Where(variant, "ghosts/" + slot + ".ghost"));
    }

    public void SetGhost(string variant, string slot, byte[] data) {
        store.Set(Where(variant, "ghosts/" + slot + ".ghost"), data);
    }

    public void RemoveGhost(string variant, string slot) {
        store.Remove(Where(variant, "ghosts/" + slot + ".ghost"));
    }

    // the slot names with a ghost stored, for this variant
    public List<string> GhostSlots(string variant) {
        var prefix = Where(variant, "ghosts/");
        var slots = new List<string>();
        foreach (var name in store.Names) {
            if (name.StartsWith(prefix) && name.EndsWith(".ghost") && name.IndexOf('/', prefix.Length) < 0) {
                slots.Add(name.Substring(prefix.Length, name.Length - prefix.Length - ".ghost".Length));
            }
        }

        return slots;
    }

    // An export shares the segment, not its history: no runs.csv, and of the ghosts only the pinned ones.
    public static bool Exported(string entry) {
        var own = entry;
        if (own.StartsWith(VariantRoot)) {
            var slash = own.IndexOf('/', VariantRoot.Length);
            own = slash < 0 ? own : own.Substring(slash + 1);
        }

        if (own == RunsEntry) {
            return false;
        }

        var ghost = own.StartsWith("ghosts/") && own.EndsWith(".ghost") && own.IndexOf('/', "ghosts/".Length) < 0;
        return !ghost || own == "ghosts/pinned.ghost";
    }

    // everything the variant owns: its json, history and ghosts
    public void RemoveVariant(string variant) {
        if (string.IsNullOrEmpty(variant)) {
            return;
        }

        var prefix = VariantRoot + variant + "/";
        foreach (var name in new List<string>(store.Names)) {
            if (name.StartsWith(prefix)) {
                store.Remove(name);
            }
        }
    }

    // the csv rows that parse; any other line is kept as written
    public List<Run> Runs {
        get { return RunsFor(Variant); }
    }

    // the variant an attempt belongs to; empty when the segment has none
    public string Variant = "";

    public List<Run> RunsFor(string variant) {
        var runs = new List<Run>();
        var raw = store.Get(Where(variant, RunsEntry));
        if (raw == null) {
            return runs;
        }

        foreach (var line in Encoding.UTF8.GetString(raw).Split('\n')) {
            var parts = line.Trim().Split(',');
            int pickups;
            long ms;
            if (parts.Length == 3
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out pickups)
                    && long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ms)) {
                var run = new Run();
                run.Date = parts[0];
                run.Pickups = pickups;
                run.Ms = ms;
                runs.Add(run);
            }
        }

        return runs;
    }

    public void AppendRun(DateTime when, int pickups, long ms) {
        var raw = store.Get(Where(Variant, RunsEntry));
        var text = raw == null ? "date,pickups,ms\n" : Encoding.UTF8.GetString(raw);
        if (!text.EndsWith("\n")) {
            text += "\n";
        }

        text += when.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            + "," + pickups.ToString(CultureInfo.InvariantCulture)
            + "," + ms.ToString(CultureInfo.InvariantCulture) + "\n";
        store.Set(Where(Variant, RunsEntry), Encoding.UTF8.GetBytes(text));
    }

    public long BestMs() {
        var best = -1L;
        foreach (var run in Runs) {
            if (best < 0 || run.Ms < best) {
                best = run.Ms;
            }
        }

        return best;
    }

    public long AverageMs() {
        var total = 0L;
        var count = 0;
        foreach (var run in Runs) {
            total += run.Ms;
            count++;
        }

        return count == 0 ? -1L : total / count;
    }

    private void ModernTombstones() {
        var ids = Variants;
        ids.Insert(0, "");
        foreach (var id in ids) {
            var lines = PlacementLines(id);
            var modern = lines.ConvertAll(RandomizerBox.Modern);
            for (var i = 0; i < lines.Count; i++) {
                if (modern[i] != lines[i]) {
                    SetPlacementLines(id, modern);
                    break;
                }
            }
        }
    }

    public void Save() {
        TrimTombstones();
        ModernTombstones();
        store.Set(SegmentEntry, Encoding.UTF8.GetBytes(Segment.Serialize(true)));
        store.Write(Path);
        var folder = store as FolderStore;
        Edited |= folder != null && folder.Wrote.Exists(Exported);
    }
}

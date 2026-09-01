using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// A .bfrp practice container: segment.json + base save + run history +
// optional placements and ghosts, all inside one ZipStore. Everything is
// held in memory; Save() rewrites the file (atomically, via the store).
public class BfrpFile {
    public const string SegmentEntry = "segment.json";

    public const string SaveEntry = "save.sav";

    public const string RunsEntry = "runs.csv";

    public const string PlacementsEntry = "placements.bfr";

    public string Path;

    public JsonValue Segment;

    private ZipStore zip;

    public struct Run {
        public string Date;

        public int Pickups;

        public long Ms;
    }

    public static BfrpFile Load(string path) {
        var file = new BfrpFile();
        file.Path = path;
        file.zip = ZipStore.Read(path);
        if (!file.zip.Has(SegmentEntry) || !file.zip.Has(SaveEntry)) {
            throw new System.IO.IOException(path + ": not a practice file (missing "
                + (file.zip.Has(SegmentEntry) ? SaveEntry : SegmentEntry) + ")");
        }

        file.Segment = JsonValue.Parse(Encoding.UTF8.GetString(file.zip.Get(SegmentEntry)));
        return file;
    }

    public static BfrpFile Create(string path, JsonValue segment, byte[] baseSave) {
        var file = new BfrpFile();
        file.Path = path;
        file.zip = new ZipStore();
        file.Segment = segment;
        file.zip.Set(SegmentEntry, new byte[0]);
        file.zip.Set(SaveEntry, baseSave);
        return file;
    }

    public byte[] BaseSave {
        get { return zip.Get(SaveEntry); }
    }

    public byte[] Placements {
        get { return zip.Get(PlacementsEntry); }
    }

    public void SetPlacements(byte[] data) {
        zip.Set(PlacementsEntry, data);
    }

    // --- variants ---
    //
    // A segment with variants has no plain run: every attempt belongs to one of
    // them. They share this file's save (so they share a start) and its global
    // boxes, and each keeps its own json, run history and ghosts under
    // variants/<id>/.
    public List<string> Variants {
        get {
            var found = new List<string>();
            foreach (var name in zip.Names) {
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

        var raw = zip.Get(Where(variant, SegmentEntry));
        return raw == null ? JsonValue.Null() : JsonValue.Parse(Encoding.UTF8.GetString(raw));
    }

    public void SetVariantSegment(string variant, JsonValue json) {
        zip.Set(Where(variant, SegmentEntry), Encoding.UTF8.GetBytes(json.Serialize(true)));
    }

    // where a variant's own files live; the root when there is no variant
    private string Where(string variant, string entry) {
        return string.IsNullOrEmpty(variant) ? entry : VariantRoot + variant + "/" + entry;
    }

    public byte[] GetGhost(string variant, string slot) {
        return zip.Get(Where(variant, "ghosts/" + slot + ".ghost"));
    }

    public void SetGhost(string variant, string slot, byte[] data) {
        zip.Set(Where(variant, "ghosts/" + slot + ".ghost"), data);
    }

    public void RemoveGhost(string variant, string slot) {
        zip.Remove(Where(variant, "ghosts/" + slot + ".ghost"));
    }

    // the slot names with a ghost stored, for this variant
    public List<string> GhostSlots(string variant) {
        var prefix = Where(variant, "ghosts/");
        var slots = new List<string>();
        foreach (var name in zip.Names) {
            if (name.StartsWith(prefix) && name.EndsWith(".ghost") && name.IndexOf('/', prefix.Length) < 0) {
                slots.Add(name.Substring(prefix.Length, name.Length - prefix.Length - ".ghost".Length));
            }
        }

        return slots;
    }

    // everything the variant owns: its json, history and ghosts
    public void RemoveVariant(string variant) {
        if (string.IsNullOrEmpty(variant)) {
            return;
        }

        var prefix = VariantRoot + variant + "/";
        foreach (var name in new List<string>(zip.Names)) {
            if (name.StartsWith(prefix)) {
                zip.Remove(name);
            }
        }
    }

    // header plus lines that parse; anything else in the csv is someone's
    // hand edit and stays untouched until the next full rewrite
    public List<Run> Runs {
        get { return RunsFor(Variant); }
    }

    // the variant an attempt belongs to; empty when the segment has none
    public string Variant = "";

    public List<Run> RunsFor(string variant) {
        var runs = new List<Run>();
        var raw = zip.Get(Where(variant, RunsEntry));
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
        var raw = zip.Get(Where(Variant, RunsEntry));
        var text = raw == null ? "date,pickups,ms\n" : Encoding.UTF8.GetString(raw);
        if (!text.EndsWith("\n")) {
            text += "\n";
        }

        text += when.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            + "," + pickups.ToString(CultureInfo.InvariantCulture)
            + "," + ms.ToString(CultureInfo.InvariantCulture) + "\n";
        zip.Set(Where(Variant, RunsEntry), Encoding.UTF8.GetBytes(text));
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

    public void Save() {
        zip.Set(SegmentEntry, Encoding.UTF8.GetBytes(Segment.Serialize(true)));
        zip.Write(Path);
    }
}

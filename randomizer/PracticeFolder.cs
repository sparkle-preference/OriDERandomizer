using System;
using System.Collections.Generic;
using System.IO;

// The practice folder on disk: a folder per segment made or extracted here, .bfrp files given by others,
// and export/ holding each folder's shareable .bfrp. Pack, unpack and export copy entries unchanged.
public static class PracticeFolder {
    public const string Extension = ".bfrp";

    public const string ExportDir = "export";

    // export/ and the page's map cache, which no segment may be named
    private static readonly string[] Reserved = { ExportDir, "map-tiles" };

    public static bool IsReserved(string name) {
        foreach (var reserved in Reserved) {
            if (string.Equals(name, reserved, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    // What the chooser lists, by path: every folder holding a segment.json, and every .bfrp file.
    public static List<string> List(string dir) {
        var found = new List<string>();
        foreach (var sub in Directory.GetDirectories(dir)) {
            var name = Path.GetFileName(sub);
            if (!IsReserved(name) && !name.EndsWith(FolderStore.TempSuffix, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(Path.Combine(sub, BfrpFile.SegmentEntry))) {
                found.Add(sub);
            }
        }

        foreach (var file in Directory.GetFiles(dir, "*" + Extension)) {
            if (file.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) {
                found.Add(file);
            }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    // a folder's own name, a file's without its .bfrp
    public static string NameOf(string path) {
        var trimmed = path.TrimEnd('\\', '/');
        return trimmed.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(trimmed)
            ? Path.GetFileNameWithoutExtension(trimmed) : Path.GetFileName(trimmed);
    }

    // A segment's name as a folder name: Windows refuses some characters and drops trailing dots and spaces.
    public static string SafeName(string name) {
        var safe = name ?? "";
        foreach (var bad in Path.GetInvalidFileNameChars()) {
            safe = safe.Replace(bad, '-');
        }

        safe = safe.Trim().TrimEnd('.', ' ');
        if (safe.Length == 0) {
            safe = "Segment";
        }

        return FolderStore.IsDevice(safe) ? safe + "-" : safe;
    }

    // dir/name, else "name (2)" and on: taken by no folder, file or .bfrp, and not a reserved name
    public static string FreePath(string dir, string name) {
        var path = Path.Combine(dir, name);
        for (var n = 2; Taken(path); n++) {
            path = Path.Combine(dir, name + " (" + n + ")");
        }

        return path;
    }

    private static bool Taken(string path) {
        return IsReserved(Path.GetFileName(path)) || Directory.Exists(path) || File.Exists(path)
            || File.Exists(path + Extension) || Directory.Exists(path + Extension);
    }

    // "New Segment N", past every N already named
    public static string NextName(IEnumerable<string> names) {
        var taken = 0;
        foreach (var name in names) {
            taken = Math.Max(taken, Numbered(name));
        }

        return "New Segment " + (taken + 1);
    }

    // N in "New Segment N", else 0; a trailing " (2)" is FreePath dodging a taken name
    private static int Numbered(string name) {
        var bracket = name.IndexOf(" (");
        var stem = bracket > 0 ? name.Substring(0, bracket) : name;
        int n;
        return stem.StartsWith("New Segment ", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(stem.Substring("New Segment ".Length).Trim(), out n) ? n : 0;
    }

    public static void Pack(string folder, string to) {
        Packed(folder, name => true).Write(to);
    }

    private static ZipStore Packed(string folder, Predicate<string> keep) {
        var from = FolderStore.Read(folder);
        var zip = new ZipStore();
        foreach (var name in from.Names) {
            if (keep(name)) {
                zip.Set(name, from.Get(name));
            }
        }

        return zip;
    }

    // Built beside the target and moved in whole, so a cut-short unpack is never listed as a segment.
    public static void Unpack(string from, string folder) {
        var zip = ZipStore.Read(from);
        var store = new FolderStore();
        foreach (var name in zip.Names) {
            store.Set(name, zip.Get(name));
        }

        var temp = folder.TrimEnd('\\', '/') + FolderStore.TempSuffix;
        if (Directory.Exists(temp)) {
            Directory.Delete(temp, true);
        }

        store.Write(temp);
        Directory.Move(temp, folder);
    }

    // Unpacks a given .bfrp into a folder of its name (or the next free one) and moves it inside: both or neither.
    public static string Extract(string bfrp) {
        var full = Path.GetFullPath(bfrp);
        var dir = Path.GetDirectoryName(full);
        var name = NameOf(full);
        var folder = Path.Combine(dir, name);
        if (IsReserved(name) || Directory.Exists(folder) || File.Exists(folder)) {
            folder = FreePath(dir, name);
        }

        Unpack(full, folder);
        try {
            File.Move(full, Path.Combine(folder, Path.GetFileName(full)));
        } catch (Exception) {
            Directory.Delete(folder, true);
            throw;
        }

        return folder;
    }

    // practice/export/<folder name>.bfrp
    public static string ExportPath(string folder) {
        var full = Path.GetFullPath(folder).TrimEnd('\\', '/');
        return Path.Combine(Path.Combine(Path.GetDirectoryName(full), ExportDir), Path.GetFileName(full) + Extension);
    }

    // the folder as saved, less what BfrpFile.Exported leaves out; ifChanged leaves an export that matches alone
    public static bool Export(string folder, string to, bool ifChanged) {
        var zip = Packed(folder, BfrpFile.Exported);
        if (ifChanged && Holds(to, zip)) {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(to));
        zip.Write(to);
        return true;
    }

    private static bool Holds(string path, ZipStore zip) {
        if (!File.Exists(path)) {
            return false;
        }

        ZipStore old;
        try {
            old = ZipStore.Read(path);
        } catch (Exception) {
            return false;
        }

        if (old.Names.Count != zip.Names.Count) {
            return false;
        }

        foreach (var name in zip.Names) {
            if (!FolderStore.Same(old.Get(name), zip.Get(name))) {
                return false;
            }
        }

        return true;
    }
}

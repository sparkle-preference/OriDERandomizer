using System;
using System.Collections.Generic;
using System.IO;

// A practice container unpacked: each entry is a file under one folder, named by its path there.
// Write touches only the entries that changed, each through a temp file swapped in whole.
public class FolderStore : IEntryStore {
    // a write cut short leaves one of these behind, and Read never takes it for an entry
    public const string TempSuffix = ".bfrp-tmp";

    private readonly List<string> order = new List<string>();

    private readonly Dictionary<string, byte[]> entries = new Dictionary<string, byte[]>();

    private readonly HashSet<string> dirty = new HashSet<string>();

    private readonly HashSet<string> removed = new HashSet<string>();

    // the folder these entries were read from or last written to; a Write anywhere else writes them all
    private string root;

    // what the last Write created, replaced or deleted
    public readonly List<string> Wrote = new List<string>();

    public List<string> Names {
        get { return new List<string>(order); }
    }

    public bool Has(string name) {
        return entries.ContainsKey(name);
    }

    public byte[] Get(string name) {
        byte[] data;
        return entries.TryGetValue(name, out data) ? data : null;
    }

    // the same bytes again are no change, so the file on disk is left alone
    public void Set(string name, byte[] data) {
        Parts(name);
        byte[] old;
        if (entries.TryGetValue(name, out old)) {
            if (Same(old, data)) {
                return;
            }
        } else {
            order.Add(name);
        }

        entries[name] = data;
        dirty.Add(name);
        removed.Remove(name);
    }

    public void Remove(string name) {
        if (entries.Remove(name)) {
            order.Remove(name);
            dirty.Remove(name);
            removed.Add(name);
        }
    }

    public static FolderStore Read(string path) {
        var store = new FolderStore();
        var full = Full(path);
        var files = Directory.GetFiles(full, "*", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        foreach (var file in files) {
            if (file.EndsWith(TempSuffix, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var name = file.Substring(full.Length + 1).Replace('\\', '/');
            // a .bfrp at the top is the file the folder was extracted from, not an entry
            if (name.IndexOf('/') < 0 && name.EndsWith(".bfrp", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            store.order.Add(name);
            store.entries[name] = File.ReadAllBytes(file);
        }

        store.root = full;
        return store;
    }

    public void Write(string path) {
        var full = Full(path);
        var all = root == null || !string.Equals(root, full, StringComparison.OrdinalIgnoreCase);
        Wrote.Clear();
        Directory.CreateDirectory(full);
        foreach (var name in order) {
            if (!all && !dirty.Contains(name)) {
                continue;
            }

            var file = Where(full, name);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            var temp = file + TempSuffix;
            File.WriteAllBytes(temp, entries[name]);
            if (File.Exists(file)) {
                File.Replace(temp, file, null);
            } else {
                File.Move(temp, file);
            }

            Wrote.Add(name);
        }

        if (!all) {
            foreach (var name in removed) {
                var file = Where(full, name);
                if (File.Exists(file)) {
                    File.Delete(file);
                    Wrote.Add(name);
                    Prune(full, Path.GetDirectoryName(file));
                }
            }
        }

        dirty.Clear();
        removed.Clear();
        root = full;
    }

    // a folder a removal left empty goes too, up to the container's own
    private static void Prune(string root, string dir) {
        while (dir != null && dir.Length > root.Length && Directory.Exists(dir)
                && Directory.GetFileSystemEntries(dir).Length == 0) {
            Directory.Delete(dir);
            dir = Path.GetDirectoryName(dir);
        }
    }

    private static string Full(string path) {
        return Path.GetFullPath(path).TrimEnd('\\', '/');
    }

    // an entry's file under root; a name that could reach anywhere else is refused
    public static string Where(string root, string name) {
        var path = root;
        foreach (var part in Parts(name)) {
            path = Path.Combine(path, part);
        }

        return path;
    }

    // every part a plain file name, so no entry of a shared .bfrp can land outside its folder
    public static string[] Parts(string name) {
        var parts = (name ?? "").Split('/');
        foreach (var part in parts) {
            if (part.Length == 0 || part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                    || part.IndexOfAny(new[] { ':', '\\' }) >= 0 || part.EndsWith(".") || part.EndsWith(" ") || IsDevice(part)) {
                throw new IOException("'" + name + "' is not a name a practice file can use");
            }
        }

        return parts;
    }

    private static readonly string[] Devices = {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    // Windows opens a device for these whatever the extension
    public static bool IsDevice(string part) {
        var stem = part.Split('.')[0].Trim();
        foreach (var device in Devices) {
            if (string.Equals(stem, device, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    public static bool Same(byte[] a, byte[] b) {
        if (a == null || b == null || a.Length != b.Length) {
            return a == b;
        }

        for (var i = 0; i < a.Length; i++) {
            if (a[i] != b[i]) {
                return false;
            }
        }

        return true;
    }
}

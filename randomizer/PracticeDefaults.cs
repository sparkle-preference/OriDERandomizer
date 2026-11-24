using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

// The server's default segments, added to the practice folder at launch. The hidden ledger names every
// one ever added here, so a default the player deleted or renamed never comes back.
public static class PracticeDefaults {
    public const string Ledger = ".defaults";

    // this launch's list from the server, in its order; null until the fetch lands
    private static volatile string[] listed;

    // main thread: the settings are read here, and the transfers wait out the boot-time areas fetch
    public static void Start() {
        if (!NativeWebSocket.Loaded || !NativeWebSocket.HttpAvailable) {
            return;
        }

        var folder = Path.GetFullPath(PracticeSelect.Folder);
        var url = RandomizerSyncManager.WebBase() + "/netcode/practice";
        var thread = new Thread(() => Fetch(folder, url));
        thread.IsBackground = true;
        thread.Name = "practice-defaults";
        thread.Start();
    }

    private static void Fetch(string folder, string url) {
        try {
            var areas = RandomizerLocationManager.DLThread;
            if (areas != null) {
                areas.Join();
            }

            var list = Path.Combine(Path.GetTempPath(), "orirando_practice_defaults.txt");
            // two tries: a boot-time TLS request can fail once
            var status = 0;
            for (var attempt = 0; attempt < 2; attempt++) {
                File.Delete(list);
                status = NativeWebSocket.HttpDownload(url, list);
                if (status == 200) {
                    break;
                }

                Randomizer.log($"practice defaults: list attempt {attempt + 1} gave {status} ({NativeWebSocket.GetLastHttpError()})");
                Thread.Sleep(500);
            }

            // an empty body may leave no file at all
            var offered = status == 200 && File.Exists(list) ? File.ReadAllLines(list, Encoding.UTF8) : new string[0];
            File.Delete(list);
            if (status == 200) {
                listed = offered;
            }

            Sync(folder, offered, (name, to) => NativeWebSocket.HttpDownload(url + "/" + Uri.EscapeDataString(name + PracticeFolder.Extension), to));
        } catch (Exception e) {
            Randomizer.log("practice defaults: " + e.Message);
        }
    }

    // Adds each offered default the folder lacks and never had; download(name, path) returns an HTTP status.
    public static List<string> Sync(string folder, IEnumerable<string> offered, Func<string, string, int> download) {
        var added = new List<string>();
        var ledger = Path.Combine(folder, Ledger);
        var had = new HashSet<string>(Delivered(ledger), StringComparer.OrdinalIgnoreCase);
        foreach (var line in offered) {
            var name = line.Trim();
            if (name.Length == 0 || had.Contains(name)) {
                continue;
            }

            // the name becomes a path, so only one SafeName would leave alone
            if (PracticeFolder.SafeName(name) != name) {
                Randomizer.log("practice defaults: refused the name \"" + name + "\"");
                continue;
            }

            if (PracticeFolder.Taken(Path.Combine(folder, name)) || !Deliver(folder, name, download)) {
                continue;
            }

            Record(ledger, name);
            had.Add(name);
            added.Add(name);
        }

        return added;
    }

    // fetched beside its final name and moved in once it loads, so the chooser never lists half a file
    private static bool Deliver(string folder, string name, Func<string, string, int> download) {
        var part = Path.Combine(folder, name + FolderStore.TempSuffix);
        try {
            Directory.CreateDirectory(folder);
            var status = download(name, part);
            if (status != 200) {
                Randomizer.log($"practice defaults: {name} gave {status}");
                return false;
            }

            BfrpFile.Load(part);
            if (PracticeFolder.Taken(Path.Combine(folder, name))) {
                return false;
            }

            File.Move(part, Path.Combine(folder, name + PracticeFolder.Extension));
            Randomizer.log("practice defaults: added " + name);
            return true;
        } catch (Exception e) {
            Randomizer.log("practice defaults: " + name + ": " + e.Message);
            return false;
        } finally {
            try {
                File.Delete(part);
            } catch (Exception) {
            }
        }
    }

    // The chooser's order: the player's own segments as given, then the defaults in the server's order,
    // or the order they arrived in until this launch's list does.
    public static List<string> Arrange(string folder, List<string> paths) {
        List<string> delivered;
        try {
            delivered = Delivered(Path.Combine(folder, Ledger));
        } catch (Exception e) {
            Randomizer.log("practice defaults: could not read the ledger: " + e.Message);
            return paths;
        }

        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in (listed ?? new string[0]).Select(line => line.Trim()).Concat(delivered)) {
            if (!rank.ContainsKey(name)) {
                rank[name] = rank.Count;
            }
        }

        var had = new HashSet<string>(delivered, StringComparer.OrdinalIgnoreCase);
        var own = paths.Where(path => !had.Contains(PracticeFolder.NameOf(path))).ToList();
        // OrderBy is stable, so a folder and a .bfrp of one name keep the order given
        own.AddRange(paths.Where(path => had.Contains(PracticeFolder.NameOf(path))).OrderBy(path => rank[PracticeFolder.NameOf(path)]));
        return own;
    }

    // the ledger's names, in the order they were delivered
    private static List<string> Delivered(string ledger) {
        var names = new List<string>();
        if (File.Exists(ledger)) {
            foreach (var line in File.ReadAllLines(ledger, Encoding.UTF8)) {
                if (line.Trim().Length > 0) {
                    names.Add(line.Trim());
                }
            }
        }

        return names;
    }

    // appended, never rewritten: Windows won't truncate a hidden file
    private static void Record(string ledger, string name) {
        var fresh = !File.Exists(ledger);
        File.AppendAllText(ledger, name + "\n");
        if (fresh) {
            File.SetAttributes(ledger, File.GetAttributes(ledger) | FileAttributes.Hidden);
        }
    }
}

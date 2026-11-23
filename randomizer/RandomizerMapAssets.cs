using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

// The map layer pictures, kept out of the dll: fetched from the server repo on GitHub into
// MapLayers/ at startup, and read from there. Offline, whatever came down before still shows.
public static class RandomizerMapAssets {
    private const string Source = "https://raw.githubusercontent.com/sparkle-preference/ori_rando_server/master/map_layers/";

    private const string Dir = "MapLayers";

    // bumped when a fetch brings new files, so whatever read the old ones looks again
    public static int Version;

    // a dll that carries the pictures as resources fetches nothing; a file in MapLayers/ still wins over the dll's copy
    private static readonly HashSet<string> carried = new HashSet<string>(RandomizerResources.ListResources());

    public static void Start() {
        if (thread == null && !carried.Contains("manifest.txt")) {
            thread = new Thread(Fetch) { IsBackground = true };
            thread.Start();
        }
    }

    public static byte[] Read(string name) {
        try {
            var path = Path.Combine(Dir, name);
            if (File.Exists(path)) {
                return File.ReadAllBytes(path);
            }

            return carried.Contains(name) ? RandomizerResources.ReadResource(name) : null;
        } catch (Exception) {
            return null;
        }
    }

    // its own thread: Randomizer.log only, since LogError draws on screen
    private static void Fetch() {
        var manifest = Path.Combine(Dir, "manifest.txt.part");
        var changed = new List<string>();
        var fetched = 0;
        try {
            for (var i = 0; i < 60 && !(NativeWebSocket.Loaded && NativeWebSocket.HttpAvailable); i++) {
                Thread.Sleep(500);
            }

            if (!NativeWebSocket.Loaded || !NativeWebSocket.HttpAvailable) {
                Randomizer.log("map layers: no sidecar to fetch them with; keeping what's cached");
                return;
            }

            var areas = RandomizerLocationManager.DLThread;
            if (areas != null) {
                areas.Join();
            }

            Directory.CreateDirectory(Dir);
            var status = Download(Source + "manifest.txt", manifest);
            if (status != 200) {
                Randomizer.log($"map layers: the manifest gave {Failure(status)}; keeping what's cached");
                return;
            }

            // every changed file comes down beside the old set before any moves in, so the cache is always one whole set
            foreach (var line in File.ReadAllLines(manifest)) {
                var parts = line.Trim().Split(' ');
                if (parts.Length != 2 || parts[0].StartsWith("#")) {
                    continue;
                }

                var file = Path.Combine(Dir, parts[0]);
                if (File.Exists(file) && Md5(file) == parts[1]) {
                    continue;
                }

                changed.Add(file);
                status = Download(Source + parts[0], file + ".part");
                if (status != 200 || Md5(file + ".part") != parts[1]) {
                    Randomizer.log($"map layers: {parts[0]} gave {(status == 200 ? "bytes the manifest doesn't list" : Failure(status))}; keeping what's cached until the next launch");
                    return;
                }
            }

            foreach (var file in changed) {
                if (File.Exists(file)) {
                    File.Delete(file);
                }

                File.Move(file + ".part", file);
                fetched++;
            }

            // kept beside the set it describes: the editor page reads it to tell which layouts share a picture
            File.Copy(manifest, Path.Combine(Dir, "manifest.txt"), true);

            if (fetched > 0) {
                Randomizer.log($"map layers: {fetched} fetched");
            }
        } catch (Exception e) {
            Randomizer.log($"map layers: the fetch failed: {e.Message}");
        } finally {
            foreach (var file in changed) {
                try {
                    File.Delete(file + ".part");
                } catch (Exception) {
                }
            }

            try {
                File.Delete(manifest);
            } catch (Exception) {
            }

            if (fetched > 0) {
                Interlocked.Increment(ref Version);
            }
        }
    }

    // an HTTP status, or below zero the sidecar's reason it never got one
    private static string Failure(int status) {
        return status < 0 ? NativeWebSocket.GetLastHttpError() : "status " + status;
    }

    // two tries: a boot-time TLS request can fail once
    private static int Download(string url, string to) {
        var status = 0;
        for (var attempt = 0; attempt < 2; attempt++) {
            File.Delete(to);
            status = NativeWebSocket.HttpDownload(url, to);
            if (status == 200) {
                break;
            }

            Thread.Sleep(500);
        }

        return status;
    }

    private static string Md5(string path) {
        using (var md5 = MD5.Create()) {
            using (var stream = File.OpenRead(path)) {
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    private static Thread thread;
}

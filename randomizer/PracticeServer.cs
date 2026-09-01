using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// The companion page's back end: a loopback listener speaking just enough HTTP for one
// page. A request that touches the game is handed to the main thread and answered once
// it has been round; nothing here reads game state off its own thread. It also keeps the
// site's map tiles: cached beside the segments once, redirected to the site until then.
public static class PracticeServer {
    public const int FirstPort = 47826;

    private const int MaxHeader = 64 * 1024;

    private const int MaxBody = 4 * 1024 * 1024;

    private static TcpListener listener;

    private static int port;

    private static readonly Queue<Job> jobs = new Queue<Job>();

    private static readonly object gate = new object();

    // the browser is opened once per game launch, the first time the box editor starts;
    // a page that pops over the game every session would cost the player their focus
    private static bool opened;

    private class Job {
        public Func<string> Work;
        public string Result;
        public string Error;
        public readonly ManualResetEvent Done = new ManualResetEvent(false);
    }

    public static string Url {
        get { return "http://127.0.0.1:" + port + "/"; }
    }

    public static bool Start() {
        if (listener != null) {
            return true;
        }

        for (var candidate = FirstPort; candidate < FirstPort + 10; candidate++) {
            try {
                var bound = new TcpListener(IPAddress.Loopback, candidate);
                bound.Start();
                listener = bound;
                port = candidate;
                var thread = new Thread(Loop);
                thread.IsBackground = true;
                thread.Name = "practice-editor-page";
                thread.Start();
                Randomizer.log("practice: editor page at " + Url);
                EnsureTiles();
                return true;
            } catch (SocketException) {
            }
        }

        Randomizer.LogError("practice: no free port for the editor page");
        return false;
    }

    public static void Open() {
        if (!Start()) {
            return;
        }

        try {
            Application.OpenURL(Url);
        } catch (Exception e) {
            Randomizer.LogError("practice: could not open the editor page: " + e.Message);
        }
    }

    public static void OpenOnce() {
        if (!opened) {
            opened = true;
            Open();
        }
    }

    public static void SessionEnded() {
    }

    public static bool Running {
        get { return listener != null; }
    }

    // main thread: answer what the page asked for
    public static void Tick() {
        while (true) {
            Job job;
            lock (gate) {
                if (jobs.Count == 0) {
                    return;
                }

                job = jobs.Dequeue();
            }

            try {
                job.Result = job.Work();
            } catch (Exception e) {
                job.Error = e.Message;
            }

            job.Done.Set();
        }
    }

    private static string OnMain(Func<string> work, out string error) {
        var job = new Job();
        job.Work = work;
        lock (gate) {
            jobs.Enqueue(job);
        }

        if (!job.Done.WaitOne(5000, false)) {
            error = "the game did not answer in time";
            return null;
        }

        error = job.Error;
        return job.Result;
    }

    private static void Loop() {
        while (listener != null) {
            TcpClient client = null;
            try {
                client = listener.AcceptTcpClient();
                Serve(client);
            } catch (Exception e) {
                if (listener == null) {
                    break;
                }

                Randomizer.log("practice: editor page request failed: " + e.Message);
            } finally {
                if (client != null) {
                    client.Close();
                }
            }
        }
    }

    private static void Serve(TcpClient client) {
        client.ReceiveTimeout = 5000;
        client.SendTimeout = 5000;
        var stream = client.GetStream();
        string method;
        string path;
        byte[] body;
        if (!ReadRequest(stream, out method, out path, out body)) {
            return;
        }

        var query = path.IndexOf('?');
        if (query >= 0) {
            path = path.Substring(0, query);
        }

        var status = 200;
        var type = "application/json; charset=utf-8";
        string location = null;
        byte[] content;
        string error = null;
        if (method == "GET" && path == "/") {
            content = Resource("practice_editor.html", "text/html; charset=utf-8", out type, out status);
        } else if (method == "GET" && path == "/leaflet.js") {
            content = Resource("leaflet.js", "text/javascript; charset=utf-8", out type, out status);
        } else if (method == "GET" && path == "/leaflet.css") {
            content = Resource("leaflet.css", "text/css; charset=utf-8", out type, out status);
        } else if (method == "GET" && path.StartsWith("/tiles/")) {
            content = Tile(path, out type, out status, out location);
        } else if (method == "GET" && path == "/api/tiles") {
            content = Encoding.UTF8.GetBytes(TileStatus());
        } else if (method == "GET" && path == "/api/segment") {
            content = Reply(OnMain(ReadSegment, out error), error, out status);
        } else if ((method == "PUT" || method == "POST") && path == "/api/segment") {
            var text = Encoding.UTF8.GetString(body);
            content = Reply(OnMain(delegate { return WriteSegment(text); }, out error), error, out status);
        } else if (method == "GET" && path == "/api/catalog") {
            content = Reply(OnMain(Catalog, out error), error, out status);
        } else if (method == "GET" && path == "/api/ghosts") {
            content = Reply(OnMain(Ghosts, out error), error, out status);
        } else if (method == "POST" && path == "/api/ghost/pin") {
            content = Reply(OnMain(Pin, out error), error, out status);
        } else if (method == "DELETE" && path.StartsWith("/api/ghost/")) {
            var slot = path.Substring("/api/ghost/".Length);
            content = Reply(OnMain(delegate { return DeleteGhost(slot); }, out error), error, out status);
        } else if (method == "DELETE" && path.StartsWith("/api/variant/")) {
            var id = path.Substring("/api/variant/".Length);
            content = Reply(OnMain(delegate { return DeleteVariant(id); }, out error), error, out status);
        } else {
            status = 404;
            content = Encoding.UTF8.GetBytes("{\"error\":\"not found\"}");
        }

        var head = "HTTP/1.1 " + status + " " + Reason(status) + "\r\n"
            + "Content-Type: " + type + "\r\n"
            + "Content-Length: " + content.Length + "\r\n"
            + (location == null ? "" : "Location: " + location + "\r\n")
            + "Cache-Control: " + (path.StartsWith("/tiles/") && status == 200 ? "max-age=86400" : "no-store") + "\r\n"
            + "Connection: close\r\n\r\n";
        var bytes = Encoding.ASCII.GetBytes(head);
        stream.Write(bytes, 0, bytes.Length);
        stream.Write(content, 0, content.Length);
        stream.Flush();
    }

    private static byte[] Resource(string name, string wanted, out string type, out int status) {
        var content = RandomizerResources.ReadResource(name);
        if (content != null) {
            type = wanted;
            status = 200;
            return content;
        }

        type = "text/plain; charset=utf-8";
        status = 500;
        return Encoding.UTF8.GetBytes(name + " is missing from the build");
    }

    private static byte[] Reply(string result, string error, out int status) {
        if (result != null) {
            status = 200;
            return Encoding.UTF8.GetBytes(result);
        }

        status = error == "the game did not answer in time" ? 503 : 400;
        var reply = JsonValue.NewObject();
        reply.Set("error", JsonValue.Of(error ?? "failed"));
        return Encoding.UTF8.GetBytes(reply.Serialize(false));
    }

    private static string Reason(int status) {
        switch (status) {
            case 200: return "OK";
            case 302: return "Found";
            case 400: return "Bad Request";
            case 404: return "Not Found";
            case 503: return "Service Unavailable";
            default: return "Error";
        }
    }

    // Request line, headers, then exactly Content-Length bytes of body.
    private static bool ReadRequest(NetworkStream stream, out string method, out string path, out byte[] body) {
        method = null;
        path = null;
        body = new byte[0];
        var buffer = new MemoryStream();
        var chunk = new byte[4096];
        var headerEnd = -1;
        while (headerEnd < 0) {
            var read = stream.Read(chunk, 0, chunk.Length);
            if (read <= 0) {
                return false;
            }

            buffer.Write(chunk, 0, read);
            headerEnd = IndexOf(buffer.GetBuffer(), (int)buffer.Length, "\r\n\r\n");
            if (buffer.Length > MaxHeader) {
                return false;
            }
        }

        var raw = buffer.GetBuffer();
        var header = Encoding.ASCII.GetString(raw, 0, headerEnd);
        var lines = header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) {
            return false;
        }

        var request = lines[0].Split(' ');
        if (request.Length < 2) {
            return false;
        }

        method = request[0].ToUpperInvariant();
        path = request[1];
        var length = 0;
        for (var i = 1; i < lines.Length; i++) {
            var colon = lines[i].IndexOf(':');
            if (colon > 0 && lines[i].Substring(0, colon).Trim().ToLowerInvariant() == "content-length") {
                int.TryParse(lines[i].Substring(colon + 1).Trim(), out length);
            }
        }

        if (length < 0 || length > MaxBody) {
            return false;
        }

        var bodyStart = headerEnd + 4;
        var have = (int)buffer.Length - bodyStart;
        body = new byte[length];
        Array.Copy(raw, bodyStart, body, 0, Math.Min(have, length));
        var got = Math.Min(have, length);
        while (got < length) {
            var read = stream.Read(body, got, length - got);
            if (read <= 0) {
                return false;
            }

            got += read;
        }

        return true;
    }

    private static int IndexOf(byte[] data, int length, string marker) {
        var pattern = Encoding.ASCII.GetBytes(marker);
        for (var i = 0; i + pattern.Length <= length; i++) {
            var match = true;
            for (var j = 0; j < pattern.Length && match; j++) {
                match = data[i + j] == pattern[j];
            }

            if (match) {
                return i;
            }
        }

        return -1;
    }

    // --- the map ---------------------------------------------------------------------

    // The site's tile pyramid: 20480 x 14592 pixels at zoom 7, 256-pixel tiles, so
    // ceil(80 * 2^z / 128) by ceil(57 * 2^z / 128) tiles at zoom z. Zoom 7 is nearly all
    // blank and is left to the site.
    private const string TileHost = "https://ori-tracker.firebaseapp.com/images/ori-map/";

    private const int TileZooms = 7;

    private static bool tilesRunning;

    private static int tilesHave;

    private static int tilesTotal;

    private static string tilesNote = "";

    private static string TileDir {
        get { return Path.Combine(PracticeSelect.Folder, "map-tiles"); }
    }

    private static int Cols(int z) {
        return (80 * (1 << z) + 127) / 128;
    }

    private static int Rows(int z) {
        return (57 * (1 << z) + 127) / 128;
    }

    private static byte[] Tile(string path, out string type, out int status, out string location) {
        type = "image/png";
        location = null;
        var parts = path.Substring("/tiles/".Length).Split('/');
        int z, x, y;
        if (parts.Length != 3 || !parts[2].EndsWith(".png") || !int.TryParse(parts[0], out z)
                || !int.TryParse(parts[1], out x) || !int.TryParse(parts[2].Substring(0, parts[2].Length - 4), out y)
                || z < 0 || z > TileZooms || x < 0 || y < 0) {
            type = "application/json; charset=utf-8";
            status = 404;
            return Encoding.UTF8.GetBytes("{\"error\":\"no such tile\"}");
        }

        var file = Path.Combine(Path.Combine(Path.Combine(TileDir, z.ToString()), x.ToString()), y + ".png");
        if (File.Exists(file)) {
            try {
                status = 200;
                return File.ReadAllBytes(file);
            } catch (IOException) {
            }
        }

        // not cached yet: the browser fetches it from the site itself
        status = 302;
        location = TileHost + z + "/" + x + "/" + y + ".png";
        return new byte[0];
    }

    private static string TileStatus() {
        var reply = JsonValue.NewObject();
        reply.Set("have", JsonValue.Of(tilesHave));
        reply.Set("total", JsonValue.Of(tilesTotal));
        reply.Set("running", JsonValue.Of(tilesRunning));
        reply.Set("available", JsonValue.Of(NativeWebSocket.HttpAvailable));
        reply.Set("note", JsonValue.Of(tilesNote));
        return reply.Serialize(false);
    }

    // once: everything below zoom 7 that is not on disk yet, through the sidecar, which
    // has the TLS this runtime lacks
    private static void EnsureTiles() {
        if (tilesRunning) {
            return;
        }

        tilesRunning = true;
        var thread = new Thread(FetchTiles);
        thread.IsBackground = true;
        thread.Name = "practice-map-tiles";
        thread.Start();
    }

    private static void FetchTiles() {
        try {
            var missing = new List<int[]>();
            var total = 0;
            for (var z = 0; z < TileZooms; z++) {
                for (var x = 0; x < Cols(z); x++) {
                    for (var y = 0; y < Rows(z); y++) {
                        total++;
                        var file = Path.Combine(Path.Combine(Path.Combine(TileDir, z.ToString()), x.ToString()), y + ".png");
                        if (!File.Exists(file)) {
                            missing.Add(new[] { z, x, y });
                        }
                    }
                }
            }

            tilesTotal = total;
            tilesHave = total - missing.Count;
            if (missing.Count == 0) {
                tilesNote = "map cached";
                return;
            }

            if (!NativeWebSocket.HttpAvailable) {
                tilesNote = "no sidecar to fetch the map with; the page reads tiles from the site while online";
                Randomizer.log("practice: " + tilesNote);
                return;
            }

            tilesNote = "fetching the map";
            Randomizer.log("practice: fetching " + missing.Count + " map tiles into " + TileDir);
            var failures = 0;
            foreach (var tile in missing) {
                var dir = Path.Combine(Path.Combine(TileDir, tile[0].ToString()), tile[1].ToString());
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, tile[2] + ".png");
                var status = NativeWebSocket.HttpDownload(TileHost + tile[0] + "/" + tile[1] + "/" + tile[2] + ".png", file);
                if (status == 200 && File.Exists(file) && new FileInfo(file).Length > 0) {
                    tilesHave++;
                    continue;
                }

                if (File.Exists(file)) {
                    File.Delete(file);
                }

                if (++failures > 25) {
                    tilesNote = "map fetch gave up after " + failures + " failures (last status " + status + ")";
                    Randomizer.log("practice: " + tilesNote);
                    return;
                }
            }

            tilesNote = tilesHave == tilesTotal ? "map cached" : "map partly cached";
            Randomizer.log("practice: map tiles " + tilesHave + "/" + tilesTotal);
        } catch (Exception e) {
            tilesNote = "map fetch failed: " + e.Message;
            Randomizer.log("practice: " + tilesNote);
        } finally {
            tilesRunning = false;
        }
    }

    // --- the segment -----------------------------------------------------------------

    // What the page edits: the container's root json and every variant's, serialised
    // here on the main thread so the page never reads a tree the editor is changing.
    private static string ReadSegment() {
        var file = PracticeController.File;
        var reply = JsonValue.NewObject();
        reply.Set("session", JsonValue.Of(file != null));
        if (file == null) {
            return reply.Serialize(false);
        }

        reply.Set("path", JsonValue.Of(file.Path));
        reply.Set("variant", JsonValue.Of(file.Variant ?? ""));
        reply.Set("editing", JsonValue.Of(PracticeEditor.Active));
        reply.Set("segment", file.Segment);
        var variants = JsonValue.NewObject();
        foreach (var id in file.Variants) {
            variants.Set(id, file.VariantSegment(id));
        }

        reply.Set("variants", variants);
        return reply.Serialize(false);
    }

    // The page's copy replaces the file's, is written out, and a live session runs the
    // new rules at once.
    private static string WriteSegment(string text) {
        var file = PracticeController.File;
        if (file == null) {
            throw new Exception("no practice session is running");
        }

        var incoming = JsonValue.Parse(text);
        var segment = incoming["segment"];
        if (!segment.IsObject) {
            throw new Exception("segment must be an object");
        }

        file.Segment = segment;
        var variants = incoming["variants"];
        if (variants.IsObject) {
            foreach (var id in variants.Keys) {
                if (variants[id].IsObject) {
                    file.SetVariantSegment(id, variants[id]);
                }
            }
        }

        file.Save();
        PracticeController.Segment = PracticeSegment.Parse(file.Segment, file.VariantSegment(file.Variant));
        Randomizer.log("practice: segment saved from the editor page");
        return "{\"ok\":true}";
    }

    private static string DeleteVariant(string id) {
        var file = PracticeController.File;
        if (file == null) {
            throw new Exception("no practice session is running");
        }

        if (id == file.Variant) {
            throw new Exception("this variant is the one running; exit the session first");
        }

        file.RemoveVariant(id);
        file.Save();
        Randomizer.log("practice: variant " + id + " removed from the editor page");
        return "{\"ok\":true}";
    }

    // --- ghosts ----------------------------------------------------------------------

    // the running variant's ghost slots, with what the file header says about each
    private static string Ghosts() {
        var file = PracticeController.File;
        var reply = JsonValue.NewObject();
        var slots = JsonValue.NewArray();
        if (file != null) {
            foreach (var slot in file.GhostSlots(file.Variant)) {
                var data = file.GetGhost(file.Variant, slot);
                var entry = JsonValue.NewObject();
                entry.Set("slot", JsonValue.Of(slot));
                entry.Set("bytes", JsonValue.Of(data == null ? 0 : data.Length));
                if (data != null && data.Length >= 9) {
                    var count = (uint)(data[1] | (data[2] << 8) | (data[3] << 16) | (data[4] << 24));
                    var seconds = BitConverter.ToSingle(new[] { data[5], data[6], data[7], data[8] }, 0);
                    entry.Set("samples", JsonValue.Of(count));
                    entry.Set("seconds", JsonValue.Of(Math.Round(seconds, 2)));
                }

                slots.Add(entry);
            }
        }

        reply.Set("ghosts", slots);
        return reply.Serialize(false);
    }

    private static string Pin() {
        if (!PracticeController.PinLastGhost()) {
            throw new Exception("there is no recent run to pin");
        }

        return "{\"ok\":true}";
    }

    private static string DeleteGhost(string slot) {
        var file = PracticeController.File;
        if (file == null) {
            throw new Exception("no practice session is running");
        }

        file.RemoveGhost(file.Variant, slot);
        file.Save();
        Randomizer.log("practice: ghost " + slot + " removed from the editor page");
        return "{\"ok\":true}";
    }

    // --- the catalog -----------------------------------------------------------------

    // what the pickers offer: skills and world events by name, every location by zone
    private static string Catalog() {
        var reply = JsonValue.NewObject();
        var skills = JsonValue.NewArray();
        foreach (var pair in RandomizerItems.SkillNames) {
            skills.Add(Entry("SK|" + pair.Key, pair.Value));
        }

        var events = JsonValue.NewArray();
        foreach (var pair in RandomizerItems.EventNames) {
            events.Add(Entry("EV|" + pair.Key, pair.Value));
        }

        var locations = JsonValue.NewArray();
        foreach (var location in RandomizerLocationManager.LocationsByKey.Values) {
            var entry = JsonValue.NewObject();
            entry.Set("key", JsonValue.Of(location.Key));
            entry.Set("name", JsonValue.Of(location.Name));
            entry.Set("zone", JsonValue.Of(location.Zone ?? ""));
            entry.Set("type", JsonValue.Of(location.Type.ToString()));
            entry.Set("x", JsonValue.Of(Math.Round(location.Position.x, 1)));
            entry.Set("y", JsonValue.Of(Math.Round(location.Position.y, 1)));
            locations.Add(entry);
        }

        reply.Set("skills", skills);
        reply.Set("events", events);
        reply.Set("locations", locations);
        return reply.Serialize(false);
    }

    private static JsonValue Entry(string id, string name) {
        var entry = JsonValue.NewObject();
        entry.Set("id", JsonValue.Of(id));
        entry.Set("name", JsonValue.Of(name));
        return entry;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// The companion page's back end: a loopback listener speaking just enough HTTP for one page.
// Anything touching the game runs as a job on the main thread. Map tiles are cached beside the
// segments once, and redirected to the site until then.
public static class PracticeServer {
    public const int FirstPort = 47826;

    private const int MaxHeader = 64 * 1024;

    private const int MaxBody = 4 * 1024 * 1024;

    private static TcpListener listener;

    private static int port;

    private static readonly Queue<Job> jobs = new Queue<Job>();

    private static readonly object gate = new object();

    // the browser opens once per launch, the first time the box editor starts
    private static bool opened;

    private class Job {
        public Func<string> Work;
        public string Result;
        public string Error;
        // both under gate: a job the page gave up on never starts, and one that started is waited for
        public bool Started;
        public bool Cancelled;
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

    // true when a browser was opened; false when the open page was turned instead, or no port was free
    public static bool Open() {
        if (!Start()) {
            return false;
        }

        if (Turn()) {
            return false;
        }

        try {
            Application.OpenURL(Url);
        } catch (Exception e) {
            Randomizer.LogError("practice: could not open the editor page: " + e.Message);
        }

        return true;
    }

    // --- the pane ----------------------------------------------------------------------

    // The last tab to claim the pane (new or focused) is what Open reloads instead of a new tab.
    // Answered off the main thread: touch nothing of Unity's.
    private const double PaneQuiet = 6.0;

    private static string pane;

    private static DateTime paneSeen;

    private static bool paneTurn;

    // the three pane fields, shared by the request workers and Open on the main thread
    private static readonly object paneGate = new object();

    private static string Pane(string id, bool fresh, bool watched) {
        bool turn;
        lock (paneGate) {
            var now = DateTime.UtcNow;
            var mine = pane == id;
            // the tab being looked at is the one the game should turn, whenever it was opened
            if (fresh || watched || pane == null || mine || (now - paneSeen).TotalSeconds > PaneQuiet) {
                if (!mine) {
                    pane = id;
                    paneTurn = false;
                    mine = true;
                }

                paneSeen = now;
            }

            turn = mine && paneTurn;
            if (turn) {
                paneTurn = false;
            }
        }

        var reply = JsonValue.NewObject();
        reply.Set("reload", JsonValue.Of(turn));
        return reply.Serialize(false);
    }

    // one raw (not URL-decoded) value out of a query string
    private static string Arg(string query, string name) {
        foreach (var pair in query.Split('&')) {
            var eq = pair.IndexOf('=');
            if (eq > 0 && pair.Substring(0, eq) == name) {
                return pair.Substring(eq + 1);
            }
        }

        return "";
    }

    private static bool Turn() {
        lock (paneGate) {
            if (pane == null || (DateTime.UtcNow - paneSeen).TotalSeconds > PaneQuiet) {
                return false;
            }

            paneTurn = true;
            return true;
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
                if (job.Cancelled) {
                    continue;
                }

                job.Started = true;
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

        using (job.Done) {
            if (!job.Done.WaitOne(5000, false)) {
                lock (gate) {
                    job.Cancelled = !job.Started;
                }

                if (job.Cancelled) {
                    error = "the game did not answer in time";
                    return null;
                }

                job.Done.WaitOne();
            }

            error = job.Error;
            return job.Result;
        }
    }

    // a worker per connection, so an idle or slow socket holds up only itself
    private static void Loop() {
        while (listener != null) {
            try {
                ThreadPool.QueueUserWorkItem(Handle, listener.AcceptTcpClient());
            } catch (Exception e) {
                if (listener == null) {
                    break;
                }

                Randomizer.log("practice: editor page connection failed: " + e.Message);
            }
        }
    }

    private static void Handle(object state) {
        var client = (TcpClient)state;
        try {
            Serve(client);
        } catch (Exception e) {
            Randomizer.log("practice: editor page request failed: " + e.Message);
        } finally {
            client.Close();
        }
    }

    private static void Serve(TcpClient client) {
        client.ReceiveTimeout = 5000;
        client.SendTimeout = 5000;
        var stream = client.GetStream();
        string method;
        string path;
        byte[] body;
        Dictionary<string, string> headers;
        if (!ReadRequest(stream, out method, out path, out body, out headers)) {
            return;
        }

        var query = path.IndexOf('?');
        var args = "";
        if (query >= 0) {
            args = path.Substring(query + 1);
            path = path.Substring(0, query);
        }

        var status = 200;
        var type = "application/json; charset=utf-8";
        string location = null;
        byte[] content;
        string error = null;
        if (!Allowed(method, headers, body.Length)) {
            status = 403;
            content = Encoding.UTF8.GetBytes("{\"error\":\"forbidden\"}");
        } else if (method == "GET" && path == "/") {
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
        } else if (method == "PUT" && path == "/api/segment") {
            var text = Encoding.UTF8.GetString(body);
            var stale = false;
            content = Reply(OnMain(delegate { return WriteSegment(text, ref stale); }, out error), error, out status);
            if (stale) {
                status = 409;
            }
        } else if (method == "GET" && path == "/api/pane") {
            content = Encoding.UTF8.GetBytes(Pane(Arg(args, "id"), Arg(args, "new") == "1",
                Arg(args, "see") == "1"));
        } else if (method == "GET" && path == "/api/catalog") {
            content = Reply(OnMain(Catalog, out error), error, out status);
        } else if (method == "GET" && path == "/api/saves") {
            content = Reply(OnMain(Saves, out error), error, out status);
        } else if (method == "POST" && path == "/api/create") {
            var text = Encoding.UTF8.GetString(body);
            content = Reply(OnMain(delegate { return CreateSegment(text); }, out error), error, out status);
        } else if (method == "POST" && path == "/api/save") {
            var text = Encoding.UTF8.GetString(body);
            content = Reply(OnMain(delegate { return ReplaceSave(text); }, out error), error, out status);
        } else if (method == "GET" && path == "/api/ghosts") {
            content = Reply(OnMain(Ghosts, out error), error, out status);
        } else if (method == "POST" && path == "/api/ghost/pin") {
            content = Reply(OnMain(Pin, out error), error, out status);
        } else if (method == "DELETE" && path.StartsWith("/api/ghost/")) {
            var slot = Uri.UnescapeDataString(path.Substring("/api/ghost/".Length));
            content = Reply(OnMain(delegate { return DeleteGhost(slot); }, out error), error, out status);
        } else if (method == "DELETE" && path.StartsWith("/api/variant/")) {
            var id = Uri.UnescapeDataString(path.Substring("/api/variant/".Length));
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

    // Any page in a browser can reach loopback: DNS rebinding arrives under a foreign Host, a cross-site
    // write under a foreign Origin, and a json body cannot cross sites without a preflight, never answered here.
    private static bool Allowed(string method, Dictionary<string, string> headers, int bodyLength) {
        string host, origin, type;
        if (!headers.TryGetValue("host", out host) || !Ours(host)) {
            return false;
        }

        if (method == "GET") {
            return true;
        }

        if (headers.TryGetValue("origin", out origin)
                && !(origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && Ours(origin.Substring(7)))) {
            return false;
        }

        return bodyLength == 0 || (headers.TryGetValue("content-type", out type)
            && type.StartsWith("application/json", StringComparison.OrdinalIgnoreCase));
    }

    private static bool Ours(string host) {
        return string.Equals(host, "127.0.0.1:" + port, StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "localhost:" + port, StringComparison.OrdinalIgnoreCase);
    }

    private static string Reason(int status) {
        switch (status) {
            case 200: return "OK";
            case 302: return "Found";
            case 400: return "Bad Request";
            case 403: return "Forbidden";
            case 404: return "Not Found";
            case 409: return "Conflict";
            case 503: return "Service Unavailable";
            default: return "Error";
        }
    }

    // Request line, headers, then exactly Content-Length bytes of body.
    private static bool ReadRequest(NetworkStream stream, out string method, out string path, out byte[] body,
            out Dictionary<string, string> headers) {
        method = null;
        path = null;
        body = new byte[0];
        headers = new Dictionary<string, string>();
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
            if (colon > 0) {
                headers[lines[i].Substring(0, colon).Trim().ToLowerInvariant()] = lines[i].Substring(colon + 1).Trim();
            }
        }

        string declared;
        if (headers.TryGetValue("content-length", out declared)) {
            int.TryParse(declared, out length);
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

    // The site's pyramid, 20480 x 14592 px at zoom 7 in 256 px tiles: ceil(80 * 2^z / 128) by
    // ceil(57 * 2^z / 128) tiles at zoom z. Zoom 7 is mostly blank and left to the site.
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

    // once per launch: every tile below zoom 7 not on disk yet, through the sidecar's TLS
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
                // written beside the tile and moved in whole, since Tile serves whatever file is there
                var file = Path.Combine(dir, tile[2] + ".png");
                var part = file + ".part";
                var status = NativeWebSocket.HttpDownload(TileHost + tile[0] + "/" + tile[1] + "/" + tile[2] + ".png", part);
                if (status == 200 && File.Exists(part) && new FileInfo(part).Length > 0) {
                    File.Move(part, file);
                    tilesHave++;
                    continue;
                }

                if (File.Exists(part)) {
                    File.Delete(part);
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

    // what the page edits: the root json and every variant's, in the page's shape
    private static string ReadSegment() {
        var file = PracticeController.File;
        var reply = JsonValue.NewObject();
        reply.Set("session", JsonValue.Of(file != null));
        reply.Set("folder", JsonValue.Of(FolderPath()));
        if (file == null) {
            // no session: the page's create panel wants a name and whether the chooser can start one
            reply.Set("suggestedName", JsonValue.Of(PracticeSelect.NextName()));
            reply.Set("choosing", JsonValue.Of(PracticeSelect.Choosing));
            reply.Set("awaiting", JsonValue.Of(PracticeSelect.Waiting));
            return reply.Serialize(false);
        }

        reply.Set("path", JsonValue.Of(file.Path));
        var header = Header(file.BaseSave);
        if (header != null) {
            reply.Set("base", header);
        }
        reply.Set("variant", JsonValue.Of(file.Variant ?? ""));
        reply.Set("editing", JsonValue.Of(PracticeEditor.Active));
        Current(file, reply);
        return reply.Serialize(false);
    }

    // for the page's help; a folder setting the path API refuses is shown as written
    private static string FolderPath() {
        var folder = PracticeSelect.Folder;
        try {
            return Path.GetFullPath(folder);
        } catch (Exception) {
            return folder;
        }
    }

    // the segment and its variants as the page edits them, and the revision a save must name
    private static void Current(BfrpFile file, JsonValue reply) {
        var segment = WithBoxes(file, "");
        var variants = JsonValue.NewObject();
        foreach (var id in file.Variants) {
            variants.Set(id, WithBoxes(file, id));
        }

        // FNV-1a of exactly what the page is shown
        var hash = 14695981039346656037UL;
        foreach (var b in Encoding.UTF8.GetBytes(segment.Serialize(false) + variants.Serialize(false))) {
            hash = (hash ^ b) * 1099511628211UL;
        }

        reply.Set("segment", segment);
        reply.Set("variants", variants);
        reply.Set("rev", JsonValue.Of(hash.ToString("x16")));
    }

    // The page's shape: boxes as an array, tombstones too, the first goal's corners as end.box and the
    // rest of it as end.goal, on a shallow copy so none of it lands in the file's own json.
    private static JsonValue WithBoxes(BfrpFile file, string variant) {
        var json = variant == "" ? file.Segment : file.VariantSegment(variant);
        var copy = Copy(json, "boxes", "end");
        var end = Copy(json["end"], "box", "goal");
        var list = JsonValue.NewArray();
        var boxes = file.Boxes(variant);
        for (var i = 0; i < boxes.Count; i++) {
            var box = boxes[i].ToJson();
            if (variant == "" && boxes[i].Goal && !boxes[i].Deleted && !end["box"].IsArray) {
                end.Set("box", box["box"]);
                var goal = Copy(box, "box");
                // its place among the box lines, which TakeBoxes puts it back in
                goal.Set("at", JsonValue.Of(i));
                end.Set("goal", goal);
            } else {
                list.Add(box);
            }
        }

        if (variant == "") {
            copy.Set("end", end);
        }

        copy.Set("boxes", list);
        return copy;
    }

    private static JsonValue Copy(JsonValue json, params string[] except) {
        var copy = JsonValue.NewObject();
        if (!json.IsObject) {
            return copy;
        }

        foreach (var key in json.Keys) {
            if (Array.IndexOf(except, key) < 0) {
                copy.Set(key, json[key]);
            }
        }

        return copy;
    }

    // the page's boxes as lines, end.goal back in its place at end.box's corners
    private static List<RandomizerBox> TakeBoxes(JsonValue json, bool goal) {
        var boxes = new List<RandomizerBox>();
        var list = json["boxes"];
        for (var i = 0; i < list.Count; i++) {
            boxes.Add(RandomizerBox.FromJson(list[i]));
        }

        var corners = json["end"]["box"];
        if (goal && corners.IsArray && corners.Count == 4) {
            var line = Copy(json["end"]["goal"]);
            if (!line["type"].IsString) {
                line.Set("type", JsonValue.Of("goal"));
            }

            line.Set("box", corners);
            var box = RandomizerBox.FromJson(line);
            if (!box.Goal || box.Deleted) {
                throw new Exception("end.goal is not a goal box: " + box.Line);
            }

            // a goal the page added goes last, so no other box's number moves
            var at = line["at"];
            boxes.Insert(at.IsNumber ? (int)Math.Max(0, Math.Min(at.Num, boxes.Count)) : boxes.Count, box);
        }

        return boxes;
    }

    // the page's copy replaces the file's, is saved, and the live session re-parses it
    private static string WriteSegment(string text, ref bool stale) {
        var file = PracticeController.File;
        if (file == null) {
            throw new Exception("no practice session is running");
        }

        var incoming = JsonValue.Parse(text);
        // a copy made before the game changed the segment gets the current one back, to merge
        var now = JsonValue.NewObject();
        Current(file, now);
        if (incoming["rev"].IsString && incoming["rev"].Str != now["rev"].Str) {
            stale = true;
            now.Set("error", JsonValue.Of("the game changed this segment since the page loaded it"));
            return now.Serialize(false);
        }

        var segment = incoming["segment"];
        if (!segment.IsObject) {
            throw new Exception("segment must be an object");
        }

        // all of it is built and checked before the file changes, so a refusal leaves the file whole
        var boxes = TakeBoxes(segment, true);
        var stored = Copy(segment, "boxes", "end");
        stored.Set("end", Copy(segment["end"], "box", "goal"));
        for (var g = 0; g < stored["shuffle"].Count; g++) {
            CheckPickups(stored["shuffle"][g]["give"]);
        }

        var ids = new List<string>();
        var variantBoxes = new List<List<RandomizerBox>>();
        var variantJson = new List<JsonValue>();
        var variants = incoming["variants"];
        foreach (var id in variants.Keys) {
            if (variants[id].IsObject) {
                ids.Add(id);
                variantBoxes.Add(TakeBoxes(variants[id], false));
                variantJson.Add(Copy(variants[id], "boxes"));
                CheckPickups(variants[id]["inventory"]);
            }
        }

        file.SetBoxes("", boxes);
        file.Segment = stored;
        for (var i = 0; i < ids.Count; i++) {
            file.SetBoxes(ids[i], variantBoxes[i]);
            file.SetVariantSegment(ids[i], variantJson[i]);
        }

        file.Save();
        PracticeController.Reparse();
        Randomizer.log("practice: segment saved from the editor page");
        return "{\"ok\":true}";
    }

    // a code the parse would log and skip is the page's to fix, so it is refused here
    private static void CheckPickups(JsonValue codes) {
        for (var i = 0; i < codes.Count; i++) {
            if (codes[i].IsString && !PracticeSegment.IsPickup(codes[i].Str)) {
                throw new Exception("'" + codes[i].Str + "' is not a pickup");
            }
        }
    }

    private static string DeleteVariant(string id) {
        var file = PracticeController.File;
        if (file == null) {
            throw new Exception("no practice session is running");
        }

        if (id == file.Variant) {
            throw new Exception("this variant is the one running; exit the session first");
        }

        if (!file.Variants.Contains(id)) {
            throw new Exception("there is no variant " + id);
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

        if (file.GetGhost(file.Variant, slot) == null) {
            throw new Exception("there is no ghost " + slot);
        }

        file.RemoveGhost(file.Variant, slot);
        file.Save();
        Randomizer.log("practice: ghost " + slot + " removed from the editor page");
        return "{\"ok\":true}";
    }

    // --- the game's saves ------------------------------------------------------------

    // the fifty save slots as the file select shows them, for picking a segment's start
    private static string Saves() {
        var reply = JsonValue.NewObject();
        var list = JsonValue.NewArray();
        var saves = GameController.Instance.SaveGameController;
        for (var slot = 0; slot < 50; slot++) {
            if (!saves.SaveExists(slot)) {
                continue;
            }

            // the header only, as the file select reads it: this runs on the main thread
            JsonValue entry;
            try {
                using (var reader = new BinaryReader(File.Open(saves.GetSaveFilePath(slot), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
                    entry = Header(reader);
                }
            } catch (Exception) {
                continue;
            }

            if (entry != null) {
                entry.Set("slot", JsonValue.Of(slot));
                list.Add(entry);
            }
        }

        reply.Set("saves", list);
        return reply.Serialize(false);
    }

    // what a save's header says about it; null for bytes that are not a save
    private static JsonValue Header(byte[] bytes) {
        if (bytes == null) {
            return null;
        }

        using (var reader = new BinaryReader(new MemoryStream(bytes))) {
            return Header(reader);
        }
    }

    private static JsonValue Header(BinaryReader reader) {
        try {
            var info = new SaveSlotInfo();
            if (!info.LoadFromReader(reader)) {
                return null;
            }

            var shots = SaveSlotsScreenshotManager.Instance;
            var entry = JsonValue.NewObject();
            entry.Set("area", JsonValue.Of(shots != null ? shots.FindAreaName(info.AreaName) : info.AreaName));
            entry.Set("completion", JsonValue.Of(info.Completion));
            entry.Set("health", JsonValue.Of(info.Health));
            entry.Set("maxHealth", JsonValue.Of(info.MaxHealth));
            entry.Set("energy", JsonValue.Of(info.Energy));
            entry.Set("maxEnergy", JsonValue.Of(info.MaxEnergy));
            entry.Set("seconds", JsonValue.Of(info.TotalSeconds));
            entry.Set("difficulty", JsonValue.Of(info.Difficulty.ToString()));
            return entry;
        } catch (Exception) {
            return null;
        }
    }

    private static byte[] SaveBytes(JsonValue incoming) {
        var slot = incoming["slot"].IsNumber ? (int)incoming["slot"].Num : -1;
        var saves = GameController.Instance.SaveGameController;
        if (slot < 0 || slot >= 50 || !saves.SaveExists(slot)) {
            throw new Exception("there is no save in slot " + (slot + 1));
        }

        return File.ReadAllBytes(saves.GetSaveFilePath(slot));
    }

    // a new container around one of the game's saves; from the chooser it starts at once
    private static string CreateSegment(string text) {
        var incoming = JsonValue.Parse(text);
        var save = SaveBytes(incoming);
        var name = incoming["name"].IsString ? incoming["name"].Str.Trim() : "";
        var path = PracticeEditor.CreateFrom(save, name);
        var started = PracticeSelect.StartPath(path);
        var reply = JsonValue.NewObject();
        reply.Set("ok", JsonValue.Of(true));
        reply.Set("path", JsonValue.Of(path));
        reply.Set("started", JsonValue.Of(started));
        return reply.Serialize(false);
    }

    // the running segment starts from another of the game's saves, and the attempt restarts on it
    private static string ReplaceSave(string text) {
        var file = PracticeController.File;
        if (file == null) {
            throw new Exception("no practice session is running");
        }

        file.SetBaseSave(SaveBytes(JsonValue.Parse(text)));
        file.Save();
        // parked behind the title the slot holds the run the player left: no restart, the new save waits
        var game = GameController.Instance;
        var parked = game == null || game.GameInTitleScreen;
        if (!parked) {
            PracticeController.Restart();
        }

        Randomizer.log("practice: starting save replaced from the editor page");
        return parked ? "{\"ok\":true,\"restarted\":false}" : "{\"ok\":true,\"restarted\":true}";
    }

    // --- the catalog -----------------------------------------------------------------

    // what the pickers offer: skills and world events by name, every location by zone with what it can be picked for
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
            entry.Set("ends", JsonValue.Of(PracticeSegment.CanEnd(location.Key)));
            entry.Set("shuffles", JsonValue.Of(!PracticeSegment.NeverGiven(location.Key)));
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

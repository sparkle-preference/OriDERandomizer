// A fake sidecar and socket bound into NativeWebSocket's private delegates, plus a full reset of the
// netcode statics so every example starts from the state the game had before any seed loaded.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

public static class FakeNet {
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;

    public static int State = 3;
    public static bool HttpOn = true;
    public static int BeginFails;
    public static readonly Queue<string> Frames = new Queue<string>();
    public static readonly List<string> Sent = new List<string>();
    // "METHOD url" or "METHOD url|body", in order; handle n is Begun[n - 1]
    public static readonly List<string> Begun = new List<string>();
    public static readonly Dictionary<int, int> Status = new Dictionary<int, int>();
    public static readonly Dictionary<int, string> Bodies = new Dictionary<int, string>();
    public static readonly HashSet<int> Released = new HashSet<int>();
    public static int Opens, Errors, Stops, Starts, SentSeen, BegunSeen;
    static IntPtr buffer = Marshal.AllocHGlobal(1 << 20);
    static Delegate begin;

    public static void Reset() {
        State = 3;
        HttpOn = true;
        BeginFails = 0;
        Frames.Clear();
        Sent.Clear();
        Begun.Clear();
        Status.Clear();
        Bodies.Clear();
        Released.Clear();
        Opens = Errors = Stops = Starts = SentSeen = BegunSeen = 0;
        Bind();
    }

    static IntPtr Copy(string text, out int length) {
        var bytes = Encoding.UTF8.GetBytes(text);
        Marshal.Copy(bytes, 0, buffer, bytes.Length);
        length = bytes.Length;
        return buffer;
    }

    static IntPtr GetPending(out int length) {
        if (Frames.Count == 0) {
            length = 0;
            return IntPtr.Zero;
        }

        return Copy(Frames.Peek(), out length);
    }

    static void PopPending() {
        if (Frames.Count > 0) {
            Frames.Dequeue();
        }
    }

    static void SendText(string data) { Sent.Add(data); }
    static int GetState() { return State; }
    static int GetOpens() { return Opens; }
    static int GetErrors() { return Errors; }
    static void Stop() { Stops++; }
    static void Start() { Starts++; }
    static void Ignore() { }
    static void IgnoreStr(string s) { }
    static void IgnoreInt(int v) { }
    static void IgnoreByte(byte v) { }

    static IntPtr LastError(out int length) {
        length = 0;
        return IntPtr.Zero;
    }

    static int HttpBegin(string method, string url, string ca, string body, string type) {
        if (BeginFails > 0) {
            BeginFails--;
            return 0;
        }

        Begun.Add(method + " " + url + (body == "" ? "" : "|" + body));
        return Begun.Count;
    }

    static int HttpStatus(int handle) {
        int s;
        return Status.TryGetValue(handle, out s) ? s : NativeWebSocket.HttpPending;
    }

    static IntPtr HttpResponse(int handle, out int length) {
        string body;
        if (!Bodies.TryGetValue(handle, out body)) {
            length = 0;
            return IntPtr.Zero;
        }

        return Copy(body, out length);
    }

    static void HttpRelease(int handle) { Released.Add(handle); }

    static void Set(string field, string method) {
        var f = typeof(NativeWebSocket).GetField(field, Hidden);
        var m = typeof(FakeNet).GetMethod(method, Hidden);
        f.SetValue(null, Delegate.CreateDelegate(f.FieldType, m));
    }

    static void Bind() {
        Set("get_pending_message", "GetPending");
        Set("pop_pending_message", "PopPending");
        Set("send_text", "SendText");
        Set("get_state", "GetState");
        Set("get_open_count", "GetOpens");
        Set("get_error_count", "GetErrors");
        Set("get_last_error", "LastError");
        Set("stop_fn", "Stop");
        Set("start_fn", "Start");
        Set("set_url", "IgnoreStr");
        Set("set_ca_file", "IgnoreStr");
        Set("set_ping_interval", "IgnoreInt");
        Set("set_auto_reconnect", "IgnoreByte");
        Set("http_status", "HttpStatus");
        Set("http_response", "HttpResponse");
        Set("http_release", "HttpRelease");
        Set("http_begin", "HttpBegin");
        begin = (Delegate)typeof(NativeWebSocket).GetField("http_begin", Hidden).GetValue(null);
        typeof(NativeWebSocket).GetProperty("Loaded").GetSetMethod(true).Invoke(null, new object[] { true });
    }

    // the sidecar's async http entry points exist only in newer builds
    public static void SetHttp(bool on) {
        HttpOn = on;
        typeof(NativeWebSocket).GetField("http_begin", Hidden).SetValue(null, on ? begin : null);
    }

    public static void Push(string frame) { Frames.Enqueue(frame); }

    public static void Finish(int handle, int status, string body) {
        Status[handle] = status;
        if (body != null) {
            Bodies[handle] = body;
        }
    }

    public static List<int> Pending() {
        var open = new List<int>();
        for (var h = 1; h <= Begun.Count; h++) {
            if (!Status.ContainsKey(h)) {
                open.Add(h);
            }
        }

        return open;
    }
}

public static class NetGlue {
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
    static readonly Type[] Owned = {
        typeof(RandomizerSyncManager), typeof(RandomizerMW), typeof(RandomizerDeathLink),
        typeof(RandomizerTrackedDataManager), typeof(BingoController),
        typeof(Randomizer), typeof(RandomizerSwitch), typeof(RandomizerBonus), typeof(RandomizerStatsManager),
        typeof(RandomizerChaosManager), typeof(RandomizerGhostSignal), typeof(RandomizerLocationManager),
        typeof(RandomizerBonusSkill), typeof(TeleporterController), typeof(UnityEngine.Time),
    };

    static Dictionary<FieldInfo, object> snapshot;
    static HashSet<FieldInfo> stateCollections;

    // values as first loaded; an empty collection is state (cleared on reset), a full one is data (kept)
    static void Snap() {
        snapshot = new Dictionary<FieldInfo, object>();
        stateCollections = new HashSet<FieldInfo>();
        foreach (var t in Owned) {
            foreach (var f in t.GetFields(All)) {
                if (f.IsLiteral || typeof(Delegate).IsAssignableFrom(f.FieldType)) {
                    continue;
                }

                var v = f.GetValue(null);
                snapshot[f] = v;
                var count = v == null || v is Array ? null : v.GetType().GetProperty("Count");
                if (count != null && (int)count.GetValue(v, null) == 0 && v.GetType().GetMethod("Clear", Type.EmptyTypes) != null) {
                    stateCollections.Add(f);
                }
            }
        }
    }

    public static void Reset() {
        if (snapshot == null) {
            Snap();
        }

        foreach (var kv in snapshot) {
            var f = kv.Key;
            var v = kv.Value;
            if (v == null || v is string || f.FieldType.IsValueType) {
                f.SetValue(null, v);
            } else if (stateCollections.Contains(f)) {
                var now = f.GetValue(null);
                if (now == null || !ReferenceEquals(now, v)) {
                    f.SetValue(null, v);
                }

                v.GetType().GetMethod("Clear", Type.EmptyTypes).Invoke(v, null);
            }
        }

        RandomizerSettings.DevSettings = new RandomizerSettings.Dev_();
        Randomizer.Keysanity = new KeysanityStub();
        TeleporterController.Instance = new TeleporterController();
        Characters.Sein = new SeinStub();
        UnityEngine.Time.realtimeSinceStartup = 1000f;
        UnityEngine.Time.deltaTime = 0.02f;
        FakeNet.Reset();
    }

    // a sync seed just loaded: Reset, then what the seed parse and Initialize set
    public static void Fresh(string version, int syncMode, bool ws, bool http, Dictionary<string, string> table) {
        Reset();
        Randomizer.VERSION = version;
        Randomizer.SyncId = "7.2";
        Randomizer.Sync = true;
        Randomizer.SyncMode = syncMode;
        FakeNet.State = ws ? 1 : 3;
        FakeNet.SetHttp(http);
        SetTeleportTable(table);
        RandomizerSyncManager.SeedHead = new[] { "Sync7.2,mode=shared|seed" };
        RandomizerSyncManager.Initialize();
        LocalEvents();
    }

    public static void SetItem(int id, int value) {
        Characters.Sein.Inventory.SetRandomizerItem(id, value);
    }

    public static void SetTeleportTable(Dictionary<string, string> table) {
        Randomizer.TeleportTable = new Hashtable();
        foreach (var kv in table) {
            Randomizer.TeleportTable[kv.Key] = kv.Value;
        }

        foreach (var id in table.Values) {
            TeleporterController.Instance.Teleporters.Add(new TeleporterStub { Identifier = id });
        }
    }

    // Initialize's event lines with checkers reading Sein's inventory instead of the world
    public static void LocalEvents() {
        foreach (var e in RandomizerSyncManager.EventInfos) {
            var id = e.id;
            e.checker = () => Characters.Sein.Inventory.GetRandomizerItem(EventItem + id) != 0;
        }
    }

    public const int EventItem = 90000;

    public static void Hold(string code, int id) {
        if (code == "SK") {
            var line = RandomizerSyncManager.SkillInfos.First(s => s.id == id);
            Characters.Sein.PlayerAbilities.Held.Add(line.skill);
        } else if (code == "EV") {
            Characters.Sein.Inventory.SetRandomizerItem(EventItem + id, 1);
        }
    }

    // what GivePickup does to the state the tick's checks read
    public static void Apply(RandomizerAction action) {
        var value = action.ValAsStr();
        if (action.Action == "SK") {
            var line = RandomizerSyncManager.SkillInfos.FirstOrDefault(s => s.id.ToString() == value);
            if (line != null) {
                Characters.Sein.PlayerAbilities.Held.Add(line.skill);
            }
        } else if (action.Action == "EV") {
            Characters.Sein.Inventory.SetRandomizerItem(EventItem + int.Parse(value), 1);
        } else if (action.Action == "TP" && Randomizer.TeleportTable.ContainsKey(value)) {
            HoldTeleporter(value);
        } else if (action.Action == "TW") {
            TeleporterController.CustomWarps.Add(value.Split(',')[0]);
        }
    }

    public static void HoldTeleporter(string name) {
        var id = Randomizer.TeleportTable[name].ToString();
        TeleporterController.Instance.Teleporters.First(t => t.Identifier == id).Activated = true;
    }

    // {"skills"|"events"|"tps": {id: bit}} as Initialize built them
    public static Dictionary<string, object> Lines() {
        return new Dictionary<string, object> {
            { "skills", RandomizerSyncManager.SkillInfos.ToDictionary(s => s.id.ToString(), s => (object)s.bit) },
            { "events", RandomizerSyncManager.EventInfos.ToDictionary(e => e.id.ToString(), e => (object)e.bit) },
            { "tps", RandomizerSyncManager.TeleportInfos.ToDictionary(t => t.id, t => (object)t.bit) },
        };
    }

    public static void AddManifest(int slot, string code, string id) {
        RandomizerMW.AddManifestEntry(-slot - 2, "1,," + code + "," + id, "Glades");
    }

    public static void Enqueue(string type, string id, int coords) {
        RandomizerSyncManager.PickupQueue.Enqueue(new RandomizerSyncManager.Pickup(type, id, coords));
    }

    public static List<string> Queue() {
        return RandomizerSyncManager.PickupQueue.Select(p => p.type + "|" + p.id + "@" + p.coords).ToList();
    }

    public static string Sending() {
        var p = RandomizerSyncManager.SendingPickup;
        return p == null ? null : p.type + "|" + p.id + "@" + p.coords;
    }

    public static void Advance(float seconds) {
        UnityEngine.Time.realtimeSinceStartup += seconds;
        UnityEngine.Time.deltaTime = seconds;
    }

    static object Private(string name) {
        return typeof(RandomizerSyncManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
    }

    // one frame of Update after dt seconds, then what it sent and where the found queue stands
    public static Dictionary<string, object> Step(float dt) {
        Advance(dt);
        RandomizerSyncManager.Update();
        return Look();
    }

    public static Dictionary<string, object> Look() {
        var begun = new List<object>();
        for (var h = FakeNet.BegunSeen + 1; h <= FakeNet.Begun.Count; h++) {
            begun.Add(new List<object> { h, FakeNet.Begun[h - 1] });
        }

        var d = new Dictionary<string, object> {
            { "sent", FakeNet.Sent.Skip(FakeNet.SentSeen).ToList() },
            { "begun", begun },
            { "queue", Queue() },
            { "sending", Sending() },
            { "refused", RandomizerSyncManager.Refused },
            { "token", Private("wsFoundToken") },
            { "handle", Private("foundHandle") },
            { "tickHandle", Private("tickHandle") },
            { "unsupported", Private("wsFoundUnsupported") },
            { "nohttp", Private("wsNoHttp") },
            { "calls", RandomizerBonus.Calls.ToList() },
            { "errors", Randomizer.Errors.ToList() },
        };
        FakeNet.SentSeen = FakeNet.Sent.Count;
        FakeNet.BegunSeen = FakeNet.Begun.Count;
        return d;
    }

    // {"bool"|"int": {goal: item}, "multi": {goal: {subgoal: [item, bit]}}}; bit -1 is a whole item
    public static Dictionary<string, object> BingoItems() {
        var multi = new Dictionary<string, object>();
        foreach (var g in BingoController.MultiBoolGoals.Values) {
            multi[g.Name] = g.Subgoals.ToDictionary(kv => kv.Key, kv => (object)new List<object> {
                kv.Value.ItemId, kv.Value is BingoController.BitfieldBoolGoal ? ((BingoController.BitfieldBoolGoal)kv.Value).Bit : -1 });
        }

        return new Dictionary<string, object> {
            { "bool", BingoController.BoolGoals.ToDictionary(kv => kv.Key, kv => (object)kv.Value.ItemId) },
            { "int", BingoController.IntGoals.ToDictionary(kv => kv.Key, kv => (object)kv.Value.ItemId) },
            { "multi", multi },
        };
    }

    public static Dictionary<string, object> ActiveGoals() {
        return new Dictionary<string, object> {
            { "singles", BingoController.ActiveSingleGoals.ToList() },
            { "multis", BingoController.ActiveMultiGoals.ToDictionary(kv => kv.Key, kv => (object)kv.Value.ToList()) },
            { "targets", BingoController.IntGoals.ToDictionary(kv => kv.Key, kv => (object)kv.Value.Target) },
        };
    }

    static int Inv(int id) {
        return Characters.Sein.Inventory.GetRandomizerItem(id);
    }

    static string Fingerprint() {
        var words = Enumerable.Range(940, 9).Select(i => Inv(i).ToString()).ToArray();
        return RandomizerSwitch.Given.Count + "/" + RandomizerBonus.Calls.Count + "/" + string.Join(",", words) + "/" + Inv(1001) + "/" + Inv(1002);
    }

    // ticks with this body until two in a row change nothing (one quiet tick can still release a batch); -1 if it never settles
    public static int Settle(string body, int max) {
        var quiet = 0;
        for (var i = 1; i <= max; i++) {
            var before = Fingerprint();
            RandomizerSyncManager.ProcessTickResponse(body);
            quiet = Fingerprint() == before ? quiet + 1 : 0;
            if (quiet == 2) {
                return i;
            }
        }

        return -1;
    }

    public static Dictionary<string, object> State() {
        var inv = new Dictionary<string, object>();
        foreach (var kv in Characters.Sein.Inventory.Items) {
            inv[kv.Key.ToString()] = kv.Value;
        }

        return new Dictionary<string, object> {
            { "given", RandomizerSwitch.Given.ToList() },
            { "calls", RandomizerBonus.Calls.ToList() },
            { "counts", RandomizerBonus.Counts.ToDictionary(kv => kv.Key.ToString(), kv => (object)kv.Value) },
            { "inv", inv },
            { "names", RandomizerMW.PlayerNames.ToDictionary(kv => kv.Key.ToString(), kv => (object)kv.Value) },
            { "hints", RandomizerMW.ApHints.ToDictionary(kv => kv.Key.ToString(), kv => (object)kv.Value) },
            { "released", RandomizerMW.Released },
            { "sent", FakeNet.Sent.ToList() },
            { "begun", FakeNet.Begun.ToList() },
            { "errors", Randomizer.Errors.ToList() },
            { "infos", Randomizer.Infos.ToList() },
            { "wins", Randomizer.Wins.ToList() },
            { "trees", RandomizerStatsManager.Trees.ToList() },
            { "relics", RandomizerStatsManager.Relics.ToList() },
        };
    }
}

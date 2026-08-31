using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Game;
using Sein.World;
using UnityEngine;
using Events = Sein.World.Events;

public static class RandomizerSyncManager {
    public static void Initialize() {
        tslu = 0;
        Answered = Time.realtimeSinceStartup;
        Refused = false;
        Warned = false;
        webClient = new WebClient();
        webClient.DownloadStringCompleted += RetryOnFail;
        getClient = new WebClient();
        getClient.UploadValuesCompleted += CheckPickups;
        if (CurrentSignals == null) {
            CurrentSignals = new HashSet<String>();
        }

        if (PickupQueue == null) {
            PickupQueue = new Queue<Pickup>();
        }

        SeedSent = false;
        SkillInfos = new List<SkillInfoLine>();
        EventInfos = new List<EventInfoLine>();
        TeleportInfos = new List<TeleportInfoLine>();
        TeleportInfos.Add(new TeleportInfoLine("Grove", 0));
        TeleportInfos.Add(new TeleportInfoLine("Swamp", 1));
        TeleportInfos.Add(new TeleportInfoLine("Grotto", 2));
        TeleportInfos.Add(new TeleportInfoLine("Valley", 3));
        TeleportInfos.Add(new TeleportInfoLine("Forlorn", 4));
        TeleportInfos.Add(new TeleportInfoLine("Sorrow", 5));
        TeleportInfos.Add(new TeleportInfoLine("Ginso", 6));
        TeleportInfos.Add(new TeleportInfoLine("Horu", 7));
        TeleportInfos.Add(new TeleportInfoLine("Blackroot", 8));
        TeleportInfos.Add(new TeleportInfoLine("Glades", 9));
        SkillInfos.Add(new SkillInfoLine(0, 0, AbilityType.Bash));
        SkillInfos.Add(new SkillInfoLine(2, 1, AbilityType.ChargeFlame));
        SkillInfos.Add(new SkillInfoLine(3, 2, AbilityType.WallJump));
        SkillInfos.Add(new SkillInfoLine(4, 3, AbilityType.Stomp));
        SkillInfos.Add(new SkillInfoLine(5, 4, AbilityType.DoubleJump));
        SkillInfos.Add(new SkillInfoLine(8, 5, AbilityType.ChargeJump));
        SkillInfos.Add(new SkillInfoLine(12, 6, AbilityType.Climb));
        SkillInfos.Add(new SkillInfoLine(14, 7, AbilityType.Glide));
        SkillInfos.Add(new SkillInfoLine(50, 8, AbilityType.Dash));
        SkillInfos.Add(new SkillInfoLine(51, 9, AbilityType.Grenade));
        EventInfos.Add(new EventInfoLine(0, 0, () => Keys.GinsoTree));
        EventInfos.Add(new EventInfoLine(1, 1, () => Events.WaterPurified));
        EventInfos.Add(new EventInfoLine(2, 2, () => Keys.ForlornRuins));
        EventInfos.Add(new EventInfoLine(3, 3, () => Events.WindRestored));
        EventInfos.Add(new EventInfoLine(4, 4, () => Keys.MountHoru));
        if (Randomizer.SyncId != "") {
            var parts = Randomizer.SyncId.Split('.');
            RootUrl = $"{WebBase()}/netcode/game/{parts[0]}/player/{parts[1]}";
            // every websocket path is armored: a broken merge, missing
            // setting, or native failure must never break seed loading
            var wsBase = WsBase();
            if (wsBase == null) {
                Randomizer.log("ws diag: WsEndpoint setting missing (RandomizerSettings not fully merged?); websocket off");
            } else {
                var url = $"{wsBase}/netcode/game/{parts[0]}/player/{parts[1]}/ws";
                wsUrl = url;
                // alt+L doubles as the user's "retry the websocket" button:
                // a socket written off earlier this session gets a fresh
                // shot on every seed reload
                wsDead = false;
                wsLoadAttempts = 0;
                wsFoundUnsupported = false;
                wsNoHttp = false; // the server re-sends nohttp on connect
                wsWasOpen = false;
                DropSidecarHandles();
                StartWebsocket(url);
            }
        }
    }

    // Idempotent: alt+L re-runs Initialize, but the socket only restarts
    // when the game/player id actually changed. Reconnects within a game
    // are the native side's job (auto-reconnect with backoff); *load*
    // failures are retried from Update — a freshly extracted dll can lose
    // a race with the AV scanner and be loadable moments later.
    public static void StartWebsocket(string url) {
        try {
            wsNextTry = Time.realtimeSinceStartup + 3f;
            var disable = RandomizerSettings.DevSettings.DisableWebsocket;
            if (disable != null && disable.Value) {
                wsDead = true;
                return;
            }

            if (disable == null) {
                Randomizer.log("ws diag: DisableWebsocket setting missing (RandomizerSettings not fully merged?); continuing");
            }

            if (wsStartedUrl == url) {
                return;
            }

            if (!NativeWebSocket.Load()) {
                wsLoadAttempts++;
                if (wsLoadAttempts >= 3) {
                    wsDead = true;
                    Randomizer.log("ws diag: native load failed 3 times; using http this session");
                }

                return;
            }

            wsLoadAttempts = 0;
            if (wsStartedUrl != null) {
                NativeWebSocket.Stop();
            }

            wsDead = false;
            if (NativeWebSocket.CaPath != null) {
                NativeWebSocket.SetCaFile(NativeWebSocket.CaPath);
            }

            NativeWebSocket.SetUrl(url);
            NativeWebSocket.SetPingInterval(30);
            NativeWebSocket.SetAutoReconnect(true);
            NativeWebSocket.Start();
            wsStartedUrl = url;
            Randomizer.log($"ws diag: socket started for {url} (ca: {NativeWebSocket.CaPath ?? "none"})");
        } catch (Exception e) {
            // file-only: LogError renders on-screen and itself NREs during
            // early seed parse (no UI yet) — that cascade broke seed loading
            Randomizer.log($"StartWebsocket: {e}");
            wsDead = true;
        }
    }

    public static bool WsOpen => !wsDead && NativeWebSocket.Loaded && wsStartedUrl != null && NativeWebSocket.GetState() == NativeWebSocket.SocketState.Open;

    public static void Update() {
        try {
            // pickups wait in the queue until a transport exists (with the
            // http fallback gone, that means until the socket reconnects)
            if (SendingPickup == null && PickupQueue.Count > 0 && !webClient.IsBusy
                && ((WsOpen && !wsFoundUnsupported) || HttpLaneOpen)) {
                SendingPickup = PickupQueue.Dequeue();
                wsFoundAttempts = 0;
                if (WsOpen && !wsFoundUnsupported) {
                    SendFoundWs();
                } else {
                    wsFoundToken = 0;
                    SendFoundHttp();
                }
            }
            // a ws-sent pickup whose ack never came retries — via http if
            // that's still allowed, else over the socket. The server dedups
            // replayed pickups, so double delivery is as safe as a retry
            else if (SendingPickup != null && wsFoundToken != 0
                && Time.realtimeSinceStartup - wsFoundSentAt > 5f) {
                FoundFallback();
            } else if (Randomizer.SyncId != "" && !SeedSent) {
                UploadSeed();
            }

            ChaosTimeoutCounter--;
            if (ChaosTimeoutCounter < 0) {
                RandomizerChaosManager.ClearEffects();
                ChaosTimeoutCounter = 216000;
            }

            // retry a failed native load every few seconds (max 3 tries)
            if (!wsDead && wsUrl != null && wsStartedUrl == null && Time.realtimeSinceStartup >= wsNextTry) {
                StartWebsocket(wsUrl);
            }

            // unacked complete: keeps retrying (multiworld releases ride on it)
            if (completePending && Time.realtimeSinceStartup >= completeNextAt) {
                TrySendComplete();
            }

            // websocket frames are drained every frame, not at tick
            // cadence — pushed signals should land with frame latency
            CheckWebsocketHealth();
            if (WsOpen) {
                string frame;
                while ((frame = NativeWebSocket.GetPendingMessage()) != null) {
                    ProcessFrame(frame);
                }
            }

            if (WsOpen && !wsWasOpen && BingoController.Active) {
                NativeWebSocket.SendText("goals:");
            }

            if (WsOpen != wsWasOpen) {
                RandomizerGhostSignal.Apply();
            }

            wsWasOpen = WsOpen;
            Silent();
            PumpSidecar();

            tslu += Time.deltaTime;
            if (tslu < PERIOD) {
                return;
            }

            if (WsOpen) {
                tslu = 0f;
                NativeWebSocket.SendText("tick:" + TickPayload());
            } else if (!wsNoHttp && UseSidecarHttp) {
                if (tickHandle == 0) {
                    tslu = 0f;
                    tickHandle = NativeWebSocket.HttpBegin("POST", RootUrl + "/tick/", TickPayload(), FormType);
                }
            } else if (!wsNoHttp && !SecureNetcode && !getClient.IsBusy) {
                tslu = 0f;
                var nvc = new NameValueCollection();
                var pos = Characters.Sein.Position;
                nvc["x"] = pos.x.ToString();
                nvc["y"] = pos.y.ToString();
                nvc["version"] = Randomizer.VERSION;
                for (var i = 0; i < 8; i++) {
                    nvc["seen_" + i] = fixInt(Characters.Sein.Inventory.GetRandomizerItem(1560 + i));
                    nvc["have_" + i] = fixInt(Characters.Sein.Inventory.GetRandomizerItem(930 + i));
                }

                var apHints = RandomizerMW.HintRequestField();
                if (apHints != null) {
                    nvc["aph"] = apHints;
                }

                var deaths = RandomizerDeathLink.Field();
                if (deaths != null) {
                    nvc["dl"] = deaths;
                }

                var uri = new Uri(RootUrl + "/tick/");
                getClient.UploadValuesAsync(uri, nvc);
            }
        } catch (Exception e) {
            Randomizer.LogError("RSM.Update: " + e.Message);
        }
    }

    // schemes ride the settings since 4.3; a bare host is a half-merged build
    public static string WebBase() {
        var v = RandomizerSettings.DevSettings.WebEndpoint.Value.TrimEnd('/');
        return v.Contains("://") ? v : "http://" + v;
    }

    public static string WsBase() {
        var setting = RandomizerSettings.DevSettings.WsEndpoint;
        if (setting == null) {
            return null;
        }

        var v = setting.Value.TrimEnd('/');
        return v.Contains("://") ? v : "wss://" + v;
    }

    // https means the game's own WebClient cannot speak it: those requests go
    // through the sidecar, or nowhere
    public static bool SecureNetcode => RootUrl != null && RootUrl.StartsWith("https://");
    public static bool UseSidecarHttp => SecureNetcode && NativeWebSocket.AsyncHttpAvailable;

    // an http lane needs the server to still serve those routes AND a client
    // that can speak the scheme: https goes by sidecar, http:// by WebClient
    public static bool HttpLaneOpen => !wsNoHttp && (UseSidecarHttp || !SecureNetcode);
    public const string FormType = "application/x-www-form-urlencoded";

    private static int tickHandle;
    private static int foundHandle;
    private static readonly List<int> sidecarForget = new List<int>();

    // alt+L moves RootUrl; a stale reply must not land in the new game
    private static void DropSidecarHandles() {
        SidecarForget(tickHandle);
        SidecarForget(foundHandle);
        tickHandle = 0;
        foundHandle = 0;
    }

    public static void SidecarForget(int handle) {
        if (handle != 0) {
            sidecarForget.Add(handle);
        }
    }

    // per-frame: retire fire-and-forget handles, land tick and found replies
    private static void PumpSidecar() {
        for (var i = sidecarForget.Count - 1; i >= 0; i--) {
            if (NativeWebSocket.HttpStatus(sidecarForget[i]) != NativeWebSocket.HttpPending) {
                NativeWebSocket.HttpRelease(sidecarForget[i]);
                sidecarForget.RemoveAt(i);
            }
        }

        if (tickHandle != 0) {
            var status = NativeWebSocket.HttpStatus(tickHandle);
            if (status != NativeWebSocket.HttpPending) {
                var body = status == 200 ? NativeWebSocket.HttpResponse(tickHandle) : null;
                NativeWebSocket.HttpRelease(tickHandle);
                tickHandle = 0;
                if (body != null && Characters.Sein) {
                    ProcessTickResponse(body);
                } else if (status == 412) {
                    // same guidance the WebClient tick path shows on 412
                    if (Randomizer.SyncMode == 1 || Randomizer.SyncMode == 5) {
                        Randomizer.printInfo("Co-op server error, try reloading the seed (Alt+L)");
                    } else {
                        Randomizer.LogError("Co-op server error, try reloading the seed (Alt+L)");
                    }
                }
            }
        }

        if (foundHandle != 0) {
            var status = NativeWebSocket.HttpStatus(foundHandle);
            if (status != NativeWebSocket.HttpPending) {
                NativeWebSocket.HttpRelease(foundHandle);
                foundHandle = 0;
                FoundSidecarDone(status);
            }
        }
    }

    // mirrors RetryOnFail: Gone revokes RBs and drops, NotAcceptable and
    // success drop, other statuses re-issue, transport errors drop
    private static void FoundSidecarDone(int status) {
        if (SendingPickup == null) {
            return;
        }

        if (status == 410) {
            if (SendingPickup.type == "RB") {
                RandomizerBonus.UpgradeID(-int.Parse(SendingPickup.id));
            }

            SendingPickup = null;
        } else if (status == 406 || (status >= 200 && status < 300)) {
            SendingPickup = null;
        } else if (status > 0) {
            foundHandle = NativeWebSocket.HttpBegin("GET", SendingPickup.GetURL().ToString(), null, null);
            if (foundHandle == 0) {
                RequeuePickup();
            }
        } else {
            Randomizer.log($"found fallback: transport error {status}, dropping");
            SendingPickup = null;
        }
    }

    // the one http door for pickups: engine picked by the scheme
    private static void SendFoundHttp() {
        if (UseSidecarHttp) {
            SidecarForget(foundHandle);
            foundHandle = NativeWebSocket.HttpBegin("GET", SendingPickup.GetURL().ToString(), null, null);
            if (foundHandle == 0) {
                RequeuePickup();
            }
        } else if (!SecureNetcode) {
            webClient.DownloadStringAsync(SendingPickup.GetURL());
        } else {
            RequeuePickup();
        }
    }

    // no transport for this one right now: back in line, never dropped
    private static void RequeuePickup() {
        if (SendingPickup != null) {
            PickupQueue.Enqueue(SendingPickup);
            SendingPickup = null;
        }
    }

    public static void UploadSeed() {
        try {
            // no transport at all: leave SeedSent false, Update retries
            // until the socket reconnects (cheap: two bool checks/frame)
            if (!WsOpen && wsNoHttp) {
                return;
            }

            var array = File.ReadAllLines(Randomizer.SeedFilePath);
            array[0] = array[0].Replace(',', '|');
            var seed = string.Join(",", array).Replace("#", "");
            if (WsOpen) {
                NativeWebSocket.SendText("seed:seed=" + EscapeLong(seed) + "&version=" + Randomizer.VERSION);
            } else if (UseSidecarHttp) {
                var handle = NativeWebSocket.HttpBegin("POST", RootUrl + "/setSeed",
                    "seed=" + EscapeLong(seed) + "&version=" + Randomizer.VERSION, FormType);
                if (handle == 0) {
                    return;  // leave SeedSent false; Update tries again
                }

                SidecarForget(handle);
            } else if (!SecureNetcode) {
                var nvc = new NameValueCollection();
                nvc.Set("seed", seed);
                nvc.Set("version", Randomizer.VERSION);
                var client = new WebClient();
                client.UploadValuesAsync(new Uri(RootUrl + "/setSeed"), nvc);
            }

            SeedSent = true;
        } catch (Exception e) {
            Randomizer.LogError("UploadSeed: " + e.Message);
        }
    }

    // Uri.EscapeDataString throws past ~32k chars and seeds can get there
    // (plandos especially, escaped commas inflate 3x). Chunked escaping is
    // character-wise safe: seed text is ASCII.
    public static string EscapeLong(string s) {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i += 16000) {
            sb.Append(Uri.EscapeDataString(s.Substring(i, Math.Min(16000, s.Length - i))));
        }

        return sb.ToString();
    }

    public static bool getBit(int bf, int bit) {
        return 1 == (bf >> bit & 1);
    }

    public static int getTaste(int bf, int taste) {
        return bf >> 2 * taste & 3;
    }

    public static void CheckPickups(object sender, UploadValuesCompletedEventArgs e) {
        try {
            if (e.Error != null) {
                if (e.Error is NullReferenceException) {
                    return;
                }

                Randomizer.LogError("CheckPickups got error: " + e.Error);
            }

            if (!e.Cancelled && e.Error == null) {
                if (!Characters.Sein) {
                    return;
                }

                ProcessTickResponse(Encoding.UTF8.GetString(e.Result));
                return;
            }

            if (e.Error.GetType().Name == "WebException" && ((HttpWebResponse)((WebException)e.Error).Response).StatusCode == HttpStatusCode.PreconditionFailed) {
                if (Randomizer.SyncMode == 1 || Randomizer.SyncMode == 5) {
                    Randomizer.printInfo("Co-op server error, try reloading the seed (Alt+L)");
                } else {
                    Randomizer.LogError("Co-op server error, try reloading the seed (Alt+L)");
                }
            }
        } catch (Exception e2) {
            Randomizer.LogError("CheckPickups threw error: " + e2.Message);
        }
    }

    // One frame off the websocket. Kinds: "tick:<body>" (<body> is
    // byte-identical to a /tick/ http response, whether replied or pushed),
    // "foundack:<token>|<status>" (our pickup ack), "nohttp:" (server says
    // the http fallback routes are gone — websocket-only from here on),
    // "err:<what>" (server didn't understand one of our frames — old
    // server). Unknown kinds are logged and dropped so older dlls survive
    // newer servers. Only tick/foundack need Sein (they touch the
    // inventory); the rest must land even at the title screen — nohttp
    // in particular arrives right after connect.
    public static void ProcessFrame(string frame) {
        try {
            var sep = frame.IndexOf(':');
            var kind = sep < 0 ? frame : frame.Substring(0, sep);
            if (kind == "tick" && sep >= 0) {
                if (!Characters.Sein) {
                    return;
                }

                ProcessTickResponse(frame.Substring(sep + 1));
            } else if (kind == "foundack" && sep >= 0) {
                // dropping the ack is safe: the 5s timeout resends
                if (!Characters.Sein) {
                    return;
                }

                OnFoundAck(frame.Substring(sep + 1));
            } else if (kind == "bingoack" && sep >= 0) {
                BingoController.OnBingoAck(frame.Substring(sep + 1));
            } else if (kind == "goals" && sep >= 0) {
                BingoController.LoadGoals(frame.Substring(sep + 1));
            } else if (kind == "ghosts" && sep >= 0) {
                RandomizerGhostSignal.OnRoster(frame.Substring(sep + 1));
            } else if (kind == "ghost" && sep >= 0) {
                RandomizerGhostSignal.OnDescription(frame.Substring(sep + 1));
            } else if (kind == "completeack") {
                // no Sein guard: this arrives during credits
                completePending = false;
            } else if (kind == "nohttp") {
                wsNoHttp = true;
                Randomizer.log("ws: server flagged http fallback unavailable; websocket-only mode");
            } else if (kind == "err" && sep >= 0) {
                // a server that errs one of our frame kinds predates it:
                // route that channel back to http
                var what = frame.Substring(sep + 1);
                if (what.StartsWith("found")) {
                    wsFoundUnsupported = true;
                    if (SendingPickup != null && wsFoundToken != 0 && !wsNoHttp && !webClient.IsBusy) {
                        wsFoundToken = 0;
                        SendFoundHttp();
                    }
                } else if (what.StartsWith("goals")) {
                    BingoController.OnGoalsErr(what);
                } else if (what.StartsWith("bingo")) {
                    BingoController.OnBingoErr();
                }

                if (what.StartsWith("tick")) {
                    Refused = true;
                }

                Randomizer.log("ws: server err frame: " + what);
            } else {
                Randomizer.LogError("ProcessFrame: unknown frame kind: " + kind);
            }
        } catch (Exception e) {
            Randomizer.LogError("ProcessFrame threw error: " + e.Message);
        }
    }

    // Mirrors RetryOnFail's status handling exactly: Gone revokes RBs and
    // stops, NotAcceptable drops, anything else transient retries (ws up
    // to 3 attempts, then http). Stale acks (token mismatch: we already
    // fell back to http) are ignored — the server dedups the replay.
    private static void OnFoundAck(string body) {
        var parts = body.Split('|');
        var token = int.Parse(parts[0]);
        var status = int.Parse(parts[1]);
        if (SendingPickup == null || token != wsFoundToken) {
            return;
        }

        wsFoundToken = 0;
        if (status == 410) {
            if (SendingPickup.type == "RB") {
                RandomizerBonus.UpgradeID(-int.Parse(SendingPickup.id));
            }

            SendingPickup = null;
        } else if (status < 300 || status == 406) {
            SendingPickup = null;
        } else if (wsFoundAttempts < 3 && WsOpen) {
            SendFoundWs();
        } else {
            FoundFallback();
        }
    }

    // The in-flight pickup needs another route: http if allowed, else keep
    // working the socket (resend now if it's open, or re-arm the timeout
    // and wait out the reconnect — the retry loop re-fires every 5s).
    private static void FoundFallback() {
        if (HttpLaneOpen) {
            wsFoundToken = 0;
            if (!webClient.IsBusy) {
                SendFoundHttp();
            } else {
                // webClient busy should be impossible here (the queue is
                // serial), but never strand a pickup on an impossibility
                PickupQueue.Enqueue(SendingPickup);
                SendingPickup = null;
            }
        } else if (WsOpen) {
            SendFoundWs();
        } else {
            wsFoundSentAt = Time.realtimeSinceStartup;
        }
    }

    private static void SendFoundWs() {
        wsFoundToken = ++wsFoundCounter;
        wsFoundSentAt = Time.realtimeSinceStartup;
        wsFoundAttempts++;
        NativeWebSocket.SendText(SendingPickup.WsBody(wsFoundToken));
    }

    public static void ProcessTickResponse(string data) {
        Answered = Time.realtimeSinceStartup;
        try {
            if (Randomizer.CreditsActive) {
                RandomizerSwitch.SilentMode = true;
            }

            var mustRefreshLogic = false;
            var array = data.Split(',');
            var bf = int.Parse(array[0]);
            foreach (var skillInfoLine in SkillInfos) {
                if (getBit(bf, skillInfoLine.bit) && !Characters.Sein.PlayerAbilities.HasAbility(skillInfoLine.skill)) {
                    RandomizerSwitch.GivePickup(new RandomizerAction("SK", $"{skillInfoLine.id}"), 0, false);
                    mustRefreshLogic = true;
                }
            }

            var bf2 = int.Parse(array[1]);
            foreach (var eventInfoLine in EventInfos) {
                if (getBit(bf2, eventInfoLine.bit) && !eventInfoLine.checker()) {
                    RandomizerSwitch.GivePickup(new RandomizerAction("EV", $"{eventInfoLine.id}"), 0, false);
                    mustRefreshLogic = true;
                }
            }

            var bf4 = int.Parse(array[2]);
            foreach (var teleportInfoLine in TeleportInfos) {
                if (getBit(bf4, teleportInfoLine.bit) && !isTeleporterActivated(teleportInfoLine.id)) {
                    RandomizerSwitch.GivePickup(new RandomizerAction("TP", $"{teleportInfoLine.id}"), 0, false);
                    mustRefreshLogic = true;
                }
            }

            if (array[3] != "") {
                var upgrades = array[3].Split(';');
                foreach (var rawUpgrade in upgrades) {
                    var splitpair = rawUpgrade.Split('x');
                    if (splitpair[0].Contains("_")) {
                        if (WarpDatas.ContainsKey(splitpair[0])) {
                            WarpDatas[splitpair[0]].GrantFromNetwork();
                            continue;
                        }

                        Randomizer.LogError($"Unknown ?Warp? {rawUpgrade}");
                    }

                    var id = int.Parse(splitpair[0]);
                    var cnt = int.Parse(splitpair[1]);
                    // 900-909: tree progress
                    if (id >= 900 && id < 910) {
                        var tree = id - 899;
                        var treeName = RandomizerTrackedDataManager.Trees[tree];
                        if (RandomizerTrackedDataManager.SetTree(tree)) {
                            Randomizer.showHint(RandomizerUI.Message.PickupMessage(treeName + " tree (activated by teammate)"));
                        }
                        // 911-921: relic progress
                    } else if (id >= 911 && id < 922) {
                        var relicZone = RandomizerTrackedDataManager.Zones[id - 911];
                        if (RandomizerTrackedDataManager.SetRelic(relicZone)) {
                            Randomizer.showHint(RandomizerUI.Message.PickupMessage("#" + relicZone + " relic# (found by teammate)", 5f));
                        }
                        // 100-129: bonus skills
                    } else if (id >= 100 && id < 130) {
                        if (cnt > 0 && RandomizerBonus.UpgradeCount(id) == 0) {
                            GrantUpgrade(id);
                        }
                        // everything else!
                    } else if (RandomizerBonus.UpgradeCount(id) < cnt) {
                        GrantUpgrade(id);
                        mustRefreshLogic = true;
                    } else if (!PickupQueue.Where(p => p.type == "RB" && p.id == splitpair[0]).Any() && RandomizerBonus.UpgradeCount(id) > cnt) {
                        RandomizerBonus.UpgradeID(-id);
                        mustRefreshLogic = true;
                    }
                }
            }

            // signals ride at index 5. In multiworld games the field is
            // always present (possibly empty) so the slot bitfields can
            // sit at a fixed index 6; legacy games omit it when empty.
            if (array.Length > 5 && array[5] != "") {
                foreach (var text in array[5].Split('|')) {
                    if (text == "" || CurrentSignals.Contains(text)) {
                        continue;
                    }

                    if (text == "stop") {
                        RandomizerChaosManager.ClearEffects();
                    } else if (text.StartsWith("msg:")) {
                        if (!RandomizerSwitch.SilentMode) {
                            Randomizer.printInfo(text.Substring(4), 360);
                        }
                    } else if (text.StartsWith("win:")) {
                        if (!RandomizerBonusSkill.UnlockCreditWarp(text.Substring(4))) {
                            if (!RandomizerSwitch.SilentMode) {
                                Randomizer.QueueWinMessage(text.Substring(4));
                            }

                            RandomizerStatsManager.WriteStatsFile();
                        }
                    } else if (text.StartsWith("pickup:")) {
                        var parts = text.Substring(7).Split('|');
                        RandomizerAction action;
                        action = new RandomizerAction(parts[0], parts[1]);
                        RandomizerSwitch.GivePickup(action, 0, false);
                        mustRefreshLogic = true;
                    } else if (text.StartsWith("dl:")) {
                        // archipelago death link: "<token>;<source>"
                        RandomizerDeathLink.OnSignal(text.Substring(3));
                    } else if (text.StartsWith("apfrom:")) {
                        // who found the slots this tick is about to grant;
                        // the slot field is read further down, after this
                        RandomizerMW.OnApFromSignal(text.Substring(7));
                    } else if (text == "spawnChaos") {
                        Randomizer.ChaosVerbose = true;
                        RandomizerChaosManager.SpawnEffect();
                        ChaosTimeoutCounter = 3600;
                    }

                    if (WsOpen) {
                        NativeWebSocket.SendText("conf:" + text);
                    } else if (!wsNoHttp && UseSidecarHttp) {
                        SidecarForget(NativeWebSocket.HttpBegin("GET", RootUrl + "/callback/" + text, null, null));
                    } else if (!wsNoHttp && !SecureNetcode) {
                        var client = new WebClient();
                        client.DownloadStringAsync(new Uri(RootUrl + "/callback/" + text));
                    }

                    // (no transport right now: the confirm is lost, same
                    // as a failed fire-and-forget GET today — the signal
                    // lingers server-side until the next seed reload)
                    CurrentSignals.Add(text);
                }
            } else {
                CurrentSignals.Clear();
            }

            if (Randomizer.SyncMode == 5 && array.Length > 7) {
                // multiworld: player names first, so slot grant messages
                // on this same tick can already use them
                RandomizerMW.OnNamesField(array[7]);
            }

            if (Randomizer.SyncMode == 5 && array.Length > 6) {
                // multiworld: our slot bitfields (what others found for us)
                if (RandomizerMW.OnSlotsField(array[6])) {
                    mustRefreshLogic = true;
                }
            }

            // trailing and optional: a server that does not send it leaves Released alone
            if (array.Length > 9) {
                RandomizerMW.OnReleasedField(array[9]);
            }

            if (Randomizer.SyncMode == 5 && array.Length > 8 && array[8] != "") {
                // archipelago: hints bought for the slots we asked about.
                // Present only once something has been bought, so every
                // other multiworld tick still ends at field 7.
                RandomizerMW.OnApHintsField(array[8]);
            }

            if (mustRefreshLogic) {
                RandomizerLocationManager.UpdateReachable();
            }
        } finally {
            RandomizerSwitch.SilentMode = false;
        }
    }

    // a teammate's upgrade has no location and never passes through GivePickup
    private static void GrantUpgrade(int id) {
        RandomizerStatsManager.PickupZone = "offworld";
        try {
            RandomizerBonus.UpgradeID(id);
        } finally {
            RandomizerStatsManager.PickupZone = null;
        }
    }

    public static void RetryOnFail(object sender, DownloadStringCompletedEventArgs e) {
        var ln = 0;
        try {
            if (SendingPickup == null) {
                Randomizer.log("Error: no sending pickup found!");
                return;
            }

            ln = 1;
            if (e.Cancelled || e.Error != null) {
                ln = 2;
                if (e.Error is WebException we && we.Response != null) {
                    ln = 3;
                    var statusCode = ((HttpWebResponse)we.Response).StatusCode;
                    ln = 4;
                    if (statusCode == HttpStatusCode.Gone) {
                        ln = 5;
                        if (SendingPickup.type == "RB") {
                            ln = 6;
                            RandomizerBonus.UpgradeID(-int.Parse(SendingPickup.id));
                        }
                    } else if (statusCode != HttpStatusCode.NotAcceptable) {
                        SendFoundHttp();
                        return;
                    }

                    SendingPickup = null;
                    return;
                }

                if (e.Error != null) {
                    Randomizer.log($"RetryOnFail (ln: {ln}) got responseless excpetion: {e}");
                }
            }

            SendingPickup = null;
        } catch (Exception ee) {
            Randomizer.LogError($"RetryOnFail: {ee.Message}, e: {e}, ln {ln}");
            if (ee.Message == "Object reference not set to an instance of an object") {
                Randomizer.printInfo("Strange Network Error! Ping Eiko in the ori discord if you see this");
                SendingPickup = null;
            }
        }
    }

    public static void FoundPickup(RandomizerAction action, int coords) {
        try {
            var pickup = new Pickup(action, coords);
            PickupQueue.Enqueue(pickup);
        } catch (Exception e) {
            Randomizer.LogError($"FoundPickup: {action.Action}: {e.Message}\n{e.StackTrace}");
        }
    }

    // credits-roll ping: the server treats it as the real game end, and in
    // multiworld it releases our world's leftovers to their owners — so it
    // is no longer fire-and-forget (game 134478 stranded 62 items when this
    // died with the process during credits). Retries until the server acks
    // (completeack frame); the http path can't ack, so it just gets a few
    // spaced attempts. game_complete is idempotent server-side.
    public static void SendGameComplete() {
        if (!Randomizer.Sync || Randomizer.SyncId == "") {
            return;
        }

        completePending = true;
        completeAttempts = 0;
        TrySendComplete();
    }

    private static void TrySendComplete() {
        completeNextAt = Time.realtimeSinceStartup + 3f;
        try {
            if (WsOpen) {
                NativeWebSocket.SendText("complete:");
                completeAttempts++;
            } else if (!wsNoHttp && UseSidecarHttp) {
                SidecarForget(NativeWebSocket.HttpBegin("GET", RootUrl + "/complete", null, null));
                completeAttempts++;
            } else if (!wsNoHttp && !SecureNetcode) {
                var client = new WebClient();
                client.DownloadStringAsync(new Uri(RootUrl + "/complete"));
                completeAttempts++;
            }

            // no transport right now: attempts don't count, wait for the socket
            if (completeAttempts >= 5) {
                completePending = false;
            }
        } catch (Exception e) {
            Randomizer.log("SendGameComplete: " + e.Message);
        }
    }

    // A sync seed whose server never answers is silently solo: pickups do not reach anyone and
    // nothing on screen says so. Said once, after long enough that a slow start cannot trigger it.
    private const float SilenceLimit = 30f;

    private static void Silent() {
        if (Warned || !Randomizer.Sync || Answered <= 0f ||
                Time.realtimeSinceStartup - Answered < SilenceLimit) {
            return;
        }

        Warned = true;
        Randomizer.printInfo(Refused
            ? "@Not syncing@: the server does not know this game. Check your Netcode URLs."
            : "@Not syncing@: no reply from the server. Check your Netcode URLs.", 480);
    }

    public static void FoundTP(string identifier) {
        if (!Randomizer.Sync) {
            return;
        }

        try {
            if (TPIds.ContainsKey(identifier) && !isTeleporterActivated(identifier, false)) {
                FoundPickup(TPIds[identifier], 1); // this used to be -1 but multiworlds need that
            }
        } catch (Exception e) {
            Randomizer.LogError("FoundTP: " + e.Message);
        }
    }

    public static bool isTeleporterActivated(string identifier) {
        return isTeleporterActivated(identifier, true);
    }

    public static bool isTeleporterActivated(string identifier, bool translate) {
        if (translate) {
            identifier = Randomizer.TeleportTable[identifier].ToString();
        }

        try {
            if (Characters.Sein && Characters.Sein.Inventory) {
                if (identifier == "ginsoTree" && Characters.Sein.Inventory.GetRandomizerItem(1024) == 1) {
                    return true;
                }

                if (identifier == "forlorn" && Characters.Sein.Inventory.GetRandomizerItem(1025) == 1) {
                    return true;
                }

                if (identifier == "mountHoru" && Characters.Sein.Inventory.GetRandomizerItem(1026) == 1) {
                    return true;
                }
            }

            foreach (var gameMapTeleporter in TeleporterController.Instance.Teleporters) {
                if (gameMapTeleporter.Identifier == identifier) {
                    return gameMapTeleporter.Activated;
                }
            }
        } catch (Exception e) {
            Randomizer.LogError("IsTPActive: " + identifier + " " + e.Message + ". Not criticial unless repeating.");
        }

        return false;
    }

    private static float tslu;

    public static Pickup SendingPickup;

    public static string RootUrl;

    public static int PERIOD = 1;

    public static WebClient webClient;

    public static WebClient getClient;

    public static List<SkillInfoLine> SkillInfos;

    public static List<EventInfoLine> EventInfos;

    public static List<TeleportInfoLine> TeleportInfos;

    public static int ChaosTimeoutCounter;

    public static Queue<Pickup> PickupQueue;

    public static bool SeedSent;

    public static HashSet<string> CurrentSignals;

    public static bool NetworkFree => Randomizer.SyncId == "" || (PickupQueue.Count == 0 && SendingPickup == null && !webClient.IsBusy);

    // Same fields the http tick sends, as a form-encoded body. The server's
    // ws adapter must parse this identically to request.form.
    private static string TickPayload() {
        var pos = Characters.Sein.Position;
        var sb = new StringBuilder();
        sb.Append("x=").Append(pos.x.ToString());
        sb.Append("&y=").Append(pos.y.ToString());
        sb.Append("&version=").Append(Uri.EscapeDataString(Randomizer.VERSION));
        for (var i = 0; i < 8; i++) {
            sb.Append("&seen_").Append(i).Append('=').Append(fixInt(Characters.Sein.Inventory.GetRandomizerItem(1560 + i)));
            sb.Append("&have_").Append(i).Append('=').Append(fixInt(Characters.Sein.Inventory.GetRandomizerItem(930 + i)));
        }

        // archipelago: the manifest slots our own reveals want a hint for.
        // Absent on every other seed, and older servers ignore the key.
        var apHints = RandomizerMW.HintRequestField();
        if (apHints != null) {
            sb.Append("&aph=").Append(apHints);
        }

        // archipelago death link: this run's death counters, which the
        // server diffs. Absent on every seed without the option.
        var deaths = RandomizerDeathLink.Field();
        if (deaths != null) {
            sb.Append("&dl=").Append(deaths);
        }

        return sb.ToString();
    }

    // A socket that has never connected and keeps erroring is written off
    // for the session (bad TLS, old server, blocked port — http covers us).
    // A socket that connected once keeps auto-reconnecting forever.
    private static void CheckWebsocketHealth() {
        if (wsDead || wsStartedUrl == null || !NativeWebSocket.Loaded) {
            return;
        }

        if (NativeWebSocket.GetOpenCount() == 0 && NativeWebSocket.GetErrorCount() >= 8) {
            wsDead = true;
            NativeWebSocket.Stop();
            Randomizer.log($"websocket: giving up after {NativeWebSocket.GetErrorCount()} failures ({NativeWebSocket.GetLastError()}); using http");
        }
    }

    // set when a sync seed loads, so the silence is measured from the game rather than boot
    public static float Answered;

    private static bool Refused;

    private static bool Warned;

    private static string wsStartedUrl;

    private static string wsUrl;

    private static bool wsDead;

    private static int wsLoadAttempts;

    private static float wsNextTry;

    private static int wsFoundCounter;

    private static int wsFoundToken;

    private static float wsFoundSentAt;

    private static int wsFoundAttempts;

    private static bool wsFoundUnsupported;
    private static bool wsWasOpen;

    private static bool wsNoHttp;

    private static bool completePending;

    private static int completeAttempts;

    private static float completeNextAt;

    // BingoController checks this before its own http fallback
    public static bool WsNoHttp => wsNoHttp;

    public static string fixInt(int stupidFuckingSignedInt) {
        return ((uint)stupidFuckingSignedInt).ToString();
    }

    public static Dictionary<string, RandomizerAction> TPIds = new Dictionary<string, RandomizerAction> {
        { "swamp", new RandomizerAction("TP", "Swamp") },
        { "sorrowPass", new RandomizerAction("TP", "Valley") },
        { "moonGrotto", new RandomizerAction("TP", "Grotto") },
        { "valleyOfTheWind", new RandomizerAction("TP", "Sorrow") },
        { "spiritTree", new RandomizerAction("TP", "Grove") },
        { "ginsoTree", new RandomizerAction("TP", "Ginso") },
        { "forlorn", new RandomizerAction("TP", "Forlorn") },
        { "mountHoru", new RandomizerAction("TP", "Horu") },
        { "mangroveFalls", new RandomizerAction("TP", "Blackroot") },
        { "sunkenGlades", new RandomizerAction("TP", "Glades") },
    };

    public class Pickup {
        public override bool Equals(object obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            var pickup = (Pickup)obj;
            return type == pickup.type && id == pickup.id && coords == pickup.coords;
        }

        public override int GetHashCode() {
            return (type + id).GetHashCode() ^ coords.GetHashCode();
        }

        public Pickup(string _type, string _id, int _coords) {
            type = _type;
            id = _id;
            coords = _coords;
        }

        public Pickup(RandomizerAction action, int _coords) {
            type = action.Action;
            id = action.ValAsStr();
            coords = _coords;
        }

        public string CleanedId {
            get {
                var cleaned_id = id.Replace("#", "");
                if (cleaned_id.Contains("\\")) {
                    cleaned_id = cleaned_id.Split('\\')[0];
                }

                return cleaned_id;
            }
        }

        public Uri GetURL() {
            var url = RootUrl + "/found/" + coords + "/" + type + "/" + CleanedId;
            url += "?zone=" + RandomizerStatsManager.CurrentZone();

            return new Uri(url);
        }

        // found:<token>|<qs>|<coords>|<kind>|<id> — id last, the server
        // parses it greedily (TW ids carry commas)
        public string WsBody(int token) {
            return "found:" + token + "|zone=" + Uri.EscapeDataString(RandomizerStatsManager.CurrentZone()) + "|" + coords + "|" + type + "|" + CleanedId;
        }

        public string id;

        public string type;

        public int coords;
        private string urlGarbage;
    }

    public class SkillInfoLine {
        public SkillInfoLine(int _id, int _bit, AbilityType _skill) {
            bit = _bit;
            id = _id;
            skill = _skill;
        }

        public override bool Equals(object obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            var skillInfoLine = (SkillInfoLine)obj;
            return bit == skillInfoLine.bit && id == skillInfoLine.id && skill == skillInfoLine.skill;
        }

        public override int GetHashCode() {
            return skill.GetHashCode() ^ id.GetHashCode() ^ bit.GetHashCode();
        }

        public int id;
        public int bit;
        public AbilityType skill;
    }

    public delegate int UpgradeCounter();

    public class UpgradeInfoLine {
        public UpgradeInfoLine(int _id, int _bit, bool _stacks, UpgradeCounter _counter) {
            bit = _bit;
            id = _id;
            stacks = _stacks;
            counter = _counter;
        }

        public override bool Equals(object obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            var upgradeInfoLine = (UpgradeInfoLine)obj;
            return bit == upgradeInfoLine.bit && id == upgradeInfoLine.id;
        }

        public override int GetHashCode() {
            return bit.GetHashCode() ^ id.GetHashCode();
        }

        public int id;

        public int bit;

        public bool stacks;

        public UpgradeCounter counter;
    }

    public delegate bool EventChecker();

    public class EventInfoLine {
        public EventInfoLine(int _id, int _bit, EventChecker _checker) {
            bit = _bit;
            id = _id;
            checker = _checker;
        }

        public override bool Equals(object obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            var eventInfoLine = (EventInfoLine)obj;
            return bit == eventInfoLine.bit && id == eventInfoLine.id;
        }

        public override int GetHashCode() {
            return bit.GetHashCode() ^ id.GetHashCode();
        }

        public int id;

        public EventChecker checker;

        public int bit;
    }

    public class TeleportInfoLine {
        public TeleportInfoLine(string _id, int _bit) {
            bit = _bit;
            id = _id;
        }

        public override bool Equals(object obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            var teleportInfoLine = (TeleportInfoLine)obj;
            return bit == teleportInfoLine.bit && id == teleportInfoLine.id;
        }

        public override int GetHashCode() {
            return bit.GetHashCode() ^ id.GetHashCode();
        }

        public string id;

        public int bit;
    }

    public class WarpData {
        public WarpData(string _name, string _area, int _x, int _y) {
            x = _x;
            y = _y;
            name = $"Warp to {_name}";
            area = _area;
        }

        public int x;
        public int y;
        public string name;
        public string area;

        public override int GetHashCode() {
            return name.GetHashCode();
        }

        public void GrantFromNetwork() {
            if (TeleporterController.HasCustomWarp(name)) {
                return;
            }

            if (!Randomizer.WarpLogicLocations.ContainsKey(name)) {
                Randomizer.WarpLogicLocations.Add(name, area);
            }

            RandomizerSwitch.GivePickup(new RandomizerAction("TW", $"{name},{x},{y}"), 0, false);
        }
    }


    public static Dictionary<string, WarpData> WarpDatas = new Dictionary<string, WarpData> {
        { "917_-70", new WarpData("Stomp Tree Roof", "StompAreaRoofExpWarp", 917, -70) },
        { "790_-195", new WarpData("Swamp Swim", "SwampWaterWarp", 790, -195) },
        { "720_-95", new WarpData("Inner Swamp EC", "InnerSwampSkyArea", 720, -95) },
        { "580_-345", new WarpData("Above Grotto Crushers", "AboveGrottoCrushersWarp", 580, -345) },
        { "513_-440", new WarpData("Grotto Energy Vault", "GrottoEnergyVaultWarp", 513, -440) },
        { "506_-246", new WarpData("Water Vein", "WaterVeinArea", 506, -246) },
        { "310_-230", new WarpData("Dash Plant", "DashPlantAccess", 310, -230) },
        { "258_-382", new WarpData("Right of Grenade Area", "GrenadeAreaAccess", 258, -382) },
        { "499_-505", new WarpData("Lost Grove Laser Lever", "LostGroveLaserLeverWarp", 499, -505) },
        { "-13_-96", new WarpData("Above Cflame Tree EX", "AboveChargeFlameTreeExpWarp", -13, -96) },
        { "70_-110", new WarpData("Spidersack Energy Door", "SpiderSacEnergyDoorWarp", 70, -110) },
        { "328_-176", new WarpData("Death Gauntlet Roof", "DeathGauntletRoof", 328, -176) },
        { "77_11", new WarpData("Horu Fields Push Block", "HoruFieldsPushBlock", 77, 11) },
        { "330_-63", new WarpData("Kuro CS AC", "HollowGroveTreeAbilityCellWarp", 330, -63) },
        { "380_-143", new WarpData("Butter Cell Floor", "GroveWaterStompAbilityCellWarp", 380, -143) },
        { "585_-68", new WarpData("Outer Swamp HC", "OuterSwampHealthCellWarp", 585, -68) },
        { "505_-108", new WarpData("Outer Swamp AC", "OuterSwampMortarAbilityCellLedge", 505, -108) },
        { "646_-127", new WarpData("Triforce AC", "SwampDrainlessArea", 646, -127) },
        { "-224_-85", new WarpData("Valley entry (upper)", "ValleyEntryTree", -224, -85) },
        { "-605_-255", new WarpData("Forlorn entrance", "OutsideForlorn", -605, -255) },
        { "-354_-98", new WarpData("Three Bird AC", "VallleyThreeBirdACWarp", -354, -98) },
        { "-570_156", new WarpData("Wilhelm EX", "WilhelmExpWarp", -570, 156) },
        { "-358_65", new WarpData("Stompless AC", "ValleyRightFastStomplessCellWarp", -358, 65) },
        { "-578_-25", new WarpData("Misty Entrance", "MistyEntrance", -578, -25) },
        { "-500_587", new WarpData("Sunstone Plant", "SunstoneArea", -500, 587) },
        { "-432_322", new WarpData("Sorrow Mapstone", "SorrowMapstoneWarp", -432, 322) },
        { "-595_385", new WarpData("Tumbleweed Keystone Door", "LeftSorrowTumbleweedDoorWarp", -595, 385) },
        { "510_910", new WarpData("Ginso Escape", "GinsoEscape", 510, 910) },
        { "539_434", new WarpData("Upper Ginso EC", "UpperGinsoEnergyCellWarp", 539, 434) },
        { "520_274", new WarpData("Lower Ginso Keystones", "GinsoMiniBossDoor", 520, 274) },
        { "69_96", new WarpData("Horu Escape Access", "HoruBasement", 69, 96) },
        { "155_362", new WarpData("Horu R1 Mapstone", "HoruR1MapstoneSecret", 155, 362) },
        { "254_188", new WarpData("Horu R4 Cutscene Rock", "HoruR4CutsceneTrigger", 254, 188) },
        { "-610_-312", new WarpData("Forlorn HC", "RightForlorn", -610, -312) },
        { "-747_-407", new WarpData("Forlorn Orb", "ForlornOrbPossession", -747, -407) },
        { "-820_-265", new WarpData("Forlorn Plant", "ForlornOrbPossession", -820, -265) },
        { "-219_-176", new WarpData("Spirit Cavern AC", "SpiritCavernsACWarp", -219, -176) },
        { "-162_-175", new WarpData("Above Gladeser", "GladesLaserArea", -162, -175) },
        { "-241_-211", new WarpData("Glades Loop Keystone", "UpperLeftGlades", -241, -211) },
    };
}

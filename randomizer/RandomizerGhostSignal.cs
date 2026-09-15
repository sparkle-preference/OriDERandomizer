using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using Sample = RandomizerGhost.Sample;

// Ghost multiplayer over the game's own websocket. The server passes two strings between two
// players and never looks inside them, so everything about who connects to whom is decided
// here, from a roster every client computes the same way.
//
//   out  ghosts:1                 join (or 0 to leave)
//   in   ghosts:<host>:<pids>     the roster, pushed whenever it changes
//   out  ghost:<to>:<type>|<b64>  a description for one player
//   in   ghost:<from>:<type>|<b64>
//   out  ghostice:<peer>          a peer no direct attempt could reach
//   in   ice:<json>               relay credentials for this game
//
// The lowest participating player id hosts and offers to everyone else; everyone else answers.
// No negotiation round, and both sides derive the same answer from the same list. The type
// rides inside the payload rather than in the frame, so the server stays a dumb relay.
//
// Motion is a star: the host hands every packet it receives to every other peer, so one link is
// enough to see everyone. Packets carry their sender, ghosts are keyed by it, and a client drops
// its own row on the way back in.
public static class RandomizerGhostSignal {
    private class Link {
        public int Handle;
        public int PlayerId;
        public bool Offering;
        public bool Sent;
        public bool Announced;
        public float Since;
        public int Attempt;
    }

    // How long a peer may sit in connecting before the handshake counts as lost; the roster
    // still lists the player, so nothing else would ever re-offer.
    private const float RetryAfter = 15f;

    private const string StunServers = "stun:stun.l.google.com:19302";

    private const float SendInterval = 1f / 30f;

    public static bool Joined { get; private set; }

    // participation dies with the socket it was announced on, and that socket can be
    // replaced without this code seeing it
    private const float ReassertAfter = 15f;

    // Told, not polled: the roster lives on the socket, so a socket that closes takes
    // participation with it and a new one has never been told.
    public static void Apply() {
        var want = RandomizerSyncManager.WsOpen && NativeWebSocket.RtcAvailable &&
            RandomizerSettings.Customization.ShowOtherPlayers.Value;
        var now = Time.realtimeSinceStartup;
        var told = announcedOn == RandomizerSyncManager.WsGeneration && now - announcedAt < ReassertAfter;
        if (want == Joined && (!want || told)) {
            return;
        }

        Joined = want;
        if (RandomizerSyncManager.WsOpen) {
            NativeWebSocket.SendText(want ? "ghosts:1" : "ghosts:0");
            announcedOn = RandomizerSyncManager.WsGeneration;
            announcedAt = now;
        }

        if (!want) {
            DropAll();
        }

        // A flapping socket rejoins on every reconnect, which is correct and not worth saying.
        // Only a change of mind -- the setting, or a sidecar that cannot do this at all -- is.
        var mind = NativeWebSocket.RtcAvailable &&
            RandomizerSettings.Customization.ShowOtherPlayers.Value;
        if (mind != Minded) {
            Minded = mind;
            Randomizer.log("ghost signal: " + (mind ? "joined" : "left"));
        }
    }

    // "ghosts:<host>:<pids>" -- who is here and who offers. Arrives on every change, so this
    // is also how a peer leaving is noticed.
    public static void OnRoster(string body) {
        var sep = body.IndexOf(':');
        if (sep < 0) {
            return;
        }

        int host;
        if (!int.TryParse(body.Substring(0, sep), out host)) {
            return;
        }

        var present = new List<int>();
        foreach (var part in body.Substring(sep + 1).Split(',')) {
            int pid;
            if (part.Length > 0 && int.TryParse(part, out pid)) {
                present.Add(pid);
            }
        }

        Host = host;
        Randomizer.log("ghost signal: roster host=" + host + " players=" + present.Count);

        // anyone who left takes their peer and their ghost with them
        for (var i = Links.Count - 1; i >= 0; i--) {
            if (!present.Contains(Links[i].PlayerId)) {
                Drop(Links[i]);
                Links.RemoveAt(i);
            }
        }

        foreach (var pid in new List<int>(Ghosts.Keys)) {
            if (!present.Contains(pid)) {
                Forget(pid);
            }
        }

        if (!Joined || Me <= 0 || host != Me) {
            return;
        }

        // the host offers to everyone else, one peer connection each
        foreach (var pid in present) {
            if (pid != Me && Find(pid) == null) {
                Open(pid, true);
            }
        }
    }

    // "ghost:<from>:<type>|<b64>" -- one description from one player.
    public static void OnDescription(string body) {
        // every rejection below is logged: a silent drop leaves the far side waiting forever
        var sep = body.IndexOf(':');
        if (sep < 0) {
            Randomizer.log("ghost signal: description with no sender, dropped");
            return;
        }

        int from;
        if (!int.TryParse(body.Substring(0, sep), out from)) {
            Randomizer.log("ghost signal: description from an unreadable player id, dropped");
            return;
        }

        var payload = body.Substring(sep + 1);
        var bar = payload.IndexOf('|');
        if (bar < 0) {
            Randomizer.log("ghost signal: description from " + from + " has no type, dropped");
            return;
        }

        Randomizer.log("ghost signal: got " + payload.Substring(0, bar) + " from " + from +
            ", " + payload.Length + " chars, joined " + Joined);

        var type = payload.Substring(0, bar);
        string sdp;
        try {
            sdp = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload.Substring(bar + 1)));
        } catch (Exception) {
            Randomizer.log("ghost signal: undecodable description from " + from);
            return;
        }

        var link = Find(from);
        if (type == "offer") {
            // an offer replaces whatever we had for them: they have restarted their side
            if (link != null) {
                Drop(link);
                Links.Remove(link);
            }

            link = Open(from, false);
            if (link == null) {
                return;
            }
        }

        if (link == null) {
            Randomizer.log("ghost signal: " + type + " from " + from + " with no peer waiting");
            return;
        }

        if (NativeWebSocket.RtcSetRemote(link.Handle, type, sdp) < 0) {
            Randomizer.log("ghost signal: " + type + " from " + from + " rejected, " +
                NativeWebSocket.RtcLastError());
        } else {
            Randomizer.log("ghost signal: accepted " + type + " from " + from +
                " on handle " + link.Handle);
        }
    }

    public static void Update() {
        if (Links.Count == 0) {
            return;
        }

        var now = Time.time;
        var due = now - LastSent >= SendInterval;
        // assigned up front: the compiler cannot see that `due` gates the out-parameter call
        var mine = new Sample();
        var haveMine = due && RandomizerGhost.SampleLive(out mine);
        if (due) {
            LastSent = now;
        }

        for (var i = Links.Count - 1; i >= 0; i--) {
            var link = Links[i];

            // the roster only speaks when it changes, so a peer that restarts keeps its place
            // in the list and nothing would ever re-offer
            if (link.Announced && !NativeWebSocket.RtcIsOpen(link.Handle)) {
                var was = link.PlayerId;
                var host = link.Offering;
                Randomizer.log("ghost signal: channel to " + was + " closed, " +
                    (host ? "re-offering" : "waiting for a fresh offer"));
                Drop(link);
                Links.RemoveAt(i);
                if (host) {
                    Open(was, true);
                }

                continue;
            }

            // a lost handshake tells neither side; only the offerer can restart one, and the
            // answerer's link is replaced when the next offer arrives. Openness is asked of the
            // channel, not of Announced: a frame gap longer than the timeout would otherwise
            // report a peer that came up fine.
            if (!link.Announced && !NativeWebSocket.RtcIsOpen(link.Handle) &&
                now - link.Since > RetryAfter) {
                var pid = link.PlayerId;
                var attempt = link.Attempt + 1;
                Randomizer.log("ghost signal: peer " + pid + " never connected in " +
                    RetryAfter + "s, state " + NativeWebSocket.RtcGetState(link.Handle) +
                    "; " + (link.Offering ? "retrying" : "dropping and waiting for an offer"));
                var offering = link.Offering;
                Drop(link);
                Links.RemoveAt(i);
                Escalate(pid);
                if (offering) {
                    var fresh = Open(pid, true);
                    if (fresh != null) {
                        fresh.Attempt = attempt;
                    }
                }

                continue;
            }

            // a description is only worth sending once ICE has finished; non-trickle means one
            // string each way and no candidate plumbing
            if (!link.Sent && NativeWebSocket.RtcLocalReady(link.Handle)) {
                link.Sent = true;
                var sdp = NativeWebSocket.RtcLocalDescription(link.Handle);
                NativeWebSocket.SendText("ghost:" + link.PlayerId + ":" +
                    (link.Offering ? "offer" : "answer") + "|" +
                    Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sdp)));
                Randomizer.log("ghost signal: sent " + (link.Offering ? "offer" : "answer") +
                    " to " + link.PlayerId + ", " + sdp.Length + " bytes");
            }

            if (!link.Announced && NativeWebSocket.RtcIsOpen(link.Handle)) {
                link.Announced = true;
                Randomizer.log("ghost signal: channel open to " + link.PlayerId);
            }

            while (NativeWebSocket.RtcHasMessage(link.Handle)) {
                var packet = NativeWebSocket.RtcGetMessage(link.Handle);
                if (packet == null) {
                    continue;
                }

                Sample got;
                byte who;
                ushort seq;
                if (!RandomizerGhostPacket.Decode(packet, packet.Length, out got, out who, out seq) || who == Me) {
                    continue;
                }

                Feed(who, got);
                if (Host == Me) {
                    Relay(packet, link);
                }
            }

            if (haveMine && NativeWebSocket.RtcIsOpen(link.Handle)) {
                var length = RandomizerGhostPacket.Encode(Buffer, mine, (byte)Me, Sequence);
                NativeWebSocket.RtcSend(link.Handle, Buffer, length);
            }
        }

        if (haveMine) {
            Sequence++;
        }
    }

    private static Link Open(int pid, bool offering) {
        // Only a peer a direct attempt already failed against: gathering is non-trickle,
        // so a slow relay in the list would stall every handshake.
        var relayed = RelayUrl != null && Relayed.Contains(pid);
        var handle = NativeWebSocket.RtcCreate(offering, relayed ? StunServers + "," + RelayUrl : StunServers);
        if (handle == 0) {
            Randomizer.log("ghost signal: could not create a peer for " + pid + ", " +
                NativeWebSocket.RtcLastError());
            return null;
        }

        var link = new Link { Handle = handle, PlayerId = pid, Offering = offering, Since = Time.time };
        Links.Add(link);
        Randomizer.log("ghost signal: " + (offering ? "offering to " : "answering ") + pid +
            ", handle " + handle + ", " + Links.Count + " link(s)" + (relayed ? ", via the relay" : ""));
        return link;
    }

    // Asked once per peer: the server reads it as the report that pid needs relaying.
    private static void Escalate(int pid) {
        var first = Relayed.Add(pid);
        if (first && RandomizerSyncManager.WsOpen) {
            NativeWebSocket.SendText("ghostice:" + pid);
        }
    }

    // "ice:<json>" -- the relay, in the same shape a browser's iceServers takes.
    public static void OnIce(string body) {
        JsonValue parsed;
        try {
            parsed = JsonValue.Parse(body);
        } catch (Exception) {
            Randomizer.log("ghost signal: unreadable relay config");
            return;
        }

        var urls = parsed["urls"];
        var user = parsed["username"];
        var secret = parsed["credential"];
        if (!urls.IsArray || urls.Count == 0 || !urls[0].IsString ||
            !user.IsString || !secret.IsString) {
            Randomizer.log("ghost signal: relay config missing a field");
            return;
        }

        var url = urls[0].Str;
        var scheme = url.IndexOf(':');
        if (scheme < 0) {
            Randomizer.log("ghost signal: relay url has no scheme");
            return;
        }

        // libdatachannel url-decodes the userinfo but its grammar ends the username at a
        // colon, and a TURN username is "<expiry>:<name>". Escape both halves.
        RelayUrl = url.Substring(0, scheme + 1) + Escape(user.Str) + ":" + Escape(secret.Str) +
            "@" + url.Substring(scheme + 1);
        Randomizer.log("ghost signal: relay configured");

        // whoever we are already failing against should not wait out another retry
        for (var i = Links.Count - 1; i >= 0; i--) {
            var link = Links[i];
            if (!link.Announced && link.Offering && Relayed.Contains(link.PlayerId)) {
                var pid = link.PlayerId;
                var attempt = link.Attempt;
                Drop(link);
                Links.RemoveAt(i);
                var fresh = Open(pid, true);
                if (fresh != null) {
                    fresh.Attempt = attempt;
                }
            }
        }
    }

    private static string Escape(string raw) {
        var built = new StringBuilder(raw.Length + 8);
        foreach (var c in raw) {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                c == '-' || c == '.' || c == '_' || c == '~') {
                built.Append(c);
            } else {
                built.Append('%').Append(((int)c).ToString("X2"));
            }
        }

        return built.ToString();
    }

    private static Link Find(int pid) {
        foreach (var link in Links) {
            if (link.PlayerId == pid) {
                return link;
            }
        }

        return null;
    }

    private static void Drop(Link link) {
        NativeWebSocket.RtcClose(link.Handle);
        NativeWebSocket.RtcRelease(link.Handle);
    }

    private static void DropAll() {
        foreach (var link in Links) {
            Drop(link);
        }

        Links.Clear();
        Relayed.Clear();
        // credentials are named for the game we just left
        RelayUrl = null;
        foreach (var pid in new List<int>(Ghosts.Keys)) {
            Forget(pid);
        }
    }

    // A sample from a player, whichever link carried it. A ghost retired for silence is no
    // longer showing, so the first packet back brings it with it.
    private static void Feed(int who, Sample got) {
        LiveGhostSource ghost;
        var known = Ghosts.TryGetValue(who, out ghost);
        var showing = known && RandomizerGhost.Showing(ghost);
        // a title heartbeat cannot raise a ghost already taken down for standing on the title
        if (!showing && got.OnTitle) {
            return;
        }

        if (!showing) {
            ghost = new LiveGhostSource("p" + who, who, RandomizerGhost.InterpolationDelay);
            Ghosts[who] = ghost;
            RandomizerGhost.AddLive(ghost);
            Randomizer.log("ghost signal: ghost for " + who + (known ? " restored" : " up"));
        }

        ghost.Accept(got);
    }

    // the host's half of the star: every packet in goes out again on every other open link
    private static void Relay(byte[] packet, Link from) {
        foreach (var link in Links) {
            if (link != from && link.Announced && NativeWebSocket.RtcIsOpen(link.Handle)) {
                NativeWebSocket.RtcSend(link.Handle, packet, packet.Length);
            }
        }
    }

    private static void Forget(int pid) {
        LiveGhostSource ghost;
        if (Ghosts.TryGetValue(pid, out ghost)) {
            RandomizerGhost.Remove(ghost);
            Ghosts.Remove(pid);
        }
    }

    // "<game>.<player>" -- the player half is who we are on the wire.
    private static int Me {
        get {
            var id = Randomizer.SyncId;
            if (string.IsNullOrEmpty(id)) {
                return 0;
            }

            var parts = id.Split('.');
            int pid;
            return parts.Length > 1 && int.TryParse(parts[1], out pid) ? pid : 0;
        }
    }

    private static void Say(string message) {
        Randomizer.showHint(RandomizerUI.Message.InfoMessage(message, 3));
    }

    private static readonly List<Link> Links = new List<Link>();

    // peers a direct attempt has already failed against; their next link gets the relay
    private static readonly HashSet<int> Relayed = new HashSet<int>();

    // the relay as one url with its credentials folded in, or null until the server sends one
    private static string RelayUrl;

    // one ghost per player, keyed by the id their packets carry
    private static readonly Dictionary<int, LiveGhostSource> Ghosts = new Dictionary<int, LiveGhostSource>();

    private static readonly byte[] Buffer = new byte[RandomizerGhostPacket.MaxSize];

    private static bool Minded;

    // which socket the last ghosts: frame went out on, and when
    private static int announcedOn = -1;

    private static float announcedAt;

    private static int Host;

    private static float LastSent;

    private static ushort Sequence;

}

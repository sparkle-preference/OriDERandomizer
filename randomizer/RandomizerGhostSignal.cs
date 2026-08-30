using System;
using System.Collections.Generic;
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
//
// The lowest participating player id hosts and offers to everyone else; everyone else answers.
// No negotiation round, and both sides derive the same answer from the same list. The type
// rides inside the payload rather than in the frame, so the server stays a dumb relay.
public static class RandomizerGhostSignal {
    private class Link {
        public int Handle;
        public int PlayerId;
        public bool Offering;
        public bool Sent;
        public bool Announced;
        public float Since;
        public int Attempt;
        public LiveGhostSource Remote;
    }

    // How long to let a peer sit in connecting before assuming the handshake was lost. A dead
    // link is otherwise permanent: the roster still lists the player, so nothing re-offers, and
    // only restarting the game clears it.
    private const float RetryAfter = 15f;

    private const string IceServers = "stun:stun.l.google.com:19302";

    private const float SendInterval = 1f / 30f;

    public static bool Joined { get; private set; }

    // Told, not polled. The setting moves only on a settings reload or a menu toggle, and the
    // roster it feeds lives on the socket -- so a socket that closes takes participation with it,
    // and a new one has never been told.
    public static void Apply() {
        var want = RandomizerSyncManager.WsOpen && NativeWebSocket.RtcAvailable &&
            RandomizerSettings.Customization.ShowOtherPlayers.Value;
        if (want == Joined) {
            return;
        }

        Joined = want;
        if (RandomizerSyncManager.WsOpen) {
            NativeWebSocket.SendText(want ? "ghosts:1" : "ghosts:0");
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

        // anyone who left takes their peer with them
        for (var i = Links.Count - 1; i >= 0; i--) {
            if (!present.Contains(Links[i].PlayerId)) {
                Drop(Links[i]);
                Links.RemoveAt(i);
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
        // Every rejection below used to be a silent return, which is the worst possible
        // behaviour for the one frame the whole handshake depends on: the far side waits
        // forever and this side never says it threw the thing away.
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

            // A channel that was open and is not any more will never be mentioned again: the
            // roster only speaks when it changes, so a peer that restarts inside one keeps its
            // place in the list and nothing ever re-offers.
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

            // A handshake can be lost in either direction and neither side is told. Only the
            // offerer can restart one, so only the offerer retries; the answerer's link is
            // replaced when the next offer arrives.
            if (!link.Announced && now - link.Since > RetryAfter) {
                var pid = link.PlayerId;
                var attempt = link.Attempt + 1;
                Randomizer.log("ghost signal: peer " + pid + " never connected in " +
                    RetryAfter + "s, state " + NativeWebSocket.RtcGetState(link.Handle) +
                    "; " + (link.Offering ? "retrying" : "dropping and waiting for an offer"));
                var offering = link.Offering;
                Drop(link);
                Links.RemoveAt(i);
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
                link.Remote = new LiveGhostSource("p" + link.PlayerId, link.PlayerId,
                    RandomizerGhost.InterpolationDelay);
                RandomizerGhost.AddLive(link.Remote);
                Randomizer.log("ghost signal: channel open to " + link.PlayerId);
            }

            // A peer that went quiet long enough gets retired off screen, but its channel is
            // still open -- so the first packet back has to bring the ghost with it, or they
            // stay invisible until somebody rejoins by hand.
            if (link.Announced && NativeWebSocket.RtcHasMessage(link.Handle) &&
                    (link.Remote == null || !RandomizerGhost.Showing(link.Remote))) {
                link.Remote = new LiveGhostSource("p" + link.PlayerId, link.PlayerId,
                    RandomizerGhost.InterpolationDelay);
                RandomizerGhost.AddLive(link.Remote);
                Randomizer.log("ghost signal: peer " + link.PlayerId + " came back, ghost restored");
            }

            while (NativeWebSocket.RtcHasMessage(link.Handle)) {
                var packet = NativeWebSocket.RtcGetMessage(link.Handle);
                if (packet == null || link.Remote == null) {
                    continue;
                }

                Sample got;
                byte who;
                ushort seq;
                if (RandomizerGhostPacket.Decode(packet, packet.Length, out got, out who, out seq)) {
                    link.Remote.Accept(got);
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
        var handle = NativeWebSocket.RtcCreate(offering, IceServers);
        if (handle == 0) {
            Randomizer.log("ghost signal: could not create a peer for " + pid + ", " +
                NativeWebSocket.RtcLastError());
            return null;
        }

        var link = new Link { Handle = handle, PlayerId = pid, Offering = offering, Since = Time.time };
        Links.Add(link);
        Randomizer.log("ghost signal: " + (offering ? "offering to " : "answering ") + pid +
            ", handle " + handle + ", " + Links.Count + " link(s)");
        return link;
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
        link.Remote = null;
    }

    private static void DropAll() {
        foreach (var link in Links) {
            Drop(link);
        }

        Links.Clear();
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

    private static readonly byte[] Buffer = new byte[RandomizerGhostPacket.MaxSize];

    private static bool Minded;

    private static int Host;

    private static float LastSent;

    private static ushort Sequence;

}

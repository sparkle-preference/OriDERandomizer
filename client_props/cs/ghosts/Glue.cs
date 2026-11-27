// Flat views of ghost packets and the signal layer's private state, for the property side.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using Sample = RandomizerGhost.Sample;

static class GhostGlue {
    public static byte[] Encode(Sample sample, int playerId, int seq) {
        var buffer = new byte[RandomizerGhostPacket.MaxSize];
        var n = RandomizerGhostPacket.Encode(buffer, sample, (byte)playerId, (ushort)seq);
        var packet = new byte[n];
        Array.Copy(buffer, packet, n);
        return packet;
    }

    // null when Decode refuses
    public static Dictionary<string, object> Decode(byte[] from, int length) {
        Sample sample;
        byte who;
        ushort seq;
        if (!RandomizerGhostPacket.Decode(from, length, out sample, out who, out seq)) {
            return null;
        }

        var flat = Flat(sample);
        flat["player"] = (int)who;
        flat["seq"] = (int)seq;
        return flat;
    }

    public static byte[] Unclipped(byte[] packet) {
        var copy = (byte[])packet.Clone();
        RandomizerGhostPacket.Unclip(copy);
        return copy;
    }

    public static List<Dictionary<string, object>> Unpack(byte[] data) {
        return RandomizerGhostPacket.Unpack(data).Select(Flat).ToList();
    }

    public static Dictionary<string, object> Flat(Sample s) {
        return new Dictionary<string, object> {
            { "t", s.Time }, { "x", s.Position.x }, { "y", s.Position.y }, { "z", s.Position.z },
            { "rx", s.Rotation.x }, { "ry", s.Rotation.y }, { "rz", s.Rotation.z }, { "rw", s.Rotation.w },
            { "anim", s.Animation }, { "animTime", s.AnimationTime }, { "charge", s.Charge },
            { "bash", s.BashAngle }, { "bx", s.BashTarget.x }, { "by", s.BashTarget.y },
            { "ax", s.GrenadeAim.x }, { "ay", s.GrenadeAim.y }, { "wall", s.WallAim },
            { "triple", s.Triple }, { "died", s.Died }, { "sx", s.SoulLink.x }, { "sy", s.SoulLink.y },
            { "menu", s.InMenu }, { "title", s.OnTitle }, { "menuKind", s.Menu }, { "skills", (long)s.Skills },
        };
    }

    public static Quaternion Euler(float x, float y, float z) {
        return Quaternion.Euler(x, y, z);
    }
}

static class SignalGlue {
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    static object Field(string name) {
        return typeof(RandomizerGhostSignal).GetField(name, Private).GetValue(null);
    }

    static void SetField(string name, object value) {
        typeof(RandomizerGhostSignal).GetField(name, Private).SetValue(null, value);
    }

    // leave, then join as player `me` of game 7 against a fresh fake sidecar
    public static void Reset(int me) {
        Time.time = 10f;
        Time.realtimeSinceStartup = 10f;
        Randomizer.SyncId = "7." + me;
        RandomizerSettings.Customization.ShowOtherPlayers.Value = false;
        RandomizerGhostSignal.Apply();
        NativeWebSocket.Peers.Clear();
        NativeWebSocket.Texts.Clear();
        NativeWebSocket.LocalDescription = "v=0";
        RandomizerGhost.Shown.Clear();
        RandomizerGhost.OriHere = true;
        Randomizer.Lines.Clear();
        SetField("Host", 0);
        SetField("RelayExpires", 0f);
        SetField("LastSent", 0f);
        SetField("Sequence", (ushort)0);
        RandomizerSettings.Customization.ShowOtherPlayers.Value = true;
        RandomizerGhostSignal.Apply();
    }

    public static void Frame(float dt) {
        Time.time += dt;
        NativeWebSocket.Polls = 0;
        RandomizerGhostSignal.Update();
    }

    // a packet from `who` arrives on the link to `pid`, opened, and a frame passes
    public static void Hear(int pid, int who) {
        foreach (var link in (IList)Field("Links")) {
            var t = link.GetType();
            if ((int)t.GetField("PlayerId").GetValue(link) == pid) {
                var peer = NativeWebSocket.Peers[(int)t.GetField("Handle").GetValue(link)];
                peer.Open = true;
                Sample sample;
                RandomizerGhost.SampleLive(out sample);
                peer.Inbox.Enqueue(GhostGlue.Encode(sample, who, 0));
            }
        }

        Frame(1f / 30f);
    }

    public static Dictionary<string, object> State() {
        var links = new List<object>();
        foreach (var link in (IList)Field("Links")) {
            var t = link.GetType();
            Func<string, object> f = n => t.GetField(n).GetValue(link);
            var handle = (int)f("Handle");
            NativeWebSocket.Peer peer;
            NativeWebSocket.Peers.TryGetValue(handle, out peer);
            links.Add(new Dictionary<string, object> {
                { "pid", f("PlayerId") }, { "handle", handle }, { "offering", f("Offering") },
                { "otherClips", f("OtherClips") }, { "relayed", f("ViaRelay") },
                { "remote", peer == null ? new List<string>() : peer.Remote.ToList() },
                { "ice", peer == null ? null : peer.Ice },
            });
        }

        var released = NativeWebSocket.Peers.Values.Where(p => p.Released).Select(p => p.Handle).OrderBy(h => h).ToList();

        var ghosts = ((IDictionary)Field("Ghosts")).Keys.Cast<int>().OrderBy(k => k).ToList();
        return new Dictionary<string, object> {
            { "joined", RandomizerGhostSignal.Joined },
            { "host", Field("Host") },
            { "roster", ((List<int>)Field("Roster")).ToList() },
            { "links", links },
            { "relayUrl", Field("RelayUrl") },
            { "relayExpires", Field("RelayExpires") },
            { "relayed", ((HashSet<int>)Field("Relayed")).OrderBy(k => k).ToList() },
            { "ghosts", ghosts },
            { "released", released },
            { "texts", NativeWebSocket.Texts.ToList() },
        };
    }
}

// A fake sidecar and the few game statics RandomizerGhostSignal reads.
using System;
using System.Collections.Generic;

public static class NativeWebSocket {
    public enum RtcState { New, Connecting, Connected, Disconnected, Failed, Closed }

    public class Peer {
        public int Handle;
        public bool Offering;
        public string Ice;
        public bool Open;
        public bool Released;
        public readonly List<string> Remote = new List<string>();
        public readonly Queue<byte[]> Inbox = new Queue<byte[]>();
        public readonly List<byte[]> Sent = new List<byte[]>();
    }

    public static bool RtcAvailable = true;
    public static readonly Dictionary<int, Peer> Peers = new Dictionary<int, Peer>();
    public static readonly List<string> Texts = new List<string>();
    public static string LocalDescription = "v=0";
    public static int Polls;
    private static int next = 100;

    public static void SendText(string data) { Texts.Add(data); }

    public static int RtcCreate(bool offerer, string iceServers) {
        var peer = new Peer { Handle = ++next, Offering = offerer, Ice = iceServers };
        Peers[peer.Handle] = peer;
        return peer.Handle;
    }

    public static bool RtcLocalReady(int handle) { return true; }

    public static string RtcLocalDescription(int handle) { return LocalDescription; }

    // type and sdp joined by a newline, so a test reads back exactly what was applied
    public static int RtcSetRemote(int handle, string type, string sdp) {
        Peer peer;
        if (!Peers.TryGetValue(handle, out peer)) {
            return -1;
        }

        peer.Remote.Add(type + "\n" + sdp);
        return 0;
    }

    public static RtcState RtcGetState(int handle) { return RtcIsOpen(handle) ? RtcState.Connected : RtcState.Connecting; }

    public static bool RtcIsOpen(int handle) {
        Peer peer;
        return Peers.TryGetValue(handle, out peer) && peer.Open && !peer.Released;
    }

    public static int RtcSend(int handle, byte[] data, int length) {
        Peer peer;
        if (!Peers.TryGetValue(handle, out peer)) {
            return -1;
        }

        var copy = new byte[length];
        Array.Copy(data, copy, length);
        peer.Sent.Add(copy);
        return length;
    }

    public static bool RtcHasMessage(int handle) {
        if (++Polls > 100000) {
            throw new Exception("spinning on RtcHasMessage");
        }

        Peer peer;
        return Peers.TryGetValue(handle, out peer) && peer.Inbox.Count > 0;
    }

    public static byte[] RtcGetMessage(int handle) {
        Peer peer;
        if (!Peers.TryGetValue(handle, out peer) || peer.Inbox.Count == 0 || peer.Inbox.Peek() == null) {
            return null;
        }

        return peer.Inbox.Dequeue();
    }

    public static void RtcClose(int handle) { }

    public static void RtcRelease(int handle) {
        Peer peer;
        if (Peers.TryGetValue(handle, out peer)) {
            peer.Released = true;
        }
    }

    public static string RtcLastError() { return "fake"; }
}

public static class RandomizerSyncManager {
    public static bool WsOpen = true;
    public static int WsGeneration = 1;
}

public static class RandomizerSettings {
    public class BoolSetting {
        public bool Value;
    }

    public static class Customization {
        public static BoolSetting ShowOtherPlayers = new BoolSetting { Value = true };
    }
}

public static class RandomizerUI {
    public class Message {
        public static Message InfoMessage(string message, float baseDuration = 1f) { return new Message(); }
    }
}

public static class RandomizerMW {
    public static string PlayerName(int pid) { return "Player " + pid; }
}

public static class PracticeController {
    public static bool Active;
}

public static class Randomizer {
    public static string SyncId;
    public static bool Sync = true;
    public static readonly List<string> Lines = new List<string>();

    public static void log(string message) { Lines.Add(message); }

    public static void showHint(RandomizerUI.Message message) { }
}

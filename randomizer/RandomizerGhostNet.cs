using UnityEngine;

// Proves the data channel path end to end without a website or a second machine: two peers in
// this process, the offer and answer handed straight across, a packet sent and checked. ICE
// finds host candidates on the local interfaces, so it needs no STUN and no internet.
//
// It is a state machine rather than a blocking call because gathering takes a moment, and it
// runs from Update like everything else here. This is the scaffolding the real transport grows
// out of -- the same exports, driven by the website instead of by itself.
public static class RandomizerGhostNet {
    private enum Step {
        Idle,
        Offering,
        Answering,
        Connecting,
        Sent,
        Done,
    }

    public static void Begin() {
        if (!NativeWebSocket.RtcAvailable) {
            Randomizer.log("rtc loopback: sidecar has no data channels");
            return;
        }

        Stop();
        Offerer = NativeWebSocket.RtcCreate(true, "");
        Answerer = NativeWebSocket.RtcCreate(false, "");
        if (Offerer == 0 || Answerer == 0) {
            Randomizer.log("rtc loopback: could not create peers, " + NativeWebSocket.RtcLastError());
            Stop();
            return;
        }

        Started = Time.time;
        At = Step.Offering;
        Randomizer.log("rtc loopback: peers " + Offerer + " and " + Answerer + ", gathering");
    }

    public static void Update() {
        if (At == Step.Idle || At == Step.Done) {
            return;
        }

        if (Time.time - Started > Timeout) {
            Randomizer.log("rtc loopback: timed out at " + At + " after " + Timeout + "s" +
                ", offerer " + NativeWebSocket.RtcGetState(Offerer) +
                ", answerer " + NativeWebSocket.RtcGetState(Answerer));
            Stop();
            return;
        }

        switch (At) {
            case Step.Offering:
                if (NativeWebSocket.RtcLocalReady(Offerer)) {
                    var offer = NativeWebSocket.RtcLocalDescription(Offerer);
                    Randomizer.log("rtc loopback: offer gathered, " + offer.Length + " bytes, " +
                        Candidates(offer) + " candidates");
                    // checked, because a self-test that ignores a failure hides the failure --
                    // this one silently passed while set_remote was returning an error
                    if (!Fed(Answerer, NativeWebSocket.RtcLocalType(Offerer), offer)) {
                        return;
                    }

                    At = Step.Answering;
                }

                break;

            case Step.Answering:
                if (NativeWebSocket.RtcLocalReady(Answerer)) {
                    var answer = NativeWebSocket.RtcLocalDescription(Answerer);
                    Randomizer.log("rtc loopback: answer gathered, " + answer.Length + " bytes, " +
                        Candidates(answer) + " candidates");
                    if (!Fed(Offerer, NativeWebSocket.RtcLocalType(Answerer), answer)) {
                        return;
                    }

                    At = Step.Connecting;
                }

                break;

            case Step.Connecting:
                if (NativeWebSocket.RtcIsOpen(Offerer)) {
                    Randomizer.log("rtc loopback: channel open after " +
                        (Time.time - Started).ToString("F2") + "s, sending a packet");
                    NativeWebSocket.RtcSend(Offerer, Probe, Probe.Length);
                    At = Step.Sent;
                }

                break;

            case Step.Sent:
                if (NativeWebSocket.RtcHasMessage(Answerer)) {
                    var got = NativeWebSocket.RtcGetMessage(Answerer);
                    var same = got != null && got.Length == Probe.Length;
                    for (var i = 0; same && i < got.Length; i++) {
                        same = got[i] == Probe[i];
                    }

                    Randomizer.log("rtc loopback: received " + (got == null ? 0 : got.Length) +
                        " bytes, " + (same ? "identical" : "DIFFERENT") + ", round trip " +
                        (Time.time - Started).ToString("F2") + "s");
                    Stop();
                }

                break;
        }
    }

    private static bool Fed(int peer, string type, string sdp) {
        if (NativeWebSocket.RtcSetRemote(peer, type, sdp) >= 0) {
            return true;
        }

        Randomizer.log("rtc loopback: description rejected, " + NativeWebSocket.RtcLastError());
        Stop();
        return false;
    }

    private static int Candidates(string sdp) {
        if (string.IsNullOrEmpty(sdp)) {
            return 0;
        }

        var found = 0;
        var at = 0;
        while ((at = sdp.IndexOf("a=candidate", at)) >= 0) {
            found++;
            at += 11;
        }

        return found;
    }

    private static void Stop() {
        if (Offerer != 0) {
            NativeWebSocket.RtcClose(Offerer);
            NativeWebSocket.RtcRelease(Offerer);
            Offerer = 0;
        }

        if (Answerer != 0) {
            NativeWebSocket.RtcClose(Answerer);
            NativeWebSocket.RtcRelease(Answerer);
            Answerer = 0;
        }

        At = Step.Done;
    }

    private const float Timeout = 20f;

    private static readonly byte[] Probe = { 1, 200, 0x40, 0x9C, 0xDE, 0xAD, 0xBE, 0xEF, 0x7F };

    private static Step At = Step.Idle;

    private static int Offerer;

    private static int Answerer;

    private static float Started;
}

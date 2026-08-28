using System.Collections.Generic;
using UnityEngine;

using Sample = RandomizerGhost.Sample;

// Where a ghost's samples come from. A recording is complete and seekable; a peer's stream
// arrives late, out of order and may stop. The renderer wants the same two things from both:
// a list in time order, and where in it to be right now.
public interface IGhostSource {
    List<Sample> Samples { get; }

    // The point in the sample timeline to draw. Not wall-clock: a live source subtracts its
    // interpolation delay here, which is the only place that delay needs to exist.
    float At { get; }

    // Finished, and the view should be torn down.
    bool Done { get; }

    // Seconds since the newest sample turned up. A recording never stalls and answers zero;
    // a peer that has gone quiet is the only thing that makes this grow.
    float Silence { get; }

    // The multiworld player this ghost is, which is what its colour comes from. Zero means
    // nobody -- your own replay -- and takes the practice blue instead.
    int PlayerId { get; }

    string Label { get; }
}

// A finished recording played from the moment it started.
public class RecordedGhostSource : IGhostSource {
    public RecordedGhostSource(List<Sample> samples, string label, float offset) {
        Recorded = samples;
        Name = label;
        Started = Time.time - offset;
    }

    public List<Sample> Samples { get { return Recorded; } }

    public float At { get { return Time.time - Started; } }

    public bool Done { get { return At >= RandomizerGhost.Length(Recorded); } }

    public float Silence { get { return 0f; } }

    public int PlayerId { get { return 0; } }

    public string Label { get { return Name; } }

    private readonly List<Sample> Recorded;

    private readonly string Name;

    private readonly float Started;
}

// A real peer. Samples arrive decoded from the wire, on the sender's clock rather than ours, so
// an offset between the two is followed packet by packet. No clock sync protocol: being wrong by
// a constant is invisible, and being wrong by a *varying* amount is what looks like stutter.
public class LiveGhostSource : IGhostSource {
    public LiveGhostSource(string label, int who, float delay) {
        Name = label;
        Who = who;
        Delay = delay;
        Arrived = Time.time;
    }

    public List<Sample> Samples { get { return Received; } }

    // Their clock, estimated, minus the interpolation delay. Offset is theirs-minus-ours, so it
    // is *added*: subtracting it lands 2x the clock difference in the past, which reads as a
    // huge lag one way and as no interpolation at all the other.
    public float At { get { return Time.time + Offset - Delay; } }

    // A peer is finished when it is retired for silence, which the coordinator decides.
    public bool Done { get { return false; } }

    public float Silence { get { return Time.time - Arrived; } }

    public int PlayerId { get { return Who; } }

    public string Label { get { return Name; } }

    public void Accept(Sample sample) {
        Arrived = Time.time;

        // Followed rather than read off one packet, which would inherit that packet's latency
        // and leave At permanently ahead of everything received -- a jump per packet, not motion.
        var implied = sample.Time - Time.time;
        if (Received.Count == 0 || Mathf.Abs(implied - Offset) > Resync) {
            Offset = implied;
        } else {
            Offset += (implied - Offset) * Follow;
        }

        if (Received.Count > 0 && sample.Time <= Received[Received.Count - 1].Time) {
            // unordered delivery is the point of the channel; a packet behind the newest one
            // has already been interpolated past and is worth nothing
            return;
        }

        Received.Add(sample);
        // Bounded, or a long session grows without limit. The view re-seeks when its cursor
        // stops making sense, so dropping from the front is safe.
        if (Received.Count > MaxSamples) {
            Received.RemoveRange(0, Received.Count - KeepSamples);
        }
    }

    // per-packet pull toward the implied offset: slow enough to ignore jitter, quick enough to
    // settle in about a second at 30 Hz
    private const float Follow = 0.05f;

    // a gap this big is a different clock, not jitter -- a reconnect or a game restart
    private const float Resync = 1f;

    // a couple of minutes at 30 Hz, trimmed back to one when it fills
    private const int MaxSamples = 4096;

    private const int KeepSamples = 2048;

    private readonly List<Sample> Received = new List<Sample>();

    private readonly string Name;

    private readonly int Who;

    private readonly float Delay;

    private float Offset;

    private float Arrived;
}

// Stands in for a networked peer using a recording as its script: samples arrive on the schedule
// a peer's would, behind an interpolation delay, and the view cannot tell the difference.
public class LoopbackGhostSource : IGhostSource {
    public LoopbackGhostSource(List<Sample> script, string label, int who, float delay) {
        Script = script;
        Name = label;
        Who = who;
        Delay = delay;
        Started = Time.time;
    }

    public List<Sample> Samples { get { return Received; } }

    // Held back by the interpolation delay, so the view is always drawing between two samples
    // that have already arrived rather than extrapolating past the newest one.
    public float At { get { return Elapsed - Delay; } }

    public bool Done { get { return Script.Count > 0 && Elapsed >= RandomizerGhost.Length(Script) + Delay; } }

    public string Label { get { return Name; } }

    public float Silence { get { return Time.time - Arrived; } }

    public int PlayerId { get { return Who; } }

    // Simulates the peer going quiet without disconnecting, which is the failure the render
    // side has to handle gracefully and the only one that is awkward to produce on demand.
    public bool Stalled;

    // Moves everything the peer would have sent by now into the received list. Real packets
    // arrive in their own time; this arrives on the script's schedule, which is the same shape.
    public void Feed() {
        if (Stalled) {
            return;
        }

        while (Next < Script.Count && Script[Next].Time <= Elapsed) {
            Received.Add(Script[Next]);
            Next++;
            Arrived = Time.time;
        }
    }

    private float Elapsed { get { return Time.time - Started; } }

    private readonly List<Sample> Script;

    private readonly List<Sample> Received = new List<Sample>();

    private readonly string Name;

    private readonly int Who;

    private readonly float Delay;

    private readonly float Started;

    private float Arrived = Time.time;

    private int Next;
}

using System.Collections.Generic;
using UnityEngine;

using Sample = RandomizerGhost.Sample;

// Where a ghost's samples come from: a finished recording or a peer's stream. The view needs a
// time-ordered list and where in it to draw.
public interface IGhostSource {
    List<Sample> Samples { get; }

    // Where in the sample timeline to draw; a live source subtracts its interpolation delay here.
    float At { get; }

    // Finished, and the view should be torn down.
    bool Done { get; }

    // Seconds since the newest sample arrived; zero for a recording.
    float Silence { get; }

    // The multiworld player, which picks the color; zero is your own replay (practice blue).
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

// A peer's stream. Samples carry the sender's clock and the offset to ours is followed per
// packet: a constant error is invisible, a varying one is stutter.
public class LiveGhostSource : IGhostSource {
    public LiveGhostSource(string label, int who, float delay) {
        Name = label;
        Who = who;
        Delay = delay;
        Arrived = Time.time;
    }

    public List<Sample> Samples { get { return Received; } }

    // Their clock minus the interpolation delay; Offset is theirs-minus-ours, so it is added.
    public float At { get { return Time.time + Offset - Delay; } }

    // Never: the coordinator retires a peer for silence.
    public bool Done { get { return false; } }

    public float Silence { get { return Time.time - Arrived; } }

    public int PlayerId { get { return Who; } }

    public string Label { get { return Name; } }

    public void Accept(Sample sample) {
        // a non-finite time would poison Offset and the timeline for good
        if (float.IsNaN(sample.Time) || float.IsInfinity(sample.Time)) {
            return;
        }

        Arrived = Time.time;

        // eased toward each packet's implied offset; a jump past Resync is a new clock and snaps
        var implied = sample.Time - Time.time;
        if (Received.Count == 0 || Mathf.Abs(implied - Offset) > Resync) {
            Offset = implied;
            // the old clock's samples would outrank every sample on the new one
            Received.Clear();
        } else {
            Offset += (implied - Offset) * Follow;
        }

        if (Received.Count > 0 && sample.Time <= Received[Received.Count - 1].Time) {
            // the channel is unordered; a packet behind the newest is already interpolated past
            return;
        }

        Received.Add(sample);
        // trimming the front is safe: the view re-seeks when its cursor falls off
        if (Received.Count > MaxSamples) {
            Received.RemoveRange(0, Received.Count - KeepSamples);
        }
    }

    // per-packet pull toward the implied offset; settles in about a second at 30 Hz
    private const float Follow = 0.05f;

    // a jump this big is a new clock (a reconnect or a restart), not jitter
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

// A recording fed on a peer's schedule, behind the interpolation delay: echoes are these.
public class LoopbackGhostSource : IGhostSource {
    public LoopbackGhostSource(List<Sample> script, string label, int who, float delay) {
        Script = script;
        Name = label;
        Who = who;
        Delay = delay;
        Started = Time.time;
    }

    // a script already under way: the replay clock starts where it is told, not now
    public LoopbackGhostSource(List<Sample> script, string label, int who, float delay, float started)
        : this(script, label, who, delay) {
        Started = started;
    }

    public List<Sample> Samples { get { return Received; } }

    // held back by the delay, so the view interpolates between samples that have arrived
    public float At { get { return Elapsed - Delay; } }

    public bool Done { get { return Script.Count > 0 && Elapsed >= RandomizerGhost.Length(Script) + Delay; } }

    public string Label { get { return Name; } }

    public float Silence { get { return Time.time - Arrived; } }

    public int PlayerId { get { return Who; } }

    // cuts the feed, as a peer going quiet without disconnecting would
    public bool Stalled;

    // Moves everything the peer would have sent by now into the received list.
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

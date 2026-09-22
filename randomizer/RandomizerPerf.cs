using UnityEngine;

// The one switch for the rando's performance changes; the sweep gate, the queue depth and
// the scroll-lock preloader all read it live.
public static class RandomizerPerf {
    public static bool On {
        get { return RandomizerSettings.QOL.PerformanceOptimizations; }
    }

    // vsync paces evenly only with a single frame queued
    public static void Apply() {
        QualitySettings.maxQueuedFrames = On ? 1 : 2;
    }
}

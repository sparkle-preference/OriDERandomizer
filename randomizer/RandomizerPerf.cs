using UnityEngine;

// Performance Optimizations: the sweep gate, the queue depth and the preloader all read it live.
public static class RandomizerPerf {
    public static bool On {
        get { return RandomizerSettings.QOL.PerformanceOptimizations; }
    }

    // vsync paces evenly only with a single frame queued
    public static void Apply() {
        QualitySettings.maxQueuedFrames = On ? 1 : 2;
    }
}

using System;
using Core;
using Game;
using UnityEngine;

public static class UberGCManager {
    private static float s_lastUnload;

    private static float s_lastCheck;

    private static float TimeSinceUnload => Time.realtimeSinceStartup - s_lastUnload;

    public static void OnGameStart() {
        var array = new object[128];
        for (var i = 0; i < 128; i++) {
            array[i] = new byte[1024];
        }

        Events.Scheduler.OnGameFixedUpdateLate.Add(Update);
    }

    public static void CollectProactiveFull() {
        Scenes.Manager.DestroyManager.DestroyAll();
        CollectResourcesIfNeeded();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
    }

    public static void CollectResourcesIfNeeded() {
        if (TimeSinceUnload > 10f) {
            var asyncOperation = Resources.UnloadUnusedAssets();
            asyncOperation.priority = 0;
            s_lastUnload = Time.realtimeSinceStartup;
        }
    }

    private static void CollectResourcesIfOutOfMem() {
    }

    private static void Update() {
        if (Time.realtimeSinceStartup - s_lastCheck > 2.5f) {
            s_lastCheck = Time.realtimeSinceStartup;
            CollectResourcesIfOutOfMem();
        }
    }
}

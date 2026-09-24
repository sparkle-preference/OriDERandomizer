using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;

// A fader scroll lock into a room still loading freezes the game until it arrives, so the far
// side of every lock within Near is requested early, async and pinned, and unpinned past Far.
public static class RandomizerPreloader {
    public const float Near = 40f;
    public const float Far = 80f;
    public const float Beyond = 6f;
    private const float Every = 0.5f;

    public static int Requests;
    public static int Released;

    private static float s_next;
    // only pins set here are released: the game pins a teleport's destination the same way
    private static readonly HashSet<SceneManagerScene> s_pinned = new HashSet<SceneManagerScene>();
    private static readonly HashSet<SceneManagerScene> s_before = new HashSet<SceneManagerScene>();
    private static readonly List<SceneManagerScene> s_done = new List<SceneManagerScene>();

    public static void Tick() {
        if (Time.realtimeSinceStartup < s_next) {
            return;
        }

        s_next = Time.realtimeSinceStartup + Every;
        var manager = Scenes.Manager;
        if (!RandomizerPerf.On) {
            // off is vanilla, so pins taken while on are handed back
            if (s_pinned.Count > 0 && manager) {
                Release(manager, null);
            }

            return;
        }

        var sein = Characters.Sein;
        var state = GameStateMachine.Instance;
        if (!manager || !sein || state == null || state.CurrentState != GameStateMachine.State.Game || !manager.AutoLoadingUnloading
            || GameController.Instance.IsLoadingGame || Time.timeScale > 2f) {
            return;
        }

        var p = sein.Position;
        var locks = ScrollLocks.All;
        for (var i = 0; i < locks.Count; i++) {
            var scrollLock = locks[i];
            if (!scrollLock || !scrollLock.gameObject.activeInHierarchy || !scrollLock.UseFader) {
                continue;
            }

            var center = scrollLock.ScrollCenter;
            var half = scrollLock.HalfScrollSize;
            // distance from Sein to the lock's line, and whether he is level with it
            float along, across;
            Vector3 beyond;
            if (scrollLock.ScrollType == CameraScrollLock.Type.Vertical) {
                across = p.y - center.y;
                along = Mathf.Abs(p.x - center.x) - half.x;
                beyond = new Vector3(Mathf.Clamp(p.x, center.x - half.x, center.x + half.x), center.y - Mathf.Sign(across) * (half.y + Beyond), 0f);
            } else {
                across = p.x - center.x;
                along = Mathf.Abs(p.y - center.y) - half.y;
                beyond = new Vector3(center.x - Mathf.Sign(across) * (half.x + Beyond), Mathf.Clamp(p.y, center.y - half.y, center.y + half.y), 0f);
            }

            if (Mathf.Abs(across) > Near || along > Near) {
                continue;
            }

            Request(manager, beyond);
        }

        Release(manager, new Rect(p.x - Far, p.y - Far, Far * 2f, Far * 2f));
    }

    private static void Request(ScenesManager manager, Vector3 at) {
        var scenes = manager.ActiveScenes;
        s_before.Clear();
        for (var i = 0; i < scenes.Count; i++) {
            if (scenes[i].PreventUnloading) {
                s_before.Add(scenes[i]);
            }
        }

        manager.AdditivelyLoadScenesAtPosition(at, true, false, true);
        Requests++;
        for (var i = 0; i < scenes.Count; i++) {
            if (scenes[i].PreventUnloading && !s_before.Contains(scenes[i])) {
                s_pinned.Add(scenes[i]);
            }
        }
    }

    // keep null releases every pin
    private static void Release(ScenesManager manager, Rect? keep) {
        s_done.Clear();
        foreach (var scene in s_pinned) {
            if (!scene.PreventUnloading || !manager.ActiveScenes.Contains(scene)) {
                s_done.Add(scene);
            } else if (!scene.KeepLoadedForCheckpoint && scene.MetaData != null
                    && (keep == null || !scene.MetaData.IsInsideSceneBounds(keep.Value))) {
                scene.PreventUnloading = false;
                Released++;
                s_done.Add(scene);
            }
        }

        for (var i = 0; i < s_done.Count; i++) {
            s_pinned.Remove(s_done[i]);
        }
    }
}

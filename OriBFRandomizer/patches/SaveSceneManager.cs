using System;
using System.Collections.Generic;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    public static class SaveSceneManagerExt
    {
        public static Dictionary<SaveSceneManager, SceneRoot> SceneRoots =
            new Dictionary<SaveSceneManager, SceneRoot>();

        public static Dictionary<SaveSceneManager, Action<SceneRoot>> BootstrapHooks =
            new Dictionary<SaveSceneManager, Action<SceneRoot>>();
    }

    [HarmonyPatch(typeof(SaveSceneManager))]
    public static class SaveSceneManagerPatches
    {
        [HarmonyPatch(nameof(SaveSceneManager.Load), new[]{typeof(SaveScene)})]
        [HarmonyPostfix]
        public static void Bootstrap(SaveSceneManager __instance)
        {
            if (SaveSceneManagerExt.BootstrapHooks.TryGetValue(__instance, out var hook))
            {
                hook(SaveSceneManagerExt.SceneRoots[__instance]);
            }

        }
        
        [HarmonyPatch(nameof(SaveSceneManager.Load), new[]{typeof(SaveScene), typeof(HashSet<SaveSerialize>)})]
        [HarmonyPostfix]
        public static void Bootstrap2(SaveSceneManager __instance)
        {
            if (SaveSceneManagerExt.BootstrapHooks.TryGetValue(__instance, out var hook))
            {
                hook(SaveSceneManagerExt.SceneRoots[__instance]);
            }

        }
    }
}
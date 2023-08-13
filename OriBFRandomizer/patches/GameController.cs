using Core;
using Game;
using HarmonyLib;
using UnityEngine;
using Logger = BFModLoader.ModLoader.Logger;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(GameController))]
    public static class GameControllerPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GameController.FixedUpdate))]
        static void RandomizerBootstrapFUPatch()
        {
            if (Scenes.Manager)
                RandomizerBootstrap.FixedUpdate();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(GameController.Update))]
        static void RandomizerBootstrapUPatch()
        {
            Randomizer.Update();
            if (UI.SeinUI && !UI.SeinUI.ShowUI)
                UI.SeinUI.ShowUI = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(GameController.OnApplicationQuit))]
        static void RandomizerOnApplicationQuit()
        {
            Randomizer.OnApplicationQuit();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(GameController.SetupGameplay))]
        static void RandomizerSetupGameplay()
        {
            Randomizer.SetupNewGame();
        }

        /*[HarmonyPostfix]
        [HarmonyPatch(nameof(GameController.WarmUpResources))]
        static void RandomizerWarmup()
        {
            Randomizer.initialize();
        }*/
        
        [HarmonyPostfix]
        [HarmonyPatch(nameof(GameController.Awake))]
        static void RandomizerWarmupOnce()
        {
            Randomizer.InitializeOnce();
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GameController.PerformSaveGameSequence))]
        static void Save()
        {
            RandomizerStatsManager.OnSave(false);
        }

        private static int CurVsyncValue;

        [HarmonyPostfix]
        [HarmonyPatch("OnApplicationFocus")]
        static void VSyncFix(bool focusStatus)
        {
            if (focusStatus && CurVsyncValue != 0)
            {
                QualitySettings.vSyncCount = CurVsyncValue;
                CurVsyncValue = 0;
            }
            else if (QualitySettings.vSyncCount != 0)
            {
                CurVsyncValue = QualitySettings.vSyncCount;
                QualitySettings.vSyncCount = 0;
            }
        }
    }
}
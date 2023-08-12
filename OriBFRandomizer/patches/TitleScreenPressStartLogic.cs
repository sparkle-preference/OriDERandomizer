using System;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(TitleScreenPressStartLogic))]
    public static class TitleScreenPressStartLogicPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(TitleScreenPressStartLogic.FixedUpdate))]
        public static bool FixedUpdatePatch(TitleScreenPressStartLogic __instance)
        {
            XboxLiveController.Instance.StartPressedOnMainMenu(new Action(__instance.OnStartPressedCallback));
            return false;
        }
    }
}
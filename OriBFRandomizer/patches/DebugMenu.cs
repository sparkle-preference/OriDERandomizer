using Game;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(DebugMenu))]
    public static class DebugMenuPatches
    {
        [HarmonyPatch("FixedUpdate")]
        [HarmonyPrefix]
        static bool FixedUpdatePrefix()
        {
            return !UI.MainMenuVisible;
        }
    }
}
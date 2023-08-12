using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinDeathsManager))]
    public static class SeinDeathsManagerPatches
    {
        [HarmonyPatch("OnDeath")]
        [HarmonyPrefix]
        static void OnDeathHook()
        {
            Randomizer.OnDeath();
        }
    }
}
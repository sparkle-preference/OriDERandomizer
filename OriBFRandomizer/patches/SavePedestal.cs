using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SavePedestal))]
    public static class SavePedestalPatches
    {

        [HarmonyPatch("SaveOnPedestal")]
        [HarmonyPrefix]
        public static void SaveHook()
        {
            RandomizerStatsManager.OnSave();
        }
    }
}
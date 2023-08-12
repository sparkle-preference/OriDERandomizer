using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SaveGameController))]
    public static class SaveGameControllerPatches
    {
        [HarmonyPatch(nameof(SaveGameController.PerformSave))]
        [HarmonyPrefix]
        public static void OnSaveHook(SaveGameController __instance)
        {
        
            if (!__instance.CanPerformSave())
            {
                return;
            }
            Randomizer.OnSave();
        }

    }
}
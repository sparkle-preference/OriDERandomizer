using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(DifficultyToggler))]
    [HarmonyPatch("get_ToggleOptions")]
    public static class DifficultyTogglerPatch
    {
        static bool Prefix(ref string[] __result)
        {
            __result = new[]
            {
                RandomizerText.DifficultyOverrides.Easy.NameOverride.ToString(),
                RandomizerText.DifficultyOverrides.Normal.NameOverride.ToString(),
                RandomizerText.DifficultyOverrides.Hard.NameOverride.ToString(),
                RandomizerText.DifficultyOverrides.OneLife.NameOverride.ToString()
            };
            return false;
        }
    }    
}


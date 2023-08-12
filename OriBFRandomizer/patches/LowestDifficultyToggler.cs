using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(LowestDifficultyToggler))]
    [HarmonyPatch("get_ToggleOptions")]
    class LowestDifficultyTogglerPatch
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
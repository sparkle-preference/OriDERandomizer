using Game;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    public static class SeinSoulFlamePatches
    {
        [HarmonyPatch(typeof(SeinSoulFlame))]
        [HarmonyPostfix]
        [HarmonyPatch("CastSoulFlame")]
        public static void SaveHook()
        {
            RandomizerBonusSkill.LastSoulLink = Characters.Sein.Position;
            RandomizerStatsManager.OnSave();
        }
    }
}
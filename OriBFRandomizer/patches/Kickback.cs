using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(Kickback))]
    public static class KickbackPatches
    {
        [HarmonyPatch("AdvanceTime")]
        [HarmonyPostfix]
        static void FixedUpdatePostfix(ref float ___m_kickbackTimeRemaining)
        {
            ___m_kickbackTimeRemaining += (1 - RandomizerBonusSkill.TimeScale(Time.deltaTime));
        }
    }
}
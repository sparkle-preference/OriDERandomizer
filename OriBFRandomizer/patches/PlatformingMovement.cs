using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(PlatformingMovement))]
    public static class PlatformingMovementPatches{

        [HarmonyPatch(nameof(PlatformingMovement.FixedUpdate))]
        [HarmonyPostfix]
        public static void TimewarpPatch(Rigidbody ___m_rigidbody)
        {
            ___m_rigidbody.velocity *= RandomizerBonusSkill.TimeScale(1f);
        }

    }
}
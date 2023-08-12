using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(TraceGroundMovement))]
    public static class TraceGroundMovementPatches{

        [HarmonyPatch(nameof(TraceGroundMovement.FixedUpdate))]
        [HarmonyPostfix]
        public static void TimewarpPatch(TraceGroundMovement __instance, Rigidbody ___m_rigidbody)
        {
            ___m_rigidbody.velocity *= RandomizerBonusSkill.TimeScale(1f);
        }

    }
}

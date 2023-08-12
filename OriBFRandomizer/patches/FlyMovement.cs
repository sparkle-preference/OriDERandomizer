using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(FlyMovement))]
    public static class FlyMovementPatches
    {
        [HarmonyPatch("FixedUpdate")]
        [HarmonyPostfix]
        static void FixedUpdatePostfix(Rigidbody ___m_rigidbody)
        {
            ___m_rigidbody.velocity = RandomizerBonusSkill.TimeScale(___m_rigidbody.velocity);
        }
    }
}
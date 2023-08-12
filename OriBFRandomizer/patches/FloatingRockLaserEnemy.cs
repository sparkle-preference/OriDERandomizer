using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(FloatingRockLaserEnemy))]
    [HarmonyPatch("UpdateLaserDirection")]
    public static class FloatingRockLaserEnemyTimewarpPatch
    {
        static void Postfix(FloatingRockLaserEnemy __instance, ref Vector3 ___m_laserDirection)
        {
            ___m_laserDirection *= RandomizerBonusSkill.TimeScale(1f);
            __instance.transform.eulerAngles *= RandomizerBonusSkill.TimeScale(1f);
        }
    }
}
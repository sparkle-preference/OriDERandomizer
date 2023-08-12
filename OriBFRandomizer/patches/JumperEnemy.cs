using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(JumperEnemy))]
    [HarmonyPatch("FixedUpdate")]
    public static class JumperEnemyPatches
    {
        static bool Prefix(JumperEnemy __instance)
        {
            if (__instance.IsSuspended)
                return true;
            
            __instance.PlatformMovement.LocalSpeedY += Time.deltaTime * __instance.Settings.Gravity;
            __instance.PlatformMovement.LocalSpeedY -= RandomizerBonusSkill.TimeScale(Time.deltaTime) * __instance.Settings.Gravity;
            return true;
        }
    }
}
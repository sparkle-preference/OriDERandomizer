using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SpitterEnemy))]
    
    public static class SpitterEnemyPatches
    {
        [HarmonyPatch("FixedUpdate")]
        [HarmonyPostfix]
        static void SpitterTimewarpPatch(SpitterEnemy __instance)
        {
            if (__instance.IsSuspended)
                return;
            __instance.PlatformMovement.LocalSpeedY += (1 - RandomizerBonusSkill.TimeScale(Time.deltaTime))* __instance.Settings.Gravity;
        }

        [HarmonyPatch("FixedUpdate")]
        [HarmonyPrefix]
        static void BingoWillhelmScreamGoal(SpitterEnemy __instance, bool ___m_hasEnteredZone)
        {
            if (__instance.WilhelmScreamZoneRectanglesContain(__instance.transform.position) && !___m_hasEnteredZone && __instance.EnterZoneAction)
                BingoController.OnScream();
        }
    }
}
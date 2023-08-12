using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(DashOwlDashState))]
    public static class DashOwlDashStatePatches
    {
        [HarmonyPatch("UpdateState")]
        [HarmonyPrefix]
        static bool UpdateStatePrefix(DashOwlDashState __instance, DashOwlEnemy ___DashOwl, Vector3 ___m_dashTargetOffset)
        {
            ___DashOwl.FlyMovement.Kickback.Stop();
            Vector3 a = ___m_dashTargetOffset * (___DashOwl.Settings.DashCurve.Evaluate(__instance.CurrentStateTime + Time.deltaTime) - ___DashOwl.Settings.DashCurve.Evaluate(__instance.CurrentStateTime));
            ___DashOwl.FlyMovement.Velocity = ((Time.deltaTime != 0f) ? (a / Time.deltaTime) : Vector3.zero);
            return false;
        }
    }
}
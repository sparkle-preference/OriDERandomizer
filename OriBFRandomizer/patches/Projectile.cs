using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(Projectile))]
    public static class ProjectilePatches
    {
        [HarmonyPatch("FixedUpdate")]
        [HarmonyPrefix]
        static bool FixedUpdatePrefix(Projectile __instance)
        {
            if (__instance.IsSuspended)
                return true;

            __instance.SpeedVector += Vector3.down * Time.deltaTime * __instance.Gravity;
            __instance.SpeedVector -=
                Vector3.down * RandomizerBonusSkill.TimeScale(Time.deltaTime) * __instance.Gravity;
            return true;
        }

        [HarmonyPatch("UpdateVelocity")]
        [HarmonyPostfix]
        static void UpdateVelocityPostfix(Rigidbody ___Rigidbody)
        {
            ___Rigidbody.velocity = RandomizerBonusSkill.TimeScale(___Rigidbody.velocity);
        }


        [HarmonyPatch("UpdateSpeedAndDirection")]
        [HarmonyPostfix]
        static void UpdateSpeedAndDirectionPostfix(Projectile __instance)
        {
            __instance.Speed = RandomizerBonusSkill.TimeScale(__instance.Speed);
        }
    }
}
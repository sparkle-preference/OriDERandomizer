using HarmonyLib;
using OriBFRandomizer.settings;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    public static class ClimbPatches
    {
        [HarmonyPatch(typeof(SeinGrabWall))]
        [HarmonyPostfix]
        [HarmonyPatch("get_WantToGrab")]
        public static void InvertClimbPatch(ref bool __result)
        {
            __result ^= ControlSettings.INVERT_CLIMB.Value;
        }

        [HarmonyPatch(typeof(SeinGrabWall))]
        [HarmonyPrefix]
        [HarmonyPatch("UpdateCharacterState")]
        public static void UpdateGrabForgiveness(SeinGrabWall __instance)
        {
            if (!__instance.IsGrabbing && __instance.CanGrab && __instance.WantToGrab)
            {
                Randomizer.ApplyGrabForgiveness();
            }
        }

        [HarmonyPatch(typeof(SeinGrabWall))]
        [HarmonyPostfix]
        [HarmonyPatch("get_CanGrab")]
        public static void Velocity(ref bool __result)
        {
            __result |= Randomizer.DoesGrabForgivenessExpire(Time.deltaTime);
        }

        [HarmonyPatch(typeof(SeinGrabWall))]
        [HarmonyPostfix]
        [HarmonyPatch("UpdateGrabbing")]
        public static void Velocity(SeinGrabWall __instance)
        {
            __instance.PlatformMovement.LocalSpeedY *= RandomizerBonus.Veloscale;
        }


        [HarmonyPatch(typeof(SeinEdgeClamber))]
        [HarmonyPrefix]
        [HarmonyPatch("PerformEdgeClamber")]
        public static void AccessibilitySlowVaultPatch(SeinEdgeClamber __instance)
        {
            if (!RandomizerSettings.Controls.SlowClimbVault)
                return;

            if (__instance.PlatformMovement.HasWallLeft)
            {
                __instance.PlatformMovement.LocalSpeedX = Mathf.Min(__instance.PlatformMovement.LocalSpeedX,
                    __instance.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed * -0.225f);
            }
            else
            {
                __instance.PlatformMovement.LocalSpeedX = Mathf.Max(__instance.PlatformMovement.LocalSpeedX,
                    __instance.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed * 0.225f);
            }
        }
    }
}
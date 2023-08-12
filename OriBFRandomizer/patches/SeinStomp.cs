using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    public static class StompPatches
    {
        [HarmonyPatch(typeof(SeinStomp))]
        [HarmonyPatch(nameof(SeinStomp.UpdateStompDownState))]
        [HarmonyPostfix]
        public static void VelocityPatch(SeinStomp __instance)
        {
            if (__instance.State.StompDown == __instance.Logic.CurrentState)
                __instance.PlatformMovement.LocalSpeed +=
                    new Vector2(0f, -__instance.StompSpeed * 0.2f * (float) RandomizerBonus.Velocity());
        }
    }
}
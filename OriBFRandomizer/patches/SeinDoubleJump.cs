using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinDoubleJump))]
    public static class SeinDoubleJumpPatches
    {
        [HarmonyPatch("get_ExtraJumpsAvailable")]
        [HarmonyPrefix]
        public static bool ExtraJumpsAvailablePatch(SeinDoubleJump __instance, ref int __result)
        {
            int num = RandomizerBonus.DoubleJumpUpgrades();
            if (CheatsHandler.InfiniteDoubleJumps)
            {
                __result = 999999;
            }
            else if (__instance.Sein.PlayerAbilities.DoubleJumpUpgrade.HasAbility)
            {
                __result = 2 + num;
            }
            else __result = 1 + num;

            return false;
        }

        [HarmonyPatch(nameof(SeinDoubleJump.PerformDoubleJump))]
        [HarmonyPostfix]
        public static void VelocityPatch(SeinDoubleJump __instance)
        {
            __instance.PlatformMovement.LocalSpeedY = __instance.JumpStrength * RandomizerBonus.DoubleJumpscale;
        }
    }
}
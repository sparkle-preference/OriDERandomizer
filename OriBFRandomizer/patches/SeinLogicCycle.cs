using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinLogicCycle))]
    public static class SeinLogicCyclePatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("get_AllowDash")]
        public static bool UnderwaterDashPatch(SeinLogicCycle __instance, ref bool __result)
        {
            __result = !RandomizerBonus.Swimming() && !__instance.Sein.Controller.IsGrabbingLever &&
                       !__instance.Sein.Controller.IsCarrying && !__instance.Sein.Controller.IsPlayingAnimation &&
                       !__instance.Sein.Controller.IsPushPulling && !__instance.Sein.Controller.IsAimingGrenade &&
                       !__instance.Sein.Controller.IsStomping && !__instance.Sein.Controller.IsBashing &&
                       !SeinAbilityRestrictZone.IsInside(SeinAbilityRestrictZoneMode.AllAbilities) &&
                       !SeinAbilityRestrictZone.IsInside(SeinAbilityRestrictZoneMode.Dash) &&
                       __instance.Sein.Controller.CanMove;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("get_AllowGrenade")]
        public static bool UnderwaterGrenadePatch(SeinLogicCycle __instance, ref bool __result)
        {
            __result = !RandomizerBonus.Swimming() && !__instance.Sein.Controller.IsGrabbingLever &&
                       !__instance.Sein.Controller.IsCarrying && !__instance.Sein.Controller.IsPlayingAnimation &&
                       !__instance.Sein.Controller.IsPushPulling &&
                       !SeinAbilityRestrictZone.IsInside(SeinAbilityRestrictZoneMode.AllAbilities) &&
                       __instance.Sein.Controller.CanMove && !__instance.Sein.Controller.IsBashing &&
                       !__instance.Sein.Controller.IsStandingOnEdge && !__instance.Sein.Controller.IsDashing;

            return false;
        }
    }
}
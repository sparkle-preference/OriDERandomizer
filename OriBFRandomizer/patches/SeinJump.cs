using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinJump))]
    public static class SeinJumpPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("CalculateSpeedFromHeight")]
        public static bool CalculateSpeedFromHeightAndRando(SeinJump __instance, ref float __result, float height)
        {
            __result = PhysicsHelper.CalculateSpeedFromHeight(height,
                __instance.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityStrength);
            return false;
        }
    }
}
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinWallJump))]
    public static class SeinWallJumpPatches
    {
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPostfix]
        static void RegisterJumpEvent(SeinWallJump __instance)
        {
            __instance.OnWallJumpEvent += ev =>
            {
                __instance.PlatformMovement.LocalSpeedX *= RandomizerBonus.Jumpscale;
                __instance.PlatformMovement.LocalSpeedY *= RandomizerBonus.Jumpscale;
            };
        }
    }
}
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(RammingRunningState))]
    public static class RammingRunningStatePatches
    {
        [HarmonyPatch("UpdateState")]
        [HarmonyPostfix]
        static void RammingRunningStatePostfix(GroundEnemy ___GroundEnemy)
        {
            ___GroundEnemy.PlatformMovement.LocalSpeedX += RandomizerBonusSkill.TimeScale(1f);
        }
    }
}
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(Entity))]
    public static class EntityPatches
    {
        [HarmonyPatch("FixedUpdate")]
        [HarmonyPrefix]
        static void FixedUpdatePrefix(Entity __instance)
        {
            if (__instance is Enemy enemy)
            {
                enemy.Animation.Animator.TextureAnimator.SpeedMultiplier =
                    RandomizerBonusSkill.TimeScale(1f);
            }
        }
    }
}
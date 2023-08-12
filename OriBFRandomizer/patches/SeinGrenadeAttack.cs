using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinGrenadeAttack))]
    public static class SeinGrenadeAttackPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(SeinGrenadeAttack.UpdateNormal))]
        public static void ResetAimPatch1(SeinGrenadeAttack __instance)
        {
            if (RandomizerRebinding.ResetGrenadeAim.OnPressed)
            {
                var method = AccessTools.Method(typeof(SeinGrenadeAttack), "ResetAimToDefault");
                method.Invoke(__instance, new object[] { });
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(SeinGrenadeAttack.UpdateAiming))]
        public static void ResetAimPatch2(SeinGrenadeAttack __instance)
        {
            if (RandomizerRebinding.ResetGrenadeAim.OnPressed)
            {
                var method = AccessTools.Method(typeof(SeinGrenadeAttack), "ResetAimToDefault");
                method.Invoke(__instance, new object[] { });
            }
        } 
    
        [HarmonyPostfix]
        [HarmonyPatch("get_EnergyCostFinal")]
        public static void FreeNadePatch(ref float __result)
        {
            __result = 0;
        }
    }
}
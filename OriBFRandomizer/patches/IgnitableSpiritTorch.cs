using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(IgnitableSpiritTorch))]
    public static class IgnitableSpiritTorchPatches
    {

        [HarmonyPatch("Light")]
        [HarmonyPostfix]
        public static void LightBingoPatch(IgnitableSpiritTorch __instance, bool byGrenade)
        {
            BingoController.OnLanternLit(__instance.MoonGuid, byGrenade);
        }
    }
}
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(GetWorldEventCondition))]
    [HarmonyPatch("Validate")]
    public static class GetWorldEventConditionPatch
    {
        public static bool Prefix(GetWorldEventCondition __instance, ref bool __result)
        {
            if (__instance.WorldEvents.UniqueID == 26 && Randomizer.Inventory.FinishedGinsoEscape)
            {
                __result = __instance.State != 21;
                return false;
            }

            return true;
        }
    }
}
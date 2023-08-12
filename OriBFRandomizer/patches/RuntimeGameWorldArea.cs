using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(RuntimeGameWorldArea))]
    public static class RuntimeGameWorldAreaPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("UpdateCompletionAmount")]
        public static bool CalculateSpeedFromHeightAndRando(RuntimeGameWorldArea __instance,
            ref float ___m_completionAmount)
        {
            int num = RandomizerStatsManager.PickupCounts[__instance.Area.AreaIdentifier];
            int num2 = RandomizerStatsManager.GetObtainedPickupCount(__instance.Area.AreaIdentifier);
            if (RandomizerTrackedDataManager.MapBitsByArea.ContainsKey(__instance.Area.AreaIdentifier))
            {
                num++;
                if (RandomizerTrackedDataManager.GetMapstone(__instance.Area.AreaIdentifier))
                {
                    num2++;
                }
            }

            ___m_completionAmount = (float) num2 / (float) num;
            return false;
        }
    }
}
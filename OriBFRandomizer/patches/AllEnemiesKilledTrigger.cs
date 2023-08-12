namespace OriBFRandomizer.patches
{
    using HarmonyLib;

    [HarmonyPatch(typeof(AllEnemiesKilledTrigger))]
    [HarmonyPatch("Increment")]
    class AllEnemiesKilledTriggerBingoPurpleDoorPatch
    {
        static bool Prefix(AllEnemiesKilledTrigger __instance, int ___m_counter)
        {
            if (___m_counter + 1 == __instance.TriggerOnCounter)
                BingoController.OnPurpleDoor(__instance.MoonGuid);

            return true;
        }
    }
}
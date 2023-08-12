using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(EnergyDoor))]
    [HarmonyPatch("FixedUpdate")]
    public static class EnergyDoorPatches
    {
        static void Postfix(EnergyDoor __instance)
        {
            if (__instance.AmountOfEnergyUsed == __instance.AmountOfEnergyRequired)
            {
                BingoController.OnEnergyDoor(__instance.MoonGuid);
            }
        }
    }
}
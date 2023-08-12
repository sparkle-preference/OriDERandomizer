using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinEnergy))]
    public static class SeinEnergyPatches
    {
        [HarmonyPatch("Gain")]
        [HarmonyPrefix]
        static bool GainEnergyPatch(SeinEnergy __instance)
        {
            return __instance.Current < __instance.Max;
        }
    
        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        static bool UpdatePatch(SeinEnergy __instance)
        {
            __instance.MinVisual = Mathf.MoveTowards(__instance.MinVisual, (float)((int)(__instance.Current * 4f)) / 4f, Time.deltaTime);
            __instance.MaxVisual = Mathf.MoveTowards(__instance.MaxVisual, (float)((int)(__instance.Current * 4f)) / 4f, Time.deltaTime);
            return false;
        }
    
        [HarmonyPatch("RestoreAllEnergy")]
        [HarmonyPrefix]
        static bool RestoreAllEnergyPatch(SeinEnergy __instance)
        {
            return __instance.Current < __instance.Max;
        }


    }
}
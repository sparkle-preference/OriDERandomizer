using Game;
using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(DoorWithSlots))]
    public static class DoorWithSlotsPatches
    {
        [HarmonyPatch("get_SeinInRange")]
        [HarmonyPrefix]
        static bool OpenFromBothSidesPatch(ref bool __result, DoorWithSlots __instance, bool ___m_opensOnLeftSide, Transform ___m_transform)
        {
            __result = !__instance.OriHasTargets && __instance.DistanceToSein <= __instance.Radius &&
                       (Randomizer.OpenMode ||
                        ((!___m_opensOnLeftSide ||
                          ___m_transform.position.x >= Characters.Sein.Position.x) &&
                         (___m_opensOnLeftSide ||
                          ___m_transform.position.x <= Characters.Sein.Position.x)));
            return false;
        }

        [HarmonyPatch("FixedUpdate")]
        [HarmonyPostfix]
        static void UpdateBingoState(DoorWithSlots __instance)
        {
            if (__instance.NumberOfOrbsUsed == __instance.NumberOfOrbsRequired)
            {
                BingoController.OnKSDoor(__instance.MoonGuid);
            }
        }
    }
}
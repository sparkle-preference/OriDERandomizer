using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(StompPost))]
    public static class StompPostPatches
    {
        [HarmonyPatch(nameof(StompPost.OnRecieveDamage))]
        [HarmonyPostfix]
        static void Postfix(StompPost __instance, float ___m_distanceStompedIntoGround)
        {
            if (Mathf.Approximately(___m_distanceStompedIntoGround, __instance.StompIntoGroundAmount))
                BingoController.OnStompPost(__instance.MoonGuid);
        }
    }
}
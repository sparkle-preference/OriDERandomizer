using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinCharacter))]
    public static class SeinCharacterPatches
    {
        [HarmonyPatch("Activate")]
        [HarmonyPostfix]
        static void Broadcast(SeinCharacter __instance, bool active)
        {
            if (active)
            {
                __instance.gameObject.BroadcastMessage("SetReferenceToSein", __instance,
                    SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
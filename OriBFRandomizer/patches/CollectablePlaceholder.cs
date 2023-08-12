using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(CollectablePlaceholder))]
    public static class CollectablePlaceholderPatches
    {
        [HarmonyPatch("FixedUpdate")]
        [HarmonyPrefix]
        static void FixedUpdatePrefix(CollectablePlaceholder __instance, bool ___m_collected, object ___m_instance)
        {
            if (!___m_collected && RandomizerLocationManager.IsPickupCollected(__instance.MoonGuid) &&
                ___m_instance == null)
            {
                __instance.OnCollect();
            }
        }

        [HarmonyPatch("Instantiate")]
        [HarmonyPostfix]
        static void FixChildGUID(CollectablePlaceholder __instance, GameObject ___m_instance)
        {
            PickupBase componentInChildren = ___m_instance.GetComponentInChildren<PickupBase>();
            componentInChildren.MoonGuid = __instance.MoonGuid;
        }
    }
}
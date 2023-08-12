using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(PickupBase))]

    public static class PickupBasePatches
    {
        [HarmonyPatch("FixedUpdate")]
        [HarmonyPostfix]
        static void FixedUpdatePrefix(PickupBase __instance)
        {
            if (!__instance.IsCollected && RandomizerLocationManager.IsPickupCollected(__instance.MoonGuid))
            {
                __instance.IsCollected = true;
                if (__instance.OnCollectedAction != null)
                {
                    __instance.OnCollectedAction.PerformInstantly(null);
                }
                __instance.OnCollectedEvent();
                if (__instance.DestroyOnCollect)
                {
                    InstantiateUtility.Destroy(__instance.DestroyTarget);
                }
                else
                {
                    __instance.gameObject.SetActive(false);
                }
            }
        }
    }
}
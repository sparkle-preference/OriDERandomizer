using Game;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(MapStone))]
    [HarmonyPatch("FixedUpdate")]
    public static class MapStonePatches
    {
        static bool Prefix(MapStone __instance, ref int __state)
        {
            __state = Characters.Sein.Inventory.MapStones;
            MapStone.State currentState = __instance.CurrentState;
            if (currentState == MapStone.State.Activated ||
                !RandomizerLocationManager.IsPickupCollected(__instance.MoonGuid)) return true;
            if (currentState == MapStone.State.Highlighted)
            {
                __instance.Unhighlight();
            }
            if (__instance.OnOpenedAction)
            {
                __instance.OnOpenedAction.Perform(null);
            }
            __instance.CurrentState = MapStone.State.Activated;
            return false;

        }

        static void Postfix(MapStone __instance, int __state)
        {
            if (Characters.Sein.Inventory.MapStones == __state) return;
            RandomizerLocationManager.GivePickup(__instance.MoonGuid);
            GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
        }
    }
}
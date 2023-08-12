using Game;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(GetAbilityPedestal))]
    public static class GetAbilityPedestalPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetAbilityPedestal.UpdateStates))]
        public static bool RandoTree(GetAbilityPedestal __instance)
        {
            GetAbilityPedestal.States currentState = __instance.CurrentState;
            if (currentState != GetAbilityPedestal.States.Completed &&
                RandomizerLocationManager.IsPickupCollected(__instance.MoonGuid))
            {
                AccessTools.Method(__instance.GetType(), "ChangeState", new[] {typeof(GetAbilityPedestal.States)}).Invoke(
                    __instance, new object[]
                    {
                        GetAbilityPedestal.States.Completed
                    });

                __instance.ActivatePedestalSequence.PerformInstantly(null);
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetAbilityPedestal.ActivatePedestal))]
        public static bool GrantPickup(GetAbilityPedestal __instance)
        {
            __instance.StartCoroutine(__instance.MoveSeinToCenterSmoothly());
            if (Characters.Sein.Abilities.Carry && Characters.Sein.Abilities.Carry.CurrentCarryable != null)
            {
                Characters.Sein.Abilities.Carry.CurrentCarryable.Drop();
            }

            Characters.Sein.Mortality.Health.RestoreAllHealth();
            Characters.Sein.Energy.RestoreAllEnergy();
            Characters.Sein.Controller.PlayAnimation(__instance.GetAbilityAnimation);
            RandomizerLocationManager.GivePickup(__instance.MoonGuid);
            AccessTools.Method(__instance.GetType(), "ChangeState", new[] {typeof(GetAbilityPedestal.States)}).Invoke(
                __instance, new object[]
                {
                    GetAbilityPedestal.States.Completed
                });
            __instance.ActivatePedestalSequence.Perform(null);
            GameWorld.Instance.CurrentArea.DirtyCompletionAmount();

            return false;
        }
    }
}
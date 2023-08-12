using fsm;
using Game;
using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinChargeFlameAbility))]
    public static class SeinChargeFlameAbilityPatches
    {
        [HarmonyPatch(nameof(SeinChargeFlameAbility.UpdateCharacterState))]
        [HarmonyPrefix]
        public static void UpdateEnergyCost(SeinChargeFlameAbility __instance)
        {
            __instance.EnergyCost = Characters.Sein.PlayerAbilities.ChargeFlameEfficiency.HasAbility ? 0f : 0.5f;
        }

        [HarmonyPatch(nameof(SeinChargeFlameAbility.UpdateStartState))]
        [HarmonyPrefix]
        public static bool UpdateStartState(SeinChargeFlameAbility __instance, GameObject ___m_chargeFlameChargeEffect,
            SeinCharacter ___m_sein, StateMachine ___Logic)
        {
            if (___m_chargeFlameChargeEffect)
            {
                InstantiateUtility.Destroy(___m_chargeFlameChargeEffect);
            }

            if (___m_sein.Controller.IsBashing)
            {
                return false;
            }

            bool flag = __instance.ChargeFlameButton.OnPressed && !__instance.ChargeFlameButton.Used;
            if (RandomizerSettings.Controls.Autofire == RandomizerSettings.AutofireMode.Hold &&
                !RandomizerRebinding.SuppressAutofire.Pressed)
            {
                flag = false;
            }

            if (flag && ___m_sein.PlayerAbilities.ChargeFlame.HasAbility && !___m_sein.Controller.InputLocked &&
                !___m_sein.Abilities.SpiritFlame.LockShootingSpiritFlame)
            {
                ___Logic.ChangeState(__instance.State.Precharging);
            }

            return false;
        }
    }
}
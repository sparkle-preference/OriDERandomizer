using System;
using Game;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinDashAttack))]
    public static class SeinDashAttackPatches
    {
        [HarmonyPatch(nameof(SeinDashAttack.ResetDashLimit))]
        [HarmonyPostfix]
        public static void ResetDoubleAirDash()
        {
            RandomizerBonus.DoubleAirDashUsed = false;
        }

        [HarmonyPatch(nameof(SeinDashAttack.CanPerformNormalDash))]
        [HarmonyPostfix]
        public static void AllowUnderwaterDash(SeinDashAttack __instance, bool ___m_hasDashed,  
            ref bool __result)
        {
            var cd = (bool) AccessTools.Method(__instance.GetType(), "get_DashHasCooledDown").Invoke(__instance ,new object[]{});
            
            __result |= (RandomizerBonus.GravitySuit() && Characters.Sein.Abilities.Swimming.IsSwimming) &&
                        !__instance.AgainstWall() && cd && ___m_hasDashed;
        }

        [HarmonyPatch(nameof(SeinDashAttack.UpdateState))]
        [HarmonyPostfix]
        public static void UpdateEnergyCost(SeinDashAttack __instance)
        {
            __instance.EnergyCost = RandomizerBonus.ChargeDashEfficiency() ? 0.5f : 1f;
        }

        [HarmonyPatch("CanChargeDash")]
        [HarmonyPostfix]
        public static void ForbidUnderwaterCDash(ref bool __result)
        {
            __result &= !Characters.Sein.Abilities.Swimming.IsSwimming;
        }

        [HarmonyPatch(nameof(SeinDashAttack.PerformDash), new Type[]{})]
        [HarmonyPostfix]
        public static void UseExtraAirDash(ref bool ___m_hasDashed)
        {
            if (RandomizerBonus.DoubleAirDash() && !RandomizerBonus.DoubleAirDashUsed)
            {
                ___m_hasDashed = false;
                RandomizerBonus.DoubleAirDashUsed = true;
            }
        }

        [HarmonyPatch(nameof(SeinDashAttack.UpdateChargeDashing))]
        [HarmonyPostfix]
        public static void CDashVelocity(SeinCharacter ___m_sein)
        {
            ___m_sein.PlatformBehaviour.PlatformMovement.LocalSpeed *= 1f + 0.2f * (float) RandomizerBonus.Velocity();
        }

        [HarmonyPatch(nameof(SeinDashAttack.UpdateNormal))]
        [HarmonyPrefix]
        public static void DashResets(SeinCharacter ___m_sein, ref bool ___m_hasDashed)
        {
            if (___m_sein.IsOnGround || (RandomizerBonus.GravitySuit() && Characters.Sein.Abilities.Swimming.IsSwimming))
            {
                ___m_hasDashed = false;
                RandomizerBonus.DoubleAirDashUsed = false;
            }
        }

        [HarmonyPatch(nameof(SeinDashAttack.UpdateDashing))]
        [HarmonyPostfix]
        public static void DashVelocity(SeinCharacter ___m_sein)
        {
            ___m_sein.PlatformBehaviour.PlatformMovement.LocalSpeed *= 1f + 0.2f * (float) RandomizerBonus.Velocity();
            if (RandomizerBonus.GravitySuit() && Characters.Sein.Abilities.Swimming.IsSwimming)
            {
                ___m_sein.PlatformBehaviour.PlatformMovement.LocalSpeed =
                    ___m_sein.PlatformBehaviour.PlatformMovement.LocalSpeed.Rotate(___m_sein.Abilities.Swimming.SwimAngle);
            }
        }
    }
}
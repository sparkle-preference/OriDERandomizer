using Core;
using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch]
    public static class SeinBashAttackPatches
    {
        [HarmonyPatch(typeof(SeinBashAttack))]
        [HarmonyPatch("JumpOffTarget")]
        [HarmonyPostfix]
        static void VelocityPatch(SeinBashAttack __instance)
        {
            __instance.PlatformMovement.WorldSpeed +=
                __instance.PlatformMovement.WorldSpeed * 0.1f * (float) RandomizerBonus.Velocity();
        }

        [HarmonyPatch(typeof(Damage))]
        [HarmonyPatch(MethodType.Constructor, typeof(float), typeof(Vector2), typeof(Vector3), typeof(DamageType), typeof(GameObject))]
        [HarmonyPrefix]
        static void BashBonusVelocityDamageForcePatch(ref float amount, ref Vector2 force, DamageType type)
        {
            if (type != DamageType.Bash)
                return;

            amount = RandomizerBonusSkill.AbilityDamage(amount);
            force += force / 4f * (float) RandomizerBonus.Velocity();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SeinBashAttack))]
        [HarmonyPatch("UpdateNormalState")]
        static void FreeDoubleBash(SeinBashAttack __instance, ref float ___m_timeRemainingOfBashButtonPress)
        {
            Randomizer.BashWasQueued = Randomizer.QueueBash;
            if (Randomizer.QueueBash)
            {
                Randomizer.QueueBash = false;
                ___m_timeRemainingOfBashButtonPress = 0.5f;
                if (__instance.Sein.IsOnGround && __instance.Sein.Speed.x == 0f &&
                    !SeinAbilityRestrictZone.IsInside(SeinAbilityRestrictZoneMode.AllAbilities) &&
                    !__instance.Sein.Abilities.Carry.IsCarrying)
                {
                    __instance.Sein.Animation.Play(__instance.BackFlipAnimation, 10, null);
                    __instance.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY = __instance.BackFlipSpeed;
                    if ((!__instance.Sein.PlayerAbilities.BashBuff.HasAbility)
                            ? __instance.StationaryBashSound
                            : __instance.UpgradedStationaryBashSound)
                    {
                        Sound.Play(
                            (!__instance.Sein.PlayerAbilities.BashBuff.HasAbility)
                                ? __instance.StationaryBashSound.GetSound(null)
                                : __instance.UpgradedStationaryBashSound.GetSound(null), __instance.transform.position,
                            null);
                    }
                }
            }

            if (__instance.CanBash && Randomizer.BashWasQueued)
                __instance.BeginBash();
        }
    }
}
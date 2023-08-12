using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinDamageReciever))]
    public static class SeinDamageRecieverPatches
    {
        [HarmonyPatch("OnRestoreCheckpoint")]
        [HarmonyPostfix]
        static void UpdateDrain()
        {
            RandomizerBonusSkill.UpdateDrain();
        }

        [HarmonyPatch("OnKill")]
        [HarmonyPrefix]
        static void BingoDeath(SeinDamageReciever __instance, Damage damage)
        {
            if (!__instance.Sein.Active)
            {
                return;
            }

            BingoController.OnDeath(damage);
        }

        [HarmonyPatch("OnRecieveDamage")]
        [HarmonyPrefix]
        static bool SeinDamageMultipliers(SeinDamageReciever __instance, Damage damage)
        {
            if (RandomizerBonusSkill.Invincible)
            {
                return false;
            }

            damage.SetAmount(Mathf.Round(damage.Amount * Randomizer.DamageModifier));
            if (Randomizer.OHKO)
            {
                damage.SetAmount(damage.Amount * 100f);
            }
            return true;
        }
    }
}
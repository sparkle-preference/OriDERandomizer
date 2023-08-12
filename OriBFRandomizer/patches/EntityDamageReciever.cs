using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(EntityDamageReciever))]
    [HarmonyPatch("OnRecieveDamage")]
    public static class EntityDamageReceiverPatches
    {
        static void Postfix(EntityDamageReciever __instance, Damage damage)
        {
            bool flag = damage.Type == DamageType.Crush || damage.Type == DamageType.Spikes ||
                        damage.Type == DamageType.Lava || damage.Type == DamageType.Laser;
            if (__instance.Entity is Enemy && !flag && damage.Type != DamageType.Projectile &&
                damage.Type != DamageType.Enemy)
            {
                RandomizerBonus.DamageDealt(damage.Amount);
            }

            if ( __instance.NoHealthLeft)
            {
                BingoController.OnDestroyEntity(__instance.Entity, damage);
                if (__instance.Entity is Enemy)
                    RandomizerStatsManager.OnKill(damage.Type);
                if (__instance.Entity is PetrifiedPlant)
                    RandomizerLocationManager.GivePickup(__instance.Entity.MoonGuid);
            }
        }
    }
}
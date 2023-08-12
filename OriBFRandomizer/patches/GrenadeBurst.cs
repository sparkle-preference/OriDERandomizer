using System.Collections.Generic;
using Game;
using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(GrenadeBurst))]
    [HarmonyPatch("DealDamage")]
    public static class GrenadeBurstPatches
    {
        public static bool Prefix(GrenadeBurst __instance, HashSet<IAttackable> ___m_damageAttackables, ref float ___m_waitDelay)
        {
            Vector3 position = __instance.transform.position;
            foreach (IAttackable attackable in Targets.Attackables.ToArray())
            {
                if (!InstantiateUtility.IsDestroyed(attackable as Component) &&
                    !___m_damageAttackables.Contains(attackable) && attackable.CanBeGrenaded())
                {
                    Vector3 position2 = attackable.Position;
                    Vector3 vector = position2 - position;
                    if (vector.magnitude <= __instance.BurstRadius + (float) RandomizerBonus.SpiritFlameLevel())
                    {
                        ___m_damageAttackables.Add(attackable);
                        GameObject gameObject = ((Component) attackable).gameObject;
                        new Damage(__instance.DamageAmount + (float) (3 * RandomizerBonus.SpiritFlameLevel()),
                                vector.normalized * 3f, position, DamageType.Grenade, __instance.gameObject)
                            .DealToComponents(gameObject);
                        if (!attackable.IsDead())
                        {
                            GameObject gameObject2 =
                                (GameObject) InstantiateUtility.Instantiate(__instance.BurstImpactEffectPrefab, position2,
                                    Quaternion.identity);
                            gameObject2.transform.eulerAngles =
                                new Vector3(0f, 0f, MoonMath.Angle.AngleFromVector(vector.normalized));
                            gameObject2.GetComponent<FollowPositionRotation>().SetTarget(gameObject.transform);
                        }
                    }
                }
            }
            ___m_waitDelay = 0.1f;
            return false;
        }
    }
}
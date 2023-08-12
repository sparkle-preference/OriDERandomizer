using System;
using System.Collections.Generic;
using Game;
using HarmonyLib;
using UnityEngine;
[HarmonyPatch(typeof(ChargeFlameBurst))]
[HarmonyPatch("DealDamage")]
public class ChargeFlameBurstPatch
{
    public static void Prefix(ChargeFlameBurst __instance, ref HashSet<IAttackable> ___m_damageAttackables, int ___m_simultaneousEnemies, ref float ___m_waitDelay )
    {
        Vector3 position = __instance.transform.position;
        foreach (IAttackable attackable in Targets.Attackables.ToArray())
        {
            if (!InstantiateUtility.IsDestroyed(attackable as Component) && !___m_damageAttackables.Contains(attackable) && attackable.CanBeChargeFlamed())
            {
                Vector3 position2 = attackable.Position;
                Vector3 vector = position2 - position;
                if (vector.magnitude <= __instance.BurstRadius)
                {
                    ___m_damageAttackables.Add(attackable);
                    GameObject gameObject = ((Component)attackable).gameObject;
                    new Damage(__instance.DamageAmount + (float)(6 * RandomizerBonus.SpiritFlameLevel()), vector.normalized * 3f, position, DamageType.ChargeFlame, __instance.gameObject).DealToComponents(gameObject);
                    bool flag = attackable.IsDead();
                    if (!flag)
                    {
                        GameObject gameObject2 = (GameObject)InstantiateUtility.Instantiate(__instance.BurstImpactEffectPrefab, position2, Quaternion.identity);
                        gameObject2.transform.eulerAngles = new Vector3(0f, 0f, MoonMath.Angle.AngleFromVector(vector.normalized));
                        gameObject2.GetComponent<FollowPositionRotation>().SetTarget(gameObject.transform);
                    }
                    if (flag && attackable is IChargeFlameAttackable && ((IChargeFlameAttackable)attackable).CountsTowardsPowerOfLightAchievement())
                    {
                        ___m_simultaneousEnemies++;
                    }
                }
            }
        }
        if (___m_simultaneousEnemies >= 4)
        {
            AchievementsController.AwardAchievement(Characters.Sein.Abilities.ChargeFlame.KillEnemiesSimultaneouslyAchievement);
        }
        ___m_waitDelay = 0.1f;
    }
}
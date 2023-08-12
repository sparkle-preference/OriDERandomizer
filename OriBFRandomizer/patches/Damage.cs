using System;
using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(Damage))]
    public static class DamagePatches
    {
        [HarmonyPatch(MethodType.Constructor, typeof(float), typeof(Vector2), typeof(Vector3), typeof(DamageType), typeof(GameObject))]
        [HarmonyPrefix]
        static void SpiritFlameBonusDamagePatch(DamageType type, ref float amount)
        {
            if (type == DamageType.SpiritFlame)
            {
                amount += RandomizerBonus.SpiritFlameLevel();
            }
        }
    }
}
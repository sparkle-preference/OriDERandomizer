using Game;
using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch]
    public static class SeinPickupProcessorPatches
    {
        [HarmonyPatch(typeof(SeinPickupProcessor.CollectableInformation))]
        [HarmonyPatch(nameof(SeinPickupProcessor.CollectableInformation.RunActionIfFirstTime))]
        [HarmonyPostfix]
        public static void Collect(SeinPickupProcessor.CollectableInformation __instance)
        {
            if (__instance.HasBeenCollectedBefore)
            {
                return;
            }

            __instance.HasBeenCollectedBefore = true;
            if (__instance.FirstTimeCollectedSequence)
            {
                __instance.FirstTimeCollectedSequence.Perform(null);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(SeinPickupProcessor.OnCollectMapStonePickup))]
        [HarmonyPatch(typeof(SeinPickupProcessor))]
        public static bool MS(MapStonePickup mapStonePickup)
        {
            if (!RandomizerLocationManager.IsPickupRepeatable(mapStonePickup.MoonGuid) || Randomizer.RepeatableCheck())
            {
                RandomizerLocationManager.GivePickup(mapStonePickup.MoonGuid);
            }

            if (RandomizerLocationManager.IsPickupRepeatable(mapStonePickup.MoonGuid))
            {
                return false;
            }

            mapStonePickup.Collected();
            if (GameWorld.Instance.CurrentArea != null)
            {
                GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
            }

            return false;
        }
        [HarmonyPrefix]
        [HarmonyPatch(nameof(SeinPickupProcessor.OnCollectSkillPointPickup))]
        [HarmonyPatch(typeof(SeinPickupProcessor))]
        public static bool AC(SkillPointPickup skillPointPickup)
        {
            if (!RandomizerLocationManager.IsPickupRepeatable(skillPointPickup.MoonGuid) || Randomizer.RepeatableCheck())
            {
                RandomizerLocationManager.GivePickup(skillPointPickup.MoonGuid);
            }

            if (RandomizerLocationManager.IsPickupRepeatable(skillPointPickup.MoonGuid))
            {
                return false;
            }

            skillPointPickup.Collected();
            if (GameWorld.Instance.CurrentArea != null)
            {
                GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(SeinPickupProcessor.OnCollectMaxHealthContainerPickup))]
        [HarmonyPatch(typeof(SeinPickupProcessor))]
        public static bool HC(MaxHealthContainerPickup maxHealthContainerPickup)
        {
            if (!RandomizerLocationManager.IsPickupRepeatable(maxHealthContainerPickup.MoonGuid) || Randomizer.RepeatableCheck())
            {
                RandomizerLocationManager.GivePickup(maxHealthContainerPickup.MoonGuid);
            }

            if (RandomizerLocationManager.IsPickupRepeatable(maxHealthContainerPickup.MoonGuid))
            {
                return false;
            }

            maxHealthContainerPickup.Collected();
            if (GameWorld.Instance.CurrentArea != null)
            {
                GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(SeinPickupProcessor.OnCollectMaxEnergyContainerPickup))]
        [HarmonyPatch(typeof(SeinPickupProcessor))]
        public static bool EC(MaxEnergyContainerPickup energyContainerPickup)
        {
            if (!RandomizerLocationManager.IsPickupRepeatable(energyContainerPickup.MoonGuid) || Randomizer.RepeatableCheck())
            {
                RandomizerLocationManager.GivePickup(energyContainerPickup.MoonGuid);
            }

            if (RandomizerLocationManager.IsPickupRepeatable(energyContainerPickup.MoonGuid))
            {
                return false;
            }

            energyContainerPickup.Collected();
            if (GameWorld.Instance.CurrentArea != null)
            {
                GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(SeinPickupProcessor.OnCollectKeystonePickup))]
        [HarmonyPatch(typeof(SeinPickupProcessor))]
        public static bool Keystone(KeystonePickup keystonePickup)
        {
            if (!RandomizerLocationManager.IsPickupRepeatable(keystonePickup.MoonGuid) || Randomizer.RepeatableCheck())
            {
                RandomizerLocationManager.GivePickup(keystonePickup.MoonGuid);
            }

            if (RandomizerLocationManager.IsPickupRepeatable(keystonePickup.MoonGuid))
            {
                return false;
            }

            keystonePickup.Collected();
            if (GameWorld.Instance.CurrentArea != null)
            {
                GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(SeinPickupProcessor.OnCollectExpOrbPickup))]
        [HarmonyPatch(typeof(SeinPickupProcessor))]
        public static bool EXP(SeinPickupProcessor __instance, ref ExpText ___m_expText, ExpOrbPickup expOrbPickup)
        {
            int num = RandomizerBonus.ExpWithBonuses(expOrbPickup.Amount, false);
            if (expOrbPickup.MessageType == ExpOrbPickup.ExpOrbMessageType.None)
            {
                expOrbPickup.Collected();
                if (Randomizer.IgnoreEnemyExp)
                {
                    return false;
                }

                RandomizerBonus.ExpWithBonuses(expOrbPickup.Amount, true);
                __instance.Sein.Level.GainExperience(num);
                if (___m_expText && ___m_expText.gameObject.activeInHierarchy)
                {
                    ___m_expText.Amount += num;
                }
                else
                {
                    ___m_expText = Orbs.OrbDisplayText.Create(Characters.Sein.Transform, Vector3.up, num);
                }

                UI.SeinUI.ShakeExperienceBar();
                if (GameWorld.Instance.CurrentArea != null)
                {
                    GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
                }

                return false;
            }
            else
            {
                if (!RandomizerLocationManager.IsPickupRepeatable(expOrbPickup.MoonGuid) || Randomizer.RepeatableCheck())
                {
                    RandomizerLocationManager.GivePickup(expOrbPickup.MoonGuid);
                }

                if (RandomizerLocationManager.IsPickupRepeatable(expOrbPickup.MoonGuid))
                {
                    return false;
                }

                if (GameWorld.Instance.CurrentArea != null)
                {
                    GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
                }

                expOrbPickup.Collected();
                return false;
            }
        }
    }
}
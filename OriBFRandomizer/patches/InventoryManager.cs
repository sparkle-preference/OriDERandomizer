using HarmonyLib;
using Sein.World;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    public static class InventoryManagerExt
    {
        public static string GetKeyLabel(bool hasKey, int shards, int keyIndex)
        {
            if (hasKey)
            {
                return "";
            }
            if (Randomizer.Shards)
            {
                return string.Format("{0}/3", shards);
            }
            if (!Randomizer.CluesMode)
            {
                return "";
            }
            if (RandomizerBonus.SkillTreeProgression() >= RandomizerClues.RevealOrder[keyIndex] * 3)
            {
                return RandomizerClues.Clues[RandomizerClues.RevealOrder[keyIndex] - 1];
            }
            return "";
        }
    

        public static MessageBox gumonSealClueText;

        public static MessageBox waterVeinClueText;

        public static MessageBox sunstoneClueText;
    }

    [HarmonyPatch(typeof(InventoryManager))]
    public static class InventoryManagerPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryManager.UpdateItems))]
        public static void SetClueTexts()
        {
            InventoryManagerExt.waterVeinClueText.SetMessage(new MessageDescriptor(InventoryManagerExt.GetKeyLabel(Keys.GinsoTree, RandomizerBonus.WaterVeinShards(), 0)));
            InventoryManagerExt.gumonSealClueText.SetMessage(new MessageDescriptor(InventoryManagerExt.GetKeyLabel(Keys.ForlornRuins, RandomizerBonus.GumonSealShards(), 1)));
            InventoryManagerExt.sunstoneClueText.SetMessage(new MessageDescriptor(InventoryManagerExt.GetKeyLabel(Keys.MountHoru, RandomizerBonus.SunstoneShards(), 2)));

        }
    
        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryManager.Awake))]
        public static void InitClueTexts(InventoryManager __instance)
        {
            if (__instance.Difficulty)
            {
                DifficultyModeMessageProvider difficultyModeMessageProvider = (DifficultyModeMessageProvider)__instance.Difficulty.MessageProvider;
                difficultyModeMessageProvider.Easy = RandomizerText.DifficultyOverrides.Easy.NameOverrideUpper;
                difficultyModeMessageProvider.Normal = RandomizerText.DifficultyOverrides.Normal.NameOverrideUpper;
                difficultyModeMessageProvider.Hard = RandomizerText.DifficultyOverrides.Hard.NameOverrideUpper;
                difficultyModeMessageProvider.OneLife = RandomizerText.DifficultyOverrides.OneLife.NameOverrideUpper;
                InstantiateAction instantiateAction = (InstantiateAction)((ActionSequence)__instance.Difficulty.transform.parent.GetComponent<RunActionCondition>().Action).Actions[0];
                ChangeDifficultyScreen component = instantiateAction.Prefab.GetComponent<ChangeDifficultyScreen>();
                component.Easy = RandomizerText.DifficultyOverrides.Easy.NameOverride;
                component.Normal = RandomizerText.DifficultyOverrides.Normal.NameOverride;
                component.Hard = RandomizerText.DifficultyOverrides.Hard.NameOverride;
                component.OneLife = RandomizerText.DifficultyOverrides.OneLife.NameOverride;
                CleverMenuItemSelectionManager component2 = instantiateAction.Prefab.GetComponent<CleverMenuItemSelectionManager>();
                component2.MenuItems[0].GetComponentInChildren<MessageBox>().SetMessageProvider(RandomizerText.DifficultyOverrides.Easy.NameOverrideUpper);
                component2.MenuItems[1].GetComponentInChildren<MessageBox>().SetMessageProvider(RandomizerText.DifficultyOverrides.Normal.NameOverrideUpper);
                component2.MenuItems[2].GetComponentInChildren<MessageBox>().SetMessageProvider(RandomizerText.DifficultyOverrides.Hard.NameOverrideUpper);
            }
            InventoryManagerExt.waterVeinClueText = UnityEngine.Object.Instantiate<MessageBox>(__instance.EnergyUpgradesText);
            InventoryManagerExt.waterVeinClueText.transform.position = __instance.GinsoTreeKey.transform.position + Vector3.down * 0.55f;
            InventoryManagerExt.waterVeinClueText.transform.SetParent(__instance.GinsoTreeKey.transform);
            InventoryManagerExt.gumonSealClueText = UnityEngine.Object.Instantiate<MessageBox>(__instance.EnergyUpgradesText);
            InventoryManagerExt.gumonSealClueText.transform.position = __instance.ForlornRuinsKey.transform.position + Vector3.down * 0.55f;
            InventoryManagerExt.gumonSealClueText.transform.SetParent(__instance.ForlornRuinsKey.transform);
            InventoryManagerExt.sunstoneClueText = UnityEngine.Object.Instantiate<MessageBox>(__instance.EnergyUpgradesText);
            InventoryManagerExt.sunstoneClueText.transform.position = __instance.MountHoruKey.transform.position + Vector3.down * 0.55f;
            InventoryManagerExt.sunstoneClueText.transform.SetParent(__instance.MountHoruKey.transform);
        }

    }
}
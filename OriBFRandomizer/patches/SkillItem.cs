using System.Collections.Generic;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    public static class SkillItemExt
    {
        public static int ActualTotalRequiredSkillPoints(this SkillItem _this)
        {
            if (DifficultyController.Instance.Difficulty == DifficultyMode.Hard)
            {
                return TotalRequiredHardSkillPoints[_this];
            }

            return TotalRequiredSkillPoints[_this];
        }


        public static Dictionary<SkillItem, int> TotalRequiredSkillPoints = new Dictionary<SkillItem, int>();
        public static Dictionary<SkillItem, int> TotalRequiredHardSkillPoints = new Dictionary<SkillItem, int>();
        
        public static MessageProvider Name(this SkillItem _this)
        {
            return RandomizerText.GetAbilityName(_this.Ability) ?? _this.NameMessageProvider;
        }

        public static MessageProvider Description(this SkillItem _this)
        {
            return RandomizerText.GetAbilityDescription(_this.Ability) ?? _this.DescriptionMessageProvider;
        }
    }

    [HarmonyPatch(typeof(SkillItem))]
    public static class SkillItemPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("get_AbilitiesRequirementMet")]
        public static bool RemoveSkillRequirementFromAbilityTreePatch(SkillItem __instance, ref bool __result)
        {
            using (List<SkillItem>.Enumerator enumerator = __instance.RequiredItems.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (!enumerator.Current.HasSkillItem)
                    {
                        __result = false;
                        return false;
                    }
                }
            }

            __result = true;
            return false;
        }
    }
}
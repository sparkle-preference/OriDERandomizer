using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SkillTreeLaneLogic))]
    public static class SkillTreeLaneLogicPatches
    {
        [HarmonyPatch(nameof(SkillTreeLaneLogic.UpdateItems))]
        [HarmonyPrefix]
        public static void CollectSkillPointRequirements(SkillTreeLaneLogic __instance)
        {
            int num = 0;
            int num2 = 0;
            int num3 = 0;
            for (int i = 0; i < __instance.Skills.Count; i++)
            {
                SkillItem skillItem = __instance.Skills[i];
                if (!skillItem.HasSkillItem)
                {
                    if (num == 0)
                    {
                        num = i + 1;
                    }

                    num2 += skillItem.RequiredSkillPoints;
                    num3 += skillItem.RequiredHardSkillPoints;

                    SkillItemExt.TotalRequiredSkillPoints[skillItem] = num2;
                    SkillItemExt.TotalRequiredHardSkillPoints[skillItem] = num3;
                }
            }
        }
    }
}
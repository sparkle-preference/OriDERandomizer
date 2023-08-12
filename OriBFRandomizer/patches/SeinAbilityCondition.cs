using Core;
using Game;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinAbilityCondition))]
    [HarmonyPatch("Validate")]
    public static class SeinAbilityConditionPatch
    {
        static bool Prefix(SeinAbilityCondition __instance, ref bool __result)
        {
            if (Characters.Sein != null)
            {
                if (__instance.Ability == AbilityType.Stomp)
                {
                    if (Randomizer.Inventory.FinishedGinsoEscape && Scenes.Manager.CurrentScene != null)
                    {
                        string scene = Scenes.Manager.CurrentScene.Scene;
                        if (scene == "ginsoTreeTurrets")
                        {
                            __result = true;
                            return false;
                        }
                        if (scene == "kuroMomentTreeDuplicate")
                        {
                            __result = false;
                            return false;
                        }
                    }
                    if (Randomizer.OpenWorld)
                    {
                        __result = false;
                        return false;
                    }
                    if (!Randomizer.StompTriggers)
                    {
                        __result = true;
                        return false;
                    }
                }
                __result = Characters.Sein.PlayerAbilities.HasAbility(__instance.Ability);
            }

            __result = false;
            return false;
        }

    }
}
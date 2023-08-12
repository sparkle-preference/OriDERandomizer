using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(PlayerAbilities))]
    public class PlayerAbilitiesPatches
    {

        [HarmonyPatch(typeof(SeinSpiritFlameTargetting))]
        [HarmonyPatch("get_MaxNumberOfTargets")]
        [HarmonyPostfix]
        public static void AddBonusTargets(ref float __result)
        {
            __result += RandomizerBonus.SpiritFlameLevel();
        }

        [HarmonyPatch(nameof(PlayerAbilities.SetAbility))]
        [HarmonyPostfix]
        public static void DiscoverWorld(AbilityType ability)
        {
            if (ability != AbilityType.MapMarkers)
                return;
        
            using (var enumerator = GameWorld.Instance.RuntimeAreas.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    RuntimeGameWorldArea runtimeGameWorldArea = enumerator.Current;
                    runtimeGameWorldArea?.DiscoverAllAreas();
                }
            }
        }

    }
}
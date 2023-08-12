using Game;

namespace OriBFRandomizer.patches
{
    public static class AbilityDebugMenuItemsExt
    {
        public static bool ChargeFlameBurnGetter()
        {
            return Characters.Sein.PlayerAbilities.ChargeFlameBurn.HasAbility;
        }

        public static void ChargeFlameBurnSetter(bool newValue)
        {
            Characters.Sein.PlayerAbilities.ChargeFlameBurn.HasAbility = newValue;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
        }

        public static bool RemoveAllSkillsAndAbilities()
        {
            AbilityDebugMenuItemsExt.RemoveAllSkills();
            AbilityDebugMenuItemsExt.RemoveAllAbilities();
            return true;
        }

        public static bool RemoveAllSkills()
        {
            AbilityDebugMenuItems.SpiritFlameSetter(false);
            AbilityDebugMenuItems.WallJumpSetter(false);
            AbilityDebugMenuItems.ChargeFlameSetter(false);
            AbilityDebugMenuItems.DoubleJumpSetter(false);
            AbilityDebugMenuItems.BashSetter(false);
            AbilityDebugMenuItems.StompSetter(false);
            AbilityDebugMenuItems.GlideSetter(false);
            AbilityDebugMenuItems.ClimbSetter(false);
            AbilityDebugMenuItems.ChargeJumpSetter(false);
            AbilityDebugMenuItems.DashSetter(false);
            AbilityDebugMenuItems.GrenadeSetter(false);
            return true;
        }

        public static bool RemoveAllAbilities()
        {
            AbilityDebugMenuItemsExt.RemoveAllBlueAbilities();
            AbilityDebugMenuItemsExt.RemoveAllPurpleAbilities();
            AbilityDebugMenuItemsExt.RemoveAllRedAbilities();
            return true;
        }

        public static bool RemoveAllBlueAbilities()
        {
            AbilityDebugMenuItems.RekindleSetter(false);
            AbilityDebugMenuItems.RegroupSetter(false);
            AbilityDebugMenuItems.ChargeFlameEfficiencySetter(false);
            AbilityDebugMenuItems.AirDashSetter(false);
            AbilityDebugMenuItems.UltraSoulFlameSetter(false);
            AbilityDebugMenuItems.ChargeDashSetter(false);
            AbilityDebugMenuItems.WaterBreathSetter(false);
            AbilityDebugMenuItems.SoulFlameEfficiencySetter(false);
            AbilityDebugMenuItems.DoubleJumpUpgradeSetter(false);
            AbilityDebugMenuItems.UltraDefenseSetter(false);
            return true;
        }

        public static bool RemoveAllPurpleAbilities()
        {
            AbilityDebugMenuItems.MagnetSetter(false);
            AbilityDebugMenuItems.MapMarkersSetter(false);
            AbilityDebugMenuItems.HealthEfficiencySetter(false);
            AbilityDebugMenuItems.UltraMagnetSetter(false);
            AbilityDebugMenuItems.EnergyEfficiencySetter(false);
            AbilityDebugMenuItems.AbilityMarkersSetter(false);
            AbilityDebugMenuItems.SoulEfficiencySetter(false);
            AbilityDebugMenuItems.HealthMarkersSetter(false);
            AbilityDebugMenuItems.EnergyMarkersSetter(false);
            AbilityDebugMenuItems.SenseSetter(false);
            return true;
        }

        public static bool RemoveAllRedAbilities()
        {
            AbilityDebugMenuItems.QuickFlameSetter(false);
            AbilityDebugMenuItems.SparkFlameSetter(false);
            AbilityDebugMenuItemsExt.ChargeFlameBurnSetter(false);
            AbilityDebugMenuItems.SplitFlameUpgradeSetter(false);
            AbilityDebugMenuItems.GrenadeUpgradeSetter(false);
            AbilityDebugMenuItems.CinderFlameSetter(false);
            AbilityDebugMenuItems.StompUpgradeSetter(false);
            AbilityDebugMenuItems.RapidFireSetter(false);
            AbilityDebugMenuItems.ChargeFlameBlastSetter(false);
            AbilityDebugMenuItems.UltraSplitFlameSetter(false);
            AbilityDebugMenuItems.BashUpgradeSetter(false);
            AbilityDebugMenuItems.GrenadeEfficiencySetter(false);
            return true;
        }
    }
}
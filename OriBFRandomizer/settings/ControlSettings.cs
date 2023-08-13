using BaseModLib;

namespace OriBFRandomizer.settings
{
    public static class ControlSettings
    {
        public static readonly FloatSetting BASH_DEADZONE =
            new FloatSetting("controls.bash_deadzone", 0, 1, 0.1f, "Controller Bash Deadzone", "", 1.0f);

        public static readonly BoolSetting INSTANT_GRENADE_AIM =
            new BoolSetting("controls.instant_grenade_aim", "Instant Grenade Aim", "", false); 
        
        public static readonly FloatSetting GRENADE_AIM_SPEED =
             new FloatSetting("controls.grenade_aim_speed", 0, 1, 0.1f, "Grenade Aim Speed", "", 1.0f);
        
        public static readonly BoolSetting SWIMMING_MOUSE_AIM = new BoolSetting("controls.swim_mouse_aim", "Swimming Mouse Aim", "", false);
        public static readonly BoolSetting INVERT_SWIM = new BoolSetting("controls.invert_swim", "Invert Swim", "", false);
        public static readonly BoolSetting WALL_CHARGE_MOUSE_AIM = new BoolSetting("controls.wall_charge_mouse_aim", "Wall Charge Mouse Aim", "", false);
        public static readonly BoolSetting INVERT_CLIMB = new BoolSetting("controls.invert_climb", "Invert Climb", "", false);
        public static readonly BoolSetting CLIMB_VAULT_ASSIST = new BoolSetting("controls.climb_vault_assist", "Slow Climb Vault", "", false);

    }
}
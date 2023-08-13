using System;
using System.Collections.Generic;
using BaseModLib;
using OriBFRandomizer.settings;
using OriDeModLoader;

namespace OriBFRandomizer
{
    public class BFRandomizer: BaseMod 
    {
        public override string Name => "Ori and the Blind Forest - Randomizer";
        public override string ModID => "com.orirando.bf";
        public override string Version => "5.0.0";

        public override void FixedUpdate()
        {
        }

        public override List<SettingsScreenConfig> GetSettings() => new List<SettingsScreenConfig>
        {
            SettingsScreen("Randomizer Accessibility Options", AccessibilitySettings.CAMERA_SHAKE, AccessibilitySettings.SOUND_COMPRESSION, AccessibilitySettings.SOUND_COMPRESSION_FACTOR, AccessibilitySettings.SA_OPACITY_FACTOR, AccessibilitySettings.CURSOR_LOCK),
            SettingsScreen("Randomizer Control Options", ControlSettings.BASH_DEADZONE, ControlSettings.INSTANT_GRENADE_AIM, ControlSettings.GRENADE_AIM_SPEED, ControlSettings.SWIMMING_MOUSE_AIM, ControlSettings.INVERT_SWIM, ControlSettings.WALL_CHARGE_MOUSE_AIM, ControlSettings.INVERT_CLIMB, ControlSettings.CLIMB_VAULT_ASSIST),
            SettingsScreen("Randomizer Keybinds", Keybinds.REPLAY_MESSAGE, Keybinds.OPEN_TELEPORTER_MENU, Keybinds.RELOAD_SEED, Keybinds.SHOW_PROGRESS, Keybinds.DOUBLE_BASH, Keybinds.GJUMP, Keybinds.LIST_TREES, Keybinds.LIST_MAP_ALTARS, Keybinds.LIST_TELEPORTERS, Keybinds.LIST_RELICS, Keybinds.SHOW_STATS),
        };
    }
}
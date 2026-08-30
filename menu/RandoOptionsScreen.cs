public class RandoOptionsScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddSlider(RandomizerSettings.Customization.MapWarpHold, 0.1f, 3f, 0.1f, "How long Proceed must be held on a Spirit Well in the map before it warps you there.");
        AddToggle(RandomizerSettings.Customization.ShowOtherPlayers, "Shows the other players in your game as ghosts and as markers on the map, and shares your position with them. It is both or neither.");
        AddToggle(RandomizerSettings.Customization.RandomizedExpNames, "Replaces \"Experience\" with a random currency name whenever a pickup grants it.");
        AddToggle(RandomizerSettings.Customization.AlwaysShowDoorHints, "Shows every unlocked Keysanity door hint on the map, not just the door under the cursor.");
        AddToggle(RandomizerSettings.Customization.KeyLockWarnings, "Warns when you touch a keystone door that is out of logic, since opening it early can strand the seed.");
        AddEnum(RandomizerSettings.Customization.DefaultMapFilter, "Which filter the map opens on. Changing this does not move the current one.");
        AddEnum(RandomizerSettings.Game.DefaultDifficulty, "Difficulty a new save file starts on.");
        AddColor(RandomizerSettings.Customization.HotColor, "Ori's colour when Sensing an item at point-blank range.");
        AddColor(RandomizerSettings.Customization.ColdColor, "Ori's colour when Sensing an item at the edge of range.");
        AddColor(RandomizerSettings.Customization.WarpTeleporterColor, "Colour of Warp-created teleporters on the map.");
    }
}

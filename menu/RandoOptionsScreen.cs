public class RandoOptionsScreen : CustomSettingsScreen {
    public override void InitScreen() {
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

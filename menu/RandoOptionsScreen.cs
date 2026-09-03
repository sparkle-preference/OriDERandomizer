public class RandoOptionsScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddSlider(RandomizerSettings.Customization.TouchedVisibility, 0f, 1f, 0.05f, "Pickup icon transparency for checked-but-uncollected pickups.");
        AddSlider(RandomizerSettings.Customization.MapWarpHold, 0.1f, 3f, 0.1f, "Hold duration when warping to Teleporters using the area map ([[Map Warp]]).");
        AddToggle(RandomizerSettings.Customization.ShowOtherPlayers, "Shows the other players in your multiplayer games on your screen and map.");
        AddToggle(RandomizerSettings.Customization.RandomizedExpNames, "Replaces \"Experience\" with a random-chosen currency name.");
        AddToggle(RandomizerSettings.Customization.AlwaysShowDoorHints, "Shows every unlocked Keysanity door hint on the map without hovering.");
        AddToggle(RandomizerSettings.Customization.KeyLockWarnings, "Toggle out-of-logic keystone door warnings (spending keystones out of logic can sometimes render seeds uncompletable).");
        AddEnum(RandomizerSettings.Customization.DefaultMapFilter, "Which item filter the map defaults to.");
        AddEnum(RandomizerSettings.Game.DefaultDifficulty, "Default difficulty on file creation.");
        AddColor(RandomizerSettings.Customization.HotColor, "Ori's color when Sensing an item at point-blank range.");
        AddColor(RandomizerSettings.Customization.ColdColor, "Ori's color when Sensing an item at max range.");
        AddColor(RandomizerSettings.Customization.WarpTeleporterColor, "Colour of Warp-created teleporters on the map.");
    }
}

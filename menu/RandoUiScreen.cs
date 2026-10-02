public class RandoUiScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddToggle(RandomizerSettings.Customization.MultiplePickupMessages, "Shows up to five pickup messages at once down the left, instead of one at a time in the center.");
        AddToggle(RandomizerSettings.Customization.AlwaysShowLastFivePickups, "Keeps the last five pickup messages on screen (requires Display Multiple Pickup Messages).");

        AddEnum(RandomizerSettings.Customization.HintLevel,
                "Customize the hints that display while Ori is teleporting.",
                "Warping Tips");
        AddEnum(RandomizerSettings.Customization.DefaultMapFilter, "Which item filter the map defaults to.");
        AddToggle(RandomizerSettings.Customization.AlwaysShowDoorHints, "Shows every unlocked Keysanity door hint on the map without hovering.");
        AddColor(RandomizerSettings.Customization.WarpTeleporterColor, "Color of Warp-created teleporters on the map.");

        AddToggle(RandomizerSettings.Customization.KeyLockWarnings, "Toggle out-of-logic keystone door warnings (spending keystones out of logic can sometimes render seeds uncompletable).");

        // the two previews sit mid-screen, where the background is brightest and a translucent message background is easiest to judge
        AddColor(RandomizerSettings.Customization.PickupMessageBgColor, "Background color for normal pickup messages.", null, true);
        AddColor(RandomizerSettings.Customization.MwPickupMessageBgColor, "Background for pickup messages when the pickup belongs to another player.", "Multiworld Message Background", true);

        AddToggle(RandomizerSettings.Customization.RandomizedExpNames, "Replaces \"Experience\" with a random-chosen currency name.");
        AddToggle(RandomizerSettings.Customization.CustomPickupNames, "Renames pickups with the rules in PickupNames.txt, next to the game.");

        AddToggle(RandomizerSettings.Customization.DisableTempResourceRows, "Display temporary health and energy as though you actually have that much health/energy.");
    }
}

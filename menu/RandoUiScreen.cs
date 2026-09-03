public class RandoUiScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddToggle(RandomizerSettings.Customization.DiscoSense, "Speeds up the Color.txt rotation while Sense is active (faster = closer).");
        AddToggle(RandomizerSettings.Customization.MultiplePickupMessages, "Shows up to five pickup messages at once down the left, instead of one at a time in the center.");
        AddToggle(RandomizerSettings.Customization.AlwaysShowLastFivePickups, "Keeps the last five pickup messages on screen (requires Display Multiple Pickup Messages).");

        AddEnum(RandomizerSettings.Customization.HintLevel,
                "Customize the hints that display while Ori is teleporting.",
                "Warping Tips");

        // the two previews sit mid-screen, where the background is brightest and a
        // translucent message background is easiest to judge
        AddColor(RandomizerSettings.Customization.PickupMessageBgColor, "Background color for normal pickup messages.", null, true);
        AddColor(RandomizerSettings.Customization.MwPickupMessageBgColor, "Background for pickup messages when the pickup belongs to another player.", "Multiworld Message Background", true);

        AddToggle(RandomizerSettings.Customization.DisableTempResourceRows, "Display temporary health and energy as though you actually have that much health/energy.");
    }
}

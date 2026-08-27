public class RandoUiScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddToggle(RandomizerSettings.Customization.DiscoSense, "Speeds up the Color.txt rotation while Sense is active, instead of tinting Ori with the Sense colours.");
        AddToggle(RandomizerSettings.Customization.MultiplePickupMessages, "Shows up to five pickup messages at once down the left, instead of one at a time in the centre.");
        AddToggle(RandomizerSettings.Customization.AlwaysShowLastFivePickups, "Keeps the last five pickup messages on screen. Needs Display Multiple Pickup Messages.");

        AddEnum(RandomizerSettings.Customization.HintLevel,
                "Who the tips on warp loading screens are written for, or Disabled for none.",
                "Warping Tips");

        // the two previews sit mid-screen, where the background is brightest and a
        // translucent message background is easiest to judge
        AddColor(RandomizerSettings.Customization.PickupMessageBgColor, "Background behind a pickup message.", null, true);
        AddColor(RandomizerSettings.Customization.MwPickupMessageBgColor, "Background behind a pickup you found for someone else.", "Multiworld Message Background", true);

        AddToggle(RandomizerSettings.Customization.DisableTempResourceRows, "Draws temporary health and energy inline past your normal cells, as older versions did.");
        AddSlider(RandomizerSettings.Customization.TempRowSpacing, 0f, 2f, 0.1f, "Gap between a bar and its temporary-resource row.");
        AddSlider(RandomizerSettings.Customization.TempRowHorizontalOffset, -2f, 2f, 0.1f, "Shifts the temporary-resource rows sideways, away from the experience wheel.");
        AddSlider(RandomizerSettings.Customization.TempRowScale, 0.2f, 1.5f, 0.05f, "Size of the temporary-resource rows against the bars they sit above.");
        AddSlider(RandomizerSettings.Customization.TempRowBrightness, 0f, 1f, 0.05f, "Brightness of the temporary-resource rows.");
    }
}

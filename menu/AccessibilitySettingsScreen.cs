public class AccessibilitySettingsScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddSlider(RandomizerSettings.Accessibility.CameraShakeFactor, 0f, 1f, 0.1f, "Allows adjusting the strength of camera shake effects (0% - 100%).");
        AddToggle(RandomizerSettings.Accessibility.ApplySoundCompression, "Toggles the sound compression effect, reducing the volume of loud sounds while increasing the volume of quiet sounds.");
        AddSlider(RandomizerSettings.Accessibility.SoundCompressionFactor, 0f, 1f, 0.1f, "Allows changing the scale of the sound compression effect (0% - 100%).");
        AddSlider(RandomizerSettings.QOL.AbilityMenuOpacity, 0f, 1f, 0.1f, "Opacity of the ability menu during a Save Anywhere (0% - 100%).");
        AddSlider(RandomizerSettings.QOL.PauseMenuOpacity, 0f, 1f, 0.1f, "Opacity of the pause menu (0% - 100%).");
        AddToggle(RandomizerSettings.Accessibility.DisableMenuBlur, "Disables the blur effect applied to the game during a Save Anywhere.");
        AddToggle(RandomizerSettings.Accessibility.ColorlessText, "Removes color styling from rando messages.");
        AddToggle(RandomizerSettings.Accessibility.TextCues, "Adds tags to important rando messages: (Skill), (Key), (World), (Shard), (Relic) or (Error).");
        AddToggle(RandomizerSettings.QOL.CursorLock, "Toggles Cursor Lock, which traps the mouse inside the window borders.");
    }
}

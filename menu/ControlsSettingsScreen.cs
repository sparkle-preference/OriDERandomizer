public class ControlsSettingsScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddSlider(RandomizerSettings.Controls.BashDeadzone, 0f, 1f, 0.05f, "The size of the stick deadzone when aiming Bash on controller (0% - 200%).");
        AddToggle(RandomizerSettings.Controls.FastGrenadeAim, "Toggles fast grenade aim.");
        AddSlider(RandomizerSettings.Controls.GrenadeAimSpeed, 0f, 2f, 0.1f, "Allows adjusting the speed at which the grenade will aim on controller (0% - 200%).");
        AddToggle(RandomizerSettings.Controls.SwimmingMouseAim, "Toggles directing Ori through water with the mouse.");
        AddToggle(RandomizerSettings.Controls.InvertSwim, "Toggles whether the swim speed input ([Jump]) is reversed. (When enabled, press [Jump] to swim slower).");
        AddToggle(RandomizerSettings.Controls.WallChargeMouseAim, "Toggles Wall Charge Jump mouse aiming.");
        AddToggle(RandomizerSettings.Controls.InvertClimb, "Toggles whether the Climb input ([Climb]) is inverted. If enabled, hold [Climb] to stop climbing.");
        AddToggle(RandomizerSettings.Controls.SlowClimbVault, "Toggles slow Climb vaults, which make it slightly easier to land on narrow platforms from below.");
        AddToggle(RandomizerSettings.Controls.LongerBashAimTime, "Triples the maximum Bash aim duration.");
        AddEnum(RandomizerSettings.Controls.GrenadeJump, "Auto jumps on the [[Grenade Jump]] bind; Manual is the vanilla two-input timing.");
        AddEnum(RandomizerSettings.Controls.Autofire, "Change Autofire modes - hold or toggle Spirit Flame to fire continuously, or Off for vanilla.");
    }
}

public class ControlsSettingsScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddSlider(RandomizerSettings.Controls.BashDeadzone, 0f, 1f, 0.05f, "The size of the stick deadzone when aiming Bash on controller (0% - 100%).");
        AddToggle(RandomizerSettings.Controls.FastGrenadeAim, "Toggles fast Grenade aim.");
        AddSlider(RandomizerSettings.Controls.GrenadeAimSpeed, 0f, 2f, 0.1f, "Allows adjusting the speed at which the grenade will aim on controller (0% - 200%).");
        AddEnum(RandomizerSettings.Controls.GrenadeJump, "Change Grenade Jump mode - Auto to use the bind ([[Grenade Jump]]); Manual for vanilla behavior.");
        AddEnum(RandomizerSettings.Controls.DoubleBash, "Change Double Bash mode - Hold vs Tap the [[Double Bash]] bind while Bashing.");
        AddEnum(RandomizerSettings.Controls.Autofire, "Change Autofire modes - Hold or Toggle Spirit Flame to fire continuously, or Off for vanilla.");
        AddEnum(RandomizerSettings.QOL.DebugControls, "Toggles fast Debug movement.");
        AddToggle(RandomizerSettings.Controls.SwimmingMouseAim, "Enables directing Ori through water with the mouse.");
        AddToggle(RandomizerSettings.Controls.InvertSwim, "Enable to swim fast by default (hold [Jump] to swim slower).");
        AddToggle(RandomizerSettings.Controls.WallChargeMouseAim, "Enables Wall Charge Jump mouse aiming.");
        AddToggle(RandomizerSettings.Controls.InvertClimb, "Enable to invert Climb input (Climb by default; hold [Climb] to let go).");
    }
}

public class ControllerBindsScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddControllerBind("Bash", () => PlayerInputRebinding.ControllerRebindingSettings.Bash, k => PlayerInputRebinding.ControllerRebindingSettings.Bash = k);
        AddControllerBind("Charge Jump", () => PlayerInputRebinding.ControllerRebindingSettings.ChargeJump, k => PlayerInputRebinding.ControllerRebindingSettings.ChargeJump = k);
        AddControllerBind("Dash", () => PlayerInputRebinding.ControllerRebindingSettings.RightShoulder, k => PlayerInputRebinding.ControllerRebindingSettings.RightShoulder = k);
        AddControllerBind("Glide", () => PlayerInputRebinding.ControllerRebindingSettings.Glide, k => PlayerInputRebinding.ControllerRebindingSettings.Glide = k);
        AddControllerBind("Grab", () => PlayerInputRebinding.ControllerRebindingSettings.Grab, k => PlayerInputRebinding.ControllerRebindingSettings.Grab = k);
        AddControllerBind("Grenade", () => PlayerInputRebinding.ControllerRebindingSettings.LeftShoulder, k => PlayerInputRebinding.ControllerRebindingSettings.LeftShoulder = k);
        AddControllerBind("Jump", () => PlayerInputRebinding.ControllerRebindingSettings.Jump, k => PlayerInputRebinding.ControllerRebindingSettings.Jump = k);
        AddControllerBind("Soul Link", () => PlayerInputRebinding.ControllerRebindingSettings.SoulFlame, k => PlayerInputRebinding.ControllerRebindingSettings.SoulFlame = k);
        AddControllerBind("Spirit Flame", () => PlayerInputRebinding.ControllerRebindingSettings.SpiritFlame, k => PlayerInputRebinding.ControllerRebindingSettings.SpiritFlame = k);
        AddControllerBind("Stomp", () => PlayerInputRebinding.ControllerRebindingSettings.Stomp, k => PlayerInputRebinding.ControllerRebindingSettings.Stomp = k);
        AddControllerBind("Movement Up", () => PlayerInputRebinding.ControllerRebindingSettings.VerticalDigiPadUp, k => PlayerInputRebinding.ControllerRebindingSettings.VerticalDigiPadUp = k);
        AddControllerBind("Movement Down", () => PlayerInputRebinding.ControllerRebindingSettings.VerticalDigiPadDown, k => PlayerInputRebinding.ControllerRebindingSettings.VerticalDigiPadDown = k);
        AddControllerBind("Movement Left", () => PlayerInputRebinding.ControllerRebindingSettings.HorizontalDigiPadLeft, k => PlayerInputRebinding.ControllerRebindingSettings.HorizontalDigiPadLeft = k);
        AddControllerBind("Movement Right", () => PlayerInputRebinding.ControllerRebindingSettings.HorizontalDigiPadRight, k => PlayerInputRebinding.ControllerRebindingSettings.HorizontalDigiPadRight = k);

        // Lower tooltip so it fits under the options
        var pos = TooltipController.transform.position;
        pos.y = -3.38f;
        TooltipController.transform.position = pos;
        HideLegend();
    }
}

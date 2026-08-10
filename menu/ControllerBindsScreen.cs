public class ControllerBindsScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddControllerBind("Bash", () => PlayerInputRebinding.ControllerRebindings.Bash, k => PlayerInputRebinding.ControllerRebindings.Bash = k);
        AddControllerBind("Charge Jump", () => PlayerInputRebinding.ControllerRebindings.ChargeJump, k => PlayerInputRebinding.ControllerRebindings.ChargeJump = k);
        AddControllerBind("Dash", () => PlayerInputRebinding.ControllerRebindings.RightShoulder, k => PlayerInputRebinding.ControllerRebindings.RightShoulder = k);
        AddControllerBind("Glide", () => PlayerInputRebinding.ControllerRebindings.Glide, k => PlayerInputRebinding.ControllerRebindings.Glide = k);
        AddControllerBind("Grab", () => PlayerInputRebinding.ControllerRebindings.Grab, k => PlayerInputRebinding.ControllerRebindings.Grab = k);
        AddControllerBind("Grenade", () => PlayerInputRebinding.ControllerRebindings.LeftShoulder, k => PlayerInputRebinding.ControllerRebindings.LeftShoulder = k);
        AddControllerBind("Jump", () => PlayerInputRebinding.ControllerRebindings.Jump, k => PlayerInputRebinding.ControllerRebindings.Jump = k);
        AddControllerBind("Soul Link", () => PlayerInputRebinding.ControllerRebindings.SoulFlame, k => PlayerInputRebinding.ControllerRebindings.SoulFlame = k);
        AddControllerBind("Spirit Flame", () => PlayerInputRebinding.ControllerRebindings.SpiritFlame, k => PlayerInputRebinding.ControllerRebindings.SpiritFlame = k);
        AddControllerBind("Stomp", () => PlayerInputRebinding.ControllerRebindings.Stomp, k => PlayerInputRebinding.ControllerRebindings.Stomp = k);
        AddControllerBind("Movement Up", () => PlayerInputRebinding.ControllerRebindings.VerticalDigiPadUp, k => PlayerInputRebinding.ControllerRebindings.VerticalDigiPadUp = k);
        AddControllerBind("Movement Down", () => PlayerInputRebinding.ControllerRebindings.VerticalDigiPadDown, k => PlayerInputRebinding.ControllerRebindings.VerticalDigiPadDown = k);
        AddControllerBind("Movement Left", () => PlayerInputRebinding.ControllerRebindings.HorizontalDigiPadLeft, k => PlayerInputRebinding.ControllerRebindings.HorizontalDigiPadLeft = k);
        AddControllerBind("Movement Right", () => PlayerInputRebinding.ControllerRebindings.HorizontalDigiPadRight, k => PlayerInputRebinding.ControllerRebindings.HorizontalDigiPadRight = k);

        // Lower tooltip so it fits under the options
        var pos = tooltipController.transform.position;
        pos.y = -3.38f;
        tooltipController.transform.position = pos;
        HideLegend();
    }
}

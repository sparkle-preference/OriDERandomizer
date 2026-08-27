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

        AddControllerBind("Pause", () => PlayerInputRebinding.ControllerRebindings.Start, k => PlayerInputRebinding.ControllerRebindings.Start = k);
        AddControllerBind("Cancel", () => PlayerInputRebinding.ControllerRebindings.Cancel, k => PlayerInputRebinding.ControllerRebindings.Cancel = k);
        AddControllerBind("Proceed", () => PlayerInputRebinding.ControllerRebindings.ActionButtonA, k => PlayerInputRebinding.ControllerRebindings.ActionButtonA = k);
        AddControllerBind("Menu Up", () => PlayerInputRebinding.ControllerRebindings.MenuUp, k => PlayerInputRebinding.ControllerRebindings.MenuUp = k);
        AddControllerBind("Menu Down", () => PlayerInputRebinding.ControllerRebindings.MenuDown, k => PlayerInputRebinding.ControllerRebindings.MenuDown = k);
        AddControllerBind("Menu Left", () => PlayerInputRebinding.ControllerRebindings.MenuLeft, k => PlayerInputRebinding.ControllerRebindings.MenuLeft = k);
        AddControllerBind("Menu Right", () => PlayerInputRebinding.ControllerRebindings.MenuRight, k => PlayerInputRebinding.ControllerRebindings.MenuRight = k);
        AddControllerBind("Menu Next", () => PlayerInputRebinding.ControllerRebindings.MenuPageRight, k => PlayerInputRebinding.ControllerRebindings.MenuPageRight = k);
        AddControllerBind("Menu Previous", () => PlayerInputRebinding.ControllerRebindings.MenuPageLeft, k => PlayerInputRebinding.ControllerRebindings.MenuPageLeft = k);
        AddControllerBind("Map", () => PlayerInputRebinding.ControllerRebindings.Select, k => PlayerInputRebinding.ControllerRebindings.Select = k);
        AddControllerBind("Zoom In (Map)", () => PlayerInputRebinding.ControllerRebindings.ZoomIn, k => PlayerInputRebinding.ControllerRebindings.ZoomIn = k);
        AddControllerBind("Zoom Out (Map)", () => PlayerInputRebinding.ControllerRebindings.ZoomOut, k => PlayerInputRebinding.ControllerRebindings.ZoomOut = k);
        AddButton("Reset Keybinds", ResetKeybinds, "Puts every controller bind on this screen back to its default.");

        ScrollAfter(12);

        // Lower tooltip so it fits under the options
        var pos = tooltipController.transform.position;
        pos.y = -3.38f;
        tooltipController.transform.position = pos;
        HideLegend();
    }

    private void ResetKeybinds() {
        PlayerInputRebinding.SetDefaultControllerBindingSettings();
        var instance = PlayerInput.Instance;
        if (instance != null) {
            instance.RefreshControlScheme();
        }

        var componentsInChildren = OptionsScreen.Instance.transform.GetComponentsInChildren<ControllerBindControl>(true);
        for (var i = 0; i < componentsInChildren.Length; i++) {
            componentsInChildren[i].Reset();
        }

        PlayerInputRebinding.WriteControllerRebindSettings();
    }
}

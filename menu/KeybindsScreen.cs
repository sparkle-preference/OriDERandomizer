public class KeybindsScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddKeybind("Bash", () => PlayerInputRebinding.KeyRebindings.Bash, k => PlayerInputRebinding.KeyRebindings.Bash = k);
        AddKeybind("Charge Jump", () => PlayerInputRebinding.KeyRebindings.ChargeJump, k => PlayerInputRebinding.KeyRebindings.ChargeJump = k);
        AddKeybind("Dash", () => PlayerInputRebinding.KeyRebindings.RightShoulder, k => PlayerInputRebinding.KeyRebindings.RightShoulder = k);
        AddKeybind("Glide", () => PlayerInputRebinding.KeyRebindings.Glide, k => PlayerInputRebinding.KeyRebindings.Glide = k);
        AddKeybind("Grab", () => PlayerInputRebinding.KeyRebindings.Grab, k => PlayerInputRebinding.KeyRebindings.Grab = k);
        AddKeybind("Grenade", () => PlayerInputRebinding.KeyRebindings.LeftShoulder, k => PlayerInputRebinding.KeyRebindings.LeftShoulder = k);
        AddKeybind("Jump", () => PlayerInputRebinding.KeyRebindings.Jump, k => PlayerInputRebinding.KeyRebindings.Jump = k);
        AddKeybind("Soul Link", () => PlayerInputRebinding.KeyRebindings.SoulFlame, k => PlayerInputRebinding.KeyRebindings.SoulFlame = k);
        AddKeybind("Spirit Flame", () => PlayerInputRebinding.KeyRebindings.SpiritFlame, k => PlayerInputRebinding.KeyRebindings.SpiritFlame = k);
        AddKeybind("Stomp", () => PlayerInputRebinding.KeyRebindings.Stomp, k => PlayerInputRebinding.KeyRebindings.Stomp = k);
        AddKeybind("Movement Up", () => PlayerInputRebinding.KeyRebindings.VerticalDigiPadUp, k => PlayerInputRebinding.KeyRebindings.VerticalDigiPadUp = k);
        AddKeybind("Movement Down", () => PlayerInputRebinding.KeyRebindings.VerticalDigiPadDown, k => PlayerInputRebinding.KeyRebindings.VerticalDigiPadDown = k);
        AddKeybind("Movement Left", () => PlayerInputRebinding.KeyRebindings.HorizontalDigiPadLeft, k => PlayerInputRebinding.KeyRebindings.HorizontalDigiPadLeft = k);
        AddKeybind("Movement Right", () => PlayerInputRebinding.KeyRebindings.HorizontalDigiPadRight, k => PlayerInputRebinding.KeyRebindings.HorizontalDigiPadRight = k);

        AddKeybind("Pause", () => PlayerInputRebinding.KeyRebindings.Start, k => PlayerInputRebinding.KeyRebindings.Start = k);
        AddKeybind("Cancel", () => PlayerInputRebinding.KeyRebindings.Cancel, k => PlayerInputRebinding.KeyRebindings.Cancel = k);
        AddKeybind("Proceed", () => PlayerInputRebinding.KeyRebindings.ActionButtonA, k => PlayerInputRebinding.KeyRebindings.ActionButtonA = k);
        AddKeybind("Menu Up", () => PlayerInputRebinding.KeyRebindings.MenuUp, k => PlayerInputRebinding.KeyRebindings.MenuUp = k);
        AddKeybind("Menu Down", () => PlayerInputRebinding.KeyRebindings.MenuDown, k => PlayerInputRebinding.KeyRebindings.MenuDown = k);
        AddKeybind("Menu Left", () => PlayerInputRebinding.KeyRebindings.MenuLeft, k => PlayerInputRebinding.KeyRebindings.MenuLeft = k);
        AddKeybind("Menu Right", () => PlayerInputRebinding.KeyRebindings.MenuRight, k => PlayerInputRebinding.KeyRebindings.MenuRight = k);
        AddKeybind("Menu Previous", () => PlayerInputRebinding.KeyRebindings.MenuPageLeft, k => PlayerInputRebinding.KeyRebindings.MenuPageLeft = k);
        AddKeybind("Menu Next", () => PlayerInputRebinding.KeyRebindings.MenuPageRight, k => PlayerInputRebinding.KeyRebindings.MenuPageRight = k);
        AddKeybind("Map", () => PlayerInputRebinding.KeyRebindings.Select, k => PlayerInputRebinding.KeyRebindings.Select = k);
        AddKeybind("Zoom In (Map)", () => PlayerInputRebinding.KeyRebindings.ZoomIn, k => PlayerInputRebinding.KeyRebindings.ZoomIn = k);
        AddKeybind("Zoom Out (Map)", () => PlayerInputRebinding.KeyRebindings.ZoomOut, k => PlayerInputRebinding.KeyRebindings.ZoomOut = k);
        AddButton("Reset Keybinds", ResetKeybinds, "Puts every keyboard bind on this screen back to its default.");

        ScrollAfter(12);

        // Lower tooltip so it fits under the options
        var pos = tooltipController.transform.position;
        pos.y = -3.38f;
        tooltipController.transform.position = pos;
        HideLegend();
    }

    private void ResetKeybinds() {
        PlayerInputRebinding.SetDefaultKeyBindingSettings();
        var instance = PlayerInput.Instance;
        if (instance != null) {
            instance.RefreshControlScheme();
        }

        var componentsInChildren = OptionsScreen.Instance.transform.GetComponentsInChildren<KeybindControl>(true);
        for (var i = 0; i < componentsInChildren.Length; i++) {
            componentsInChildren[i].Reset();
        }

        PlayerInputRebinding.WriteKeyRebindSettings();
    }
}

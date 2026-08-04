public class ControllerMenuBindsScreen : CustomSettingsScreen {
    public override void InitScreen() {
        AddControllerBind("Pause", () => PlayerInputRebinding.ControllerRebindingSettings.Start, k => PlayerInputRebinding.ControllerRebindingSettings.Start = k);
        AddControllerBind("Cancel", () => PlayerInputRebinding.ControllerRebindingSettings.Cancel, k => PlayerInputRebinding.ControllerRebindingSettings.Cancel = k);
        AddControllerBind("Proceed", () => PlayerInputRebinding.ControllerRebindingSettings.ActionButtonA, k => PlayerInputRebinding.ControllerRebindingSettings.ActionButtonA = k);
        AddControllerBind("Menu Up", () => PlayerInputRebinding.ControllerRebindingSettings.MenuUp, k => PlayerInputRebinding.ControllerRebindingSettings.MenuUp = k);
        AddControllerBind("Menu Down", () => PlayerInputRebinding.ControllerRebindingSettings.MenuDown, k => PlayerInputRebinding.ControllerRebindingSettings.MenuDown = k);
        AddControllerBind("Menu Left", () => PlayerInputRebinding.ControllerRebindingSettings.MenuLeft, k => PlayerInputRebinding.ControllerRebindingSettings.MenuLeft = k);
        AddControllerBind("Menu Right", () => PlayerInputRebinding.ControllerRebindingSettings.MenuRight, k => PlayerInputRebinding.ControllerRebindingSettings.MenuRight = k);
        AddControllerBind("Menu Next", () => PlayerInputRebinding.ControllerRebindingSettings.MenuPageRight, k => PlayerInputRebinding.ControllerRebindingSettings.MenuPageRight = k);
        AddControllerBind("Menu Previous", () => PlayerInputRebinding.ControllerRebindingSettings.MenuPageLeft, k => PlayerInputRebinding.ControllerRebindingSettings.MenuPageLeft = k);
        AddControllerBind("Map", () => PlayerInputRebinding.ControllerRebindingSettings.Select, k => PlayerInputRebinding.ControllerRebindingSettings.Select = k);
        AddControllerBind("Zoom In (Map)", () => PlayerInputRebinding.ControllerRebindingSettings.ZoomIn, k => PlayerInputRebinding.ControllerRebindingSettings.ZoomIn = k);
        AddControllerBind("Zoom Out (Map)", () => PlayerInputRebinding.ControllerRebindingSettings.ZoomOut, k => PlayerInputRebinding.ControllerRebindingSettings.ZoomOut = k);
        AddButton("Reset Keybinds", ResetKeybinds);

        // Lower tooltip so it fits under the options
        var pos = TooltipController.transform.position;
        pos.y = -3.38f;
        TooltipController.transform.position = pos;
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

public class MenuKeybindsScreen : CustomSettingsScreen
{
	public override void InitScreen()
	{
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
		AddButton("Reset Keybinds", ResetKeybinds);

		// Lower tooltip so it fits under the options
		var pos = tooltipController.transform.position;
		pos.y = -3.38f;
		tooltipController.transform.position = pos;
		HideLegend();
	}

    private void ResetKeybinds()
	{
		PlayerInputRebinding.SetDefaultKeyBindingSettings();
		var instance = PlayerInput.Instance;
		if (instance != null)
		{
			instance.RefreshControlScheme();
		}
		var componentsInChildren = OptionsScreen.Instance.transform.GetComponentsInChildren<KeybindControl>(true);
		for (var i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].Reset();
		}
		PlayerInputRebinding.WriteKeyRebindSettings();
	}
}

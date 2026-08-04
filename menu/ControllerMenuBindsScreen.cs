public class ControllerMenuBindsScreen : CustomSettingsScreen
{
	public override void InitScreen()
	{
		AddControllerBind("Pause", () => PlayerInputRebinding.ControllerRebindings.Start, k =>	PlayerInputRebinding.ControllerRebindings.Start = k);
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
		AddButton("Reset Keybinds", ResetKeybinds);

		// Lower tooltip so it fits under the options
		var pos = tooltipController.transform.position;
		pos.y = -3.38f;
		tooltipController.transform.position = pos;
		HideLegend();
	}

	private void ResetKeybinds()
	{
		PlayerInputRebinding.SetDefaultControllerBindingSettings();
		var instance = PlayerInput.Instance;
		if (instance != null)
		{
			instance.RefreshControlScheme();
		}
		var componentsInChildren = OptionsScreen.Instance.transform.GetComponentsInChildren<ControllerBindControl>(true);
		for (var i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].Reset();
		}
		PlayerInputRebinding.WriteControllerRebindSettings();
	}
}

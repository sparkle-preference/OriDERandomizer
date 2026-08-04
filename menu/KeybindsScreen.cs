public class KeybindsScreen : CustomSettingsScreen
{
	public override void InitScreen()
	{
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

		// Lower tooltip so it fits under the options
		var pos = tooltipController.transform.position;
		pos.y = -3.38f;
		tooltipController.transform.position = pos;
		HideLegend();
	}
}
using System;
using System.Collections.Generic;
using SmartInput;
using UnityEngine;

public class ControllerBindControl : MonoBehaviour
{
	public void Awake()
	{
		messageBox = transform.Find("text/stateText").GetComponent<MessageBox>();
	}

	public void BeginEditing()
	{
		currentKeys.Clear();
		UpdateMessageBox();
		SuspensionManager.SuspendAll();
		editing = true;
		exit = 0;
		allButtons = (XboxControllerInput.Button[])Enum.GetValues(typeof(XboxControllerInput.Button));
		buttonsPressed = new bool[allButtons.Length];
		for (int i = 0; i < buttonsPressed.Length; i++)
		{
			buttonsPressed[i] = true;
		}
		tooltipProvider.SetMessage("Start: finish editing");
		owner.tooltipController.UpdateTooltip();
	}

	public void Update()
	{
		if (!editing)
		{
			return;
		}
		if (exit < 2)
		{
			exit++;
			return;
		}
		if (Input.GetKeyDown(KeyCode.Escape) || (WasPressed(XboxControllerInput.Button.Start) && currentKeys.Count > 0))
		{
			editing = false;
			SuspensionManager.ResumeAll();
			SetKeys(currentKeys.ToArray());
			PlayerInputRebinding.WriteControllerRebindSettings();
			PlayerInput.Instance.RefreshControlScheme();
			tooltipProvider.SetMessage(owner.DefaultTooltip);
			owner.tooltipController.UpdateTooltip();
			return;
		}
		PlayerInputRebinding.ControllerButton? pressedButtonAsBind = GetPressedButtonAsBind();
		if (pressedButtonAsBind != null && !currentKeys.Contains(pressedButtonAsBind.Value))
		{
			currentKeys.Add(pressedButtonAsBind.Value);
			UpdateMessageBox();
		}
		foreach (XboxControllerInput.Button button in allButtons)
		{
			buttonsPressed[(int)button] = XboxControllerInput.GetButton(button);
		}
	}

	public void UpdateMessageBox()
	{
		messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(currentKeys.ToArray())));
	}

	public static string KeyBindingToString(PlayerInputRebinding.ControllerButton[] codes)
	{
		string text = string.Empty;
		bool flag = true;
		foreach (PlayerInputRebinding.ControllerButton controllerButton in codes)
		{
			text += !flag ? ", " : string.Empty;
			text += controllerButton;
			flag = false;
		}
		return text;
	}

	public void Reset()
	{
		messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(GetKeys())));
		editing = false;
	}

	private bool WasPressed(XboxControllerInput.Button button)
	{
		return !buttonsPressed[(int)button] && XboxControllerInput.GetButton(button);
	}

	private PlayerInputRebinding.ControllerButton ToBind(XboxControllerInput.Button button)
	{
		switch (button)
		{
		case XboxControllerInput.Button.ButtonA:
			return PlayerInputRebinding.ControllerButton.A;
		case XboxControllerInput.Button.ButtonX:
			return PlayerInputRebinding.ControllerButton.X;
		case XboxControllerInput.Button.ButtonY:
			return PlayerInputRebinding.ControllerButton.Y;
		case XboxControllerInput.Button.ButtonB:
			return PlayerInputRebinding.ControllerButton.B;
		case XboxControllerInput.Button.LeftTrigger:
			return PlayerInputRebinding.ControllerButton.LT;
		case XboxControllerInput.Button.RightTrigger:
			return PlayerInputRebinding.ControllerButton.RT;
		case XboxControllerInput.Button.LeftShoulder:
			return PlayerInputRebinding.ControllerButton.LB;
		case XboxControllerInput.Button.RightShoulder:
			return PlayerInputRebinding.ControllerButton.RB;
		case XboxControllerInput.Button.LeftStick:
			return PlayerInputRebinding.ControllerButton.LS;
		case XboxControllerInput.Button.RightStick:
			return PlayerInputRebinding.ControllerButton.RS;
		case XboxControllerInput.Button.Select:
			return PlayerInputRebinding.ControllerButton.Back;
		case XboxControllerInput.Button.Start:
			return PlayerInputRebinding.ControllerButton.Start;
		default:
			return PlayerInputRebinding.ControllerButton.A;
		}
	}

	public PlayerInputRebinding.ControllerButton? GetPressedButtonAsBind()
	{
		foreach (XboxControllerInput.Button button in allButtons)
		{
			if (WasPressed(button))
			{
				return ToBind(button);
			}
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.LeftStickX) < -0.5f)
		{
			return PlayerInputRebinding.ControllerButton.LLeft;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.LeftStickX) > 0.5f)
		{
			return PlayerInputRebinding.ControllerButton.LRight;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.LeftStickY) > 0.5f)
		{
			return PlayerInputRebinding.ControllerButton.LUp;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.LeftStickY) < -0.5f)
		{
			return PlayerInputRebinding.ControllerButton.LDown;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.RightStickX) < -0.5f)
		{
			return PlayerInputRebinding.ControllerButton.RLeft;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.RightStickX) > 0.5f)
		{
			return PlayerInputRebinding.ControllerButton.RRight;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.RightStickY) > 0.5f)
		{
			return PlayerInputRebinding.ControllerButton.RUp;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.RightStickY) < -0.5f)
		{
			return PlayerInputRebinding.ControllerButton.RDown;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.DpadX) < -0.5f)
		{
			return PlayerInputRebinding.ControllerButton.DLeft;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.DpadX) > 0.5f)
		{
			return PlayerInputRebinding.ControllerButton.DRight;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.DpadY) > 0.5f)
		{
			return PlayerInputRebinding.ControllerButton.DUp;
		}
		if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.DpadY) < -0.5f)
		{
			return PlayerInputRebinding.ControllerButton.DDown;
		}
		return null;
	}

	public void Init(Func<PlayerInputRebinding.ControllerButton[]> getKeys, Action<PlayerInputRebinding.ControllerButton[]> setKeys, CustomSettingsScreen owner)
	{
		this.owner = owner;
		GetKeys = getKeys;
		SetKeys = setKeys;
		messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(getKeys())));
		CleverMenuItemTooltip component = GetComponent<CleverMenuItemTooltip>();
		tooltipProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
		tooltipProvider.SetMessage(owner.DefaultTooltip);
		component.Tooltip = tooltipProvider;
		owner.tooltipController.UpdateTooltip();
	}

	public Func<PlayerInputRebinding.ControllerButton[]> GetKeys;

	public Action<PlayerInputRebinding.ControllerButton[]> SetKeys;

	public bool editing;

	public MessageBox messageBox;

	public List<PlayerInputRebinding.ControllerButton> currentKeys = new List<PlayerInputRebinding.ControllerButton>();

	public int exit;

	private bool[] buttonsPressed;

	private XboxControllerInput.Button[] allButtons;

	private CustomSettingsScreen owner;

	private RandomizerMessageProvider tooltipProvider;
}

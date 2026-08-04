using System;
using System.Collections.Generic;
using SmartInput;
using UnityEngine;
using UnityEngine.Serialization;

public class ControllerBindControl : MonoBehaviour {
    public void Awake() {
        MessageBox = transform.Find("text/stateText").GetComponent<MessageBox>();
    }

    public void BeginEditing() {
        CurrentKeys.Clear();
        UpdateMessageBox();
        SuspensionManager.SuspendAll();
        Editing = true;
        Exit = 0;
        allButtons = (XboxControllerInput.Button[])Enum.GetValues(typeof(XboxControllerInput.Button));
        buttonsPressed = new bool[allButtons.Length];
        for (var i = 0; i < buttonsPressed.Length; i++) {
            buttonsPressed[i] = true;
        }

        tooltipProvider.SetMessage("Start: finish editing");
        owner.TooltipController.UpdateTooltip();
    }

    public void Update() {
        if (!Editing) {
            return;
        }

        if (Exit < 2) {
            Exit++;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || (WasPressed(XboxControllerInput.Button.Start) && CurrentKeys.Count > 0)) {
            Editing = false;
            SuspensionManager.ResumeAll();
            SetKeys(CurrentKeys.ToArray());
            PlayerInputRebinding.WriteControllerRebindSettings();
            PlayerInput.Instance.RefreshControlScheme();
            tooltipProvider.SetMessage(owner.DefaultTooltip);
            owner.TooltipController.UpdateTooltip();
            return;
        }

        var pressedButtonAsBind = GetPressedButtonAsBind();
        if (pressedButtonAsBind != null && !CurrentKeys.Contains(pressedButtonAsBind.Value)) {
            CurrentKeys.Add(pressedButtonAsBind.Value);
            UpdateMessageBox();
        }

        foreach (var button in allButtons) {
            buttonsPressed[(int)button] = XboxControllerInput.GetButton(button);
        }
    }

    public void UpdateMessageBox() {
        MessageBox.SetMessage(new MessageDescriptor(KeyBindingToString(CurrentKeys.ToArray())));
    }

    public static string KeyBindingToString(PlayerInputRebinding.ControllerButton[] codes) {
        var text = string.Empty;
        var flag = true;
        foreach (var controllerButton in codes) {
            text += !flag ? ", " : string.Empty;
            text += controllerButton;
            flag = false;
        }

        return text;
    }

    public void Reset() {
        MessageBox.SetMessage(new MessageDescriptor(KeyBindingToString(GetKeys())));
        Editing = false;
    }

    private bool WasPressed(XboxControllerInput.Button button) {
        return !buttonsPressed[(int)button] && XboxControllerInput.GetButton(button);
    }

    private PlayerInputRebinding.ControllerButton ToBind(XboxControllerInput.Button button) {
        switch (button) {
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

    public PlayerInputRebinding.ControllerButton? GetPressedButtonAsBind() {
        foreach (var button in allButtons) {
            if (WasPressed(button)) {
                return ToBind(button);
            }
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.LeftStickX) < -0.5f) {
            return PlayerInputRebinding.ControllerButton.LLeft;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.LeftStickX) > 0.5f) {
            return PlayerInputRebinding.ControllerButton.LRight;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.LeftStickY) > 0.5f) {
            return PlayerInputRebinding.ControllerButton.LUp;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.LeftStickY) < -0.5f) {
            return PlayerInputRebinding.ControllerButton.LDown;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.RightStickX) < -0.5f) {
            return PlayerInputRebinding.ControllerButton.RLeft;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.RightStickX) > 0.5f) {
            return PlayerInputRebinding.ControllerButton.RRight;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.RightStickY) > 0.5f) {
            return PlayerInputRebinding.ControllerButton.RUp;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.RightStickY) < -0.5f) {
            return PlayerInputRebinding.ControllerButton.RDown;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.DpadX) < -0.5f) {
            return PlayerInputRebinding.ControllerButton.DLeft;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.DpadX) > 0.5f) {
            return PlayerInputRebinding.ControllerButton.DRight;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.DpadY) > 0.5f) {
            return PlayerInputRebinding.ControllerButton.DUp;
        }

        if (XboxControllerInput.GetAxis(XboxControllerInput.Axis.DpadY) < -0.5f) {
            return PlayerInputRebinding.ControllerButton.DDown;
        }

        return null;
    }

    public void Init(Func<PlayerInputRebinding.ControllerButton[]> getKeys, Action<PlayerInputRebinding.ControllerButton[]> setKeys, CustomSettingsScreen owner) {
        this.owner = owner;
        GetKeys = getKeys;
        SetKeys = setKeys;
        MessageBox.SetMessage(new MessageDescriptor(KeyBindingToString(getKeys())));
        var component = GetComponent<CleverMenuItemTooltip>();
        tooltipProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        tooltipProvider.SetMessage(owner.DefaultTooltip);
        component.Tooltip = tooltipProvider;
        owner.TooltipController.UpdateTooltip();
    }

    public Func<PlayerInputRebinding.ControllerButton[]> GetKeys;

    public Action<PlayerInputRebinding.ControllerButton[]> SetKeys;

    public bool Editing;

    public MessageBox MessageBox;

    public List<PlayerInputRebinding.ControllerButton> CurrentKeys = new List<PlayerInputRebinding.ControllerButton>();

    public int Exit;

    private bool[] buttonsPressed;

    private XboxControllerInput.Button[] allButtons;

    private CustomSettingsScreen owner;

    private RandomizerMessageProvider tooltipProvider;
}

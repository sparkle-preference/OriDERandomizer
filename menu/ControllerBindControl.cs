using System;
using System.Collections.Generic;
using SmartInput;
using UnityEngine;

public class ControllerBindControl : MonoBehaviour {
    public void Awake() {
        messageBox = transform.Find("text/stateText").GetComponent<MessageBox>();
    }

    public void BeginEditing() {
        currentKeys.Clear();
        UpdateMessageBox();
        SuspensionManager.SuspendAll();
        editing = true;
        owner.Editing = true;
        exit = 0;
        allButtons = (XboxControllerInput.Button[])Enum.GetValues(typeof(XboxControllerInput.Button));
        buttonsPressed = new bool[allButtons.Length];
        for (var i = 0; i < buttonsPressed.Length; i++) {
            buttonsPressed[i] = true;
        }

        back.Reset();
        tapped = false;
        // the legend says how to edit; the tooltip names what is being edited
        owner.BindLegend();
        Tooltip("Editing binds for " + label + "...");
    }

    private void Tooltip(string words) {
        tooltipProvider.SetMessage(words);
        owner.tooltipController.UpdateTooltip();
    }

    public void Update() {
        if (!editing) {
            return;
        }

        if (exit < 2) {
            exit++;
            return;
        }

        // a button pressed during a Back hold joins the binds; Back itself is no button
        var pressed = GetPressedButtonAsBind();
        if (pressed != null && !currentKeys.Contains(pressed.Value)) {
            back.Join();
        }

        // A Back hold cancels the edit; a tap finishes it, on release.
        if (back.Update(CustomSettingsScreen.BackHeld(), Time.unscaledTime)) {
            return;
        }

        // the edit starts empty, so finishing before any button would unbind the action
        if (tapped && currentKeys.Count == 0) {
            tapped = false;
            Cancel();
            return;
        }

        if (tapped || (WasPressed(XboxControllerInput.Button.Start) && currentKeys.Count > 0 && !back.Joined)) {
            tapped = false;
            editing = false;
            owner.Editing = false;
            SuspensionManager.ResumeAll();
            SetKeys(currentKeys.ToArray());
            PlayerInputRebinding.WriteControllerRebindSettings();
            var input = PlayerInput.Instance;
            if (input != null) {
                input.RefreshControlScheme();
            }

            owner.BindLegend();
            Tooltip(owner.DefaultTooltip);
            return;
        }

        if (pressed != null && !currentKeys.Contains(pressed.Value)) {
            currentKeys.Add(pressed.Value);
            UpdateMessageBox();
        }

        foreach (var button in allButtons) {
            buttonsPressed[(int)button] = XboxControllerInput.GetButton(button);
        }
    }

    // nothing is applied until the edit finishes, so cancelling only redraws the row
    private void Cancel() {
        editing = false;
        owner.Editing = false;
        SuspensionManager.ResumeAll();
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(GetKeys())));
        owner.BindLegend();
        Tooltip(owner.DefaultTooltip);
        owner.HoldMenu();
    }

    public void UpdateMessageBox() {
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(currentKeys.ToArray())));
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
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(GetKeys())));
        editing = false;
        owner.Editing = false;
    }

    private bool WasPressed(XboxControllerInput.Button button) {
        return !buttonsPressed[(int)button] && XboxControllerInput.GetButton(button);
    }

    // null for a button no ControllerButton names
    private PlayerInputRebinding.ControllerButton? ToBind(XboxControllerInput.Button button) {
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
                return null;
        }
    }

    public PlayerInputRebinding.ControllerButton? GetPressedButtonAsBind() {
        foreach (var button in allButtons) {
            var bind = WasPressed(button) ? ToBind(button) : null;
            if (bind != null) {
                return bind;
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

    public void Init(Func<PlayerInputRebinding.ControllerButton[]> getKeys, Action<PlayerInputRebinding.ControllerButton[]> setKeys, CustomSettingsScreen owner, string label) {
        this.owner = owner;
        this.label = label;
        GetKeys = getKeys;
        SetKeys = setKeys;
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(getKeys())));
        var component = GetComponent<CleverMenuItemTooltip>();
        tooltipProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        tooltipProvider.SetMessage(owner.DefaultTooltip);
        component.Tooltip = tooltipProvider;
        owner.tooltipController.UpdateTooltip();
        back = new BindHold(p => owner.DrawHold(p), owner.HideHold, key => tapped = true, key => { }, Cancel);
    }

    // the buttons the screen was entered with; Restore puts them back
    public void Snapshot() {
        snapshot = (PlayerInputRebinding.ControllerButton[])GetKeys().Clone();
    }

    public bool Changed {
        get {
            if (snapshot == null) {
                return false;
            }

            var now = GetKeys();
            if (now.Length != snapshot.Length) {
                return true;
            }

            for (var i = 0; i < now.Length; i++) {
                if (now[i] != snapshot[i]) {
                    return true;
                }
            }

            return false;
        }
    }

    public void Restore() {
        if (snapshot == null) {
            return;
        }

        SetKeys((PlayerInputRebinding.ControllerButton[])snapshot.Clone());
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(GetKeys())));
    }

    // this control is taking buttons; owner.Editing only says that *some* control is
    private bool editing;

    private PlayerInputRebinding.ControllerButton[] snapshot;

    public Func<PlayerInputRebinding.ControllerButton[]> GetKeys;

    public Action<PlayerInputRebinding.ControllerButton[]> SetKeys;


    public MessageBox messageBox;

    public List<PlayerInputRebinding.ControllerButton> currentKeys = new List<PlayerInputRebinding.ControllerButton>();

    public int exit;

    private bool[] buttonsPressed;

    private XboxControllerInput.Button[] allButtons;

    private CustomSettingsScreen owner;

    private string label;

    private BindHold back;

    // a Back tap came up this frame
    private bool tapped;

    private RandomizerMessageProvider tooltipProvider;
}

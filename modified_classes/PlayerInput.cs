using System.Collections.Generic;
using Game;
using SmartInput;
using UnityEngine;
using Input = Core.Input;

public class PlayerInput : MonoBehaviour {
    public PlayerInput() {
        m_lastPressedButtonInput = -1;
        m_lastPressedAxisInput = -1;
    }

    public void ClearControls() {
        HorizontalAnalogLeft.Clear();
        VerticalAnalogLeft.Clear();
        HorizontalAnalogRight.Clear();
        VerticalAnalogRight.Clear();
        HorizontalDigiPad.Clear();
        VerticalDigiPad.Clear();
        Jump.Clear();
        SpiritFlame.Clear();
        SoulFlame.Clear();
        Bash.Clear();
        ChargeJump.Clear();
        Glide.Clear();
        Grab.Clear();
        LeftShoulder.Clear();
        RightShoulder.Clear();
        Select.Clear();
        Start.Clear();
        LeftStick.Clear();
        RightStick.Clear();
        MenuDown.Clear();
        MenuUp.Clear();
        MenuLeft.Clear();
        MenuRight.Clear();
        MenuPageLeft.Clear();
        MenuPageRight.Clear();
        ActionButtonA.Clear();
        ZoomIn.Clear();
        ZoomOut.Clear();
        Cancel.Clear();
        Copy.Clear();
        Delete.Clear();
        Focus.Clear();
        Filter.Clear();
        Legend.Clear();
        Stomp.Clear();
    }

    public void AddXboxOneControls() {
    }

    public void AddControllerControls() {
        HorizontalAnalogLeft.Add(new ControllerAxisInput(XboxControllerInput.Axis.LeftStickX));
        VerticalAnalogLeft.Add(new ControllerAxisInput(XboxControllerInput.Axis.LeftStickY));
        HorizontalAnalogRight.Add(new ControllerAxisInput(XboxControllerInput.Axis.RightStickX));
        VerticalAnalogRight.Add(new ControllerAxisInput(XboxControllerInput.Axis.RightStickY));
        PlayerInputRebinding.ControllerBindingSettings controllerRebindings = PlayerInputRebinding.ControllerRebindings;
        foreach (PlayerInputRebinding.ControllerButton button in controllerRebindings.HorizontalDigiPadLeft) {
            HorizontalDigiPad.Add(new ButtonAxisInput(ControllerButtonToButtonInput(button), ButtonAxisInput.Mode.Negative));
        }

        foreach (PlayerInputRebinding.ControllerButton button2 in controllerRebindings.HorizontalDigiPadRight) {
            HorizontalDigiPad.Add(new ButtonAxisInput(ControllerButtonToButtonInput(button2), ButtonAxisInput.Mode.Positive));
        }

        foreach (PlayerInputRebinding.ControllerButton button3 in controllerRebindings.VerticalDigiPadDown) {
            VerticalDigiPad.Add(new ButtonAxisInput(ControllerButtonToButtonInput(button3), ButtonAxisInput.Mode.Negative));
        }

        foreach (PlayerInputRebinding.ControllerButton button4 in controllerRebindings.VerticalDigiPadUp) {
            VerticalDigiPad.Add(new ButtonAxisInput(ControllerButtonToButtonInput(button4), ButtonAxisInput.Mode.Positive));
        }

        AddControllerButtonsToButtonInput(controllerRebindings.Jump, Jump);
        AddControllerButtonsToButtonInput(controllerRebindings.SpiritFlame, SpiritFlame);
        AddControllerButtonsToButtonInput(controllerRebindings.SoulFlame, SoulFlame);
        AddControllerButtonsToButtonInput(controllerRebindings.Bash, Bash);
        AddControllerButtonsToButtonInput(controllerRebindings.ChargeJump, ChargeJump);
        AddControllerButtonsToButtonInput(controllerRebindings.ZoomIn, ZoomIn);
        AddControllerButtonsToButtonInput(controllerRebindings.Glide, Glide);
        AddControllerButtonsToButtonInput(controllerRebindings.Grab, Grab);
        AddControllerButtonsToButtonInput(controllerRebindings.ZoomOut, ZoomOut);
        AddControllerButtonsToButtonInput(controllerRebindings.LeftShoulder, LeftShoulder);
        AddControllerButtonsToButtonInput(controllerRebindings.RightShoulder, RightShoulder);
        AddControllerButtonsToButtonInput(controllerRebindings.Select, Select);
        AddControllerButtonsToButtonInput(controllerRebindings.Start, Start);
        AddControllerButtonsToButtonInput(controllerRebindings.LeftStick, LeftStick);
        AddControllerButtonsToButtonInput(controllerRebindings.RightStick, RightStick);
        AddControllerButtonsToButtonInput(controllerRebindings.MenuDown, MenuDown);
        AddControllerButtonsToButtonInput(controllerRebindings.MenuDown, MenuDown);
        AddControllerButtonsToButtonInput(controllerRebindings.MenuUp, MenuUp);
        AddControllerButtonsToButtonInput(controllerRebindings.MenuUp, MenuUp);
        AddControllerButtonsToButtonInput(controllerRebindings.MenuLeft, MenuLeft);
        AddControllerButtonsToButtonInput(controllerRebindings.MenuLeft, MenuLeft);
        AddControllerButtonsToButtonInput(controllerRebindings.MenuRight, MenuRight);
        AddControllerButtonsToButtonInput(controllerRebindings.MenuRight, MenuRight);
        AddControllerButtonsToButtonInput(controllerRebindings.ActionButtonA, ActionButtonA);
        AddControllerButtonsToButtonInput(controllerRebindings.Cancel, Cancel);
        AddControllerButtonsToButtonInput(controllerRebindings.MenuPageLeft, MenuPageLeft);
        AddControllerButtonsToButtonInput(controllerRebindings.MenuPageRight, MenuPageRight);
        AddControllerButtonsToButtonInput(controllerRebindings.Copy, Copy);
        AddControllerButtonsToButtonInput(controllerRebindings.Delete, Delete);
        AddControllerButtonsToButtonInput(controllerRebindings.Focus, Focus);
        AddControllerButtonsToButtonInput(controllerRebindings.Filter, Filter);
        AddControllerButtonsToButtonInput(controllerRebindings.Legend, Legend);
        AddControllerButtonsToButtonInput(controllerRebindings.Stomp, Stomp);
    }

    public void AddKeyboardControls() {
        PlayerInputRebinding.KeyBindingSettings keyRebindings = PlayerInputRebinding.KeyRebindings;
        foreach (KeyCode keyCode in keyRebindings.HorizontalDigiPadLeft) {
            HorizontalDigiPad.Add(new ButtonAxisInput(new KeyCodeButtonInput(keyCode), ButtonAxisInput.Mode.Negative));
        }

        foreach (KeyCode keyCode2 in keyRebindings.HorizontalDigiPadRight) {
            HorizontalDigiPad.Add(new ButtonAxisInput(new KeyCodeButtonInput(keyCode2), ButtonAxisInput.Mode.Positive));
        }

        foreach (KeyCode keyCode3 in keyRebindings.VerticalDigiPadDown) {
            VerticalDigiPad.Add(new ButtonAxisInput(new KeyCodeButtonInput(keyCode3), ButtonAxisInput.Mode.Negative));
        }

        foreach (KeyCode keyCode4 in keyRebindings.VerticalDigiPadUp) {
            VerticalDigiPad.Add(new ButtonAxisInput(new KeyCodeButtonInput(keyCode4), ButtonAxisInput.Mode.Positive));
        }

        AddKeyCodesToButtonInput(keyRebindings.MenuLeft, MenuLeft);
        AddKeyCodesToButtonInput(keyRebindings.MenuRight, MenuRight);
        AddKeyCodesToButtonInput(keyRebindings.MenuDown, MenuDown);
        AddKeyCodesToButtonInput(keyRebindings.MenuUp, MenuUp);
        AddKeyCodesToButtonInput(keyRebindings.MenuPageLeft, MenuPageLeft);
        AddKeyCodesToButtonInput(keyRebindings.MenuPageRight, MenuPageRight);
        AddKeyCodesToButtonInput(keyRebindings.ActionButtonA, ActionButtonA);
        AddKeyCodesToButtonInput(keyRebindings.SoulFlame, SoulFlame);
        AddKeyCodesToButtonInput(keyRebindings.Jump, Jump);
        AddKeyCodesToButtonInput(keyRebindings.Grab, Grab);
        AddKeyCodesToButtonInput(keyRebindings.SpiritFlame, SpiritFlame);
        AddKeyCodesToButtonInput(keyRebindings.Bash, Bash);
        AddKeyCodesToButtonInput(keyRebindings.Glide, Glide);
        AddKeyCodesToButtonInput(keyRebindings.ChargeJump, ChargeJump);
        AddKeyCodesToButtonInput(keyRebindings.Select, Select);
        AddKeyCodesToButtonInput(keyRebindings.Start, Start);
        AddKeyCodesToButtonInput(keyRebindings.Cancel, Cancel);
        AddKeyCodesToButtonInput(keyRebindings.LeftShoulder, LeftShoulder);
        AddKeyCodesToButtonInput(keyRebindings.RightShoulder, RightShoulder);
        AddKeyCodesToButtonInput(keyRebindings.LeftStick, LeftStick);
        AddKeyCodesToButtonInput(keyRebindings.RightStick, RightStick);
        AddKeyCodesToButtonInput(keyRebindings.ZoomIn, ZoomIn);
        AddKeyCodesToButtonInput(keyRebindings.ZoomOut, ZoomOut);
        AddKeyCodesToButtonInput(keyRebindings.Copy, Copy);
        AddKeyCodesToButtonInput(keyRebindings.Delete, Delete);
        AddKeyCodesToButtonInput(keyRebindings.Focus, Focus);
        AddKeyCodesToButtonInput(keyRebindings.Filter, Filter);
        AddKeyCodesToButtonInput(keyRebindings.Legend, Legend);
        AddKeyCodesToButtonInput(keyRebindings.Stomp, Stomp);
    }

    private void AddKeyCodesToButtonInput(KeyCode[] keyCodes, CompoundButtonInput buttonInput) {
        foreach (KeyCode keyCode in keyCodes) {
            buttonInput.Add(new KeyCodeButtonInput(keyCode));
        }
    }

    public void Awake() {
        Instance = this;
        RefreshControlScheme();
        LeftClick = new KeyCodeButtonInput(KeyCode.Mouse0);
        RightClick = new KeyCodeButtonInput(KeyCode.Mouse1);
        m_allButtonInput = new List<IButtonInput> {
            Jump,
            SpiritFlame,
            SoulFlame,
            Bash,
            ChargeJump,
            Glide,
            Grab,
            LeftShoulder,
            RightShoulder,
            Select,
            Start,
            LeftStick,
            RightStick,
            MenuDown,
            MenuUp,
            MenuLeft,
            MenuRight,
            MenuPageRight,
            MenuPageLeft,
            ActionButtonA,
            Cancel,
            Copy,
            Delete,
            Focus,
            Filter,
            Legend,
            Stomp
        };
        m_allButtonProcessor = new List<Input.InputButtonProcessor> {
            Input.Jump,
            Input.SpiritFlame,
            Input.SoulFlame,
            Input.Bash,
            Input.ChargeJump,
            Input.Glide,
            Input.Grab,
            Input.LeftShoulder,
            Input.RightShoulder,
            Input.Select,
            Input.Start,
            Input.LeftStick,
            Input.RightStick,
            Input.MenuDown,
            Input.MenuUp,
            Input.MenuLeft,
            Input.MenuRight,
            Input.MenuPageRight,
            Input.MenuPageLeft,
            Input.ActionButtonA,
            Input.Cancel,
            Input.Copy,
            Input.Delete,
            Input.Focus,
            Input.Filter,
            Input.Legend,
            Input.Stomp
        };
        m_allAxisInput = new List<IAxisInput> {
            HorizontalAnalogLeft,
            VerticalAnalogLeft,
            HorizontalAnalogRight,
            VerticalAnalogRight,
            HorizontalDigiPad,
            VerticalDigiPad
        };
    }

    public float SimplifyAxis(float x) {
        return Utility.Round(x, 0.001f);
    }

    public void ApplyDeadzone(ref float x, ref float y) {
        if (x * x + y * y < 0.0400000028f) {
            x = 0f;
            y = 0f;
        }
    }

    public void FixedUpdate() {
        if (!Active) {
            return;
        }

        Vector2 vector = UI.Cameras.Current.Camera.ScreenToViewportPoint(UnityEngine.Input.mousePosition);
        Input.CursorMoved = (Vector2.Distance(vector, Input.CursorPosition) > 0.0001f);
        Input.CursorPosition = vector;
        Input.HorizontalAnalogLeft = SimplifyAxis(HorizontalAnalogLeft.AxisValue());
        Input.VerticalAnalogLeft = SimplifyAxis(VerticalAnalogLeft.AxisValue());
        ApplyDeadzone(ref Input.HorizontalAnalogLeft, ref Input.VerticalAnalogLeft);
        Input.HorizontalAnalogRight = SimplifyAxis(HorizontalAnalogRight.AxisValue());
        Input.VerticalAnalogRight = SimplifyAxis(VerticalAnalogRight.AxisValue());
        ApplyDeadzone(ref Input.HorizontalAnalogRight, ref Input.VerticalAnalogRight);
        Input.HorizontalDigiPad = Mathf.RoundToInt(HorizontalDigiPad.AxisValue());
        Input.VerticalDigiPad = Mathf.RoundToInt(VerticalDigiPad.AxisValue());
        Input.AnyStart.Update(IsAnyStartPressed());
        Input.ZoomIn.Update(ZoomIn.GetButton());
        Input.ZoomOut.Update(ZoomOut.GetButton());
        Input.LeftClick.Update(LeftClick.GetButton());
        Input.RightClick.Update(RightClick.GetButton());
        m_lastPressedButtonInput = -1;
        for (int i = 0; i < m_allButtonInput.Count; i++) {
            bool button = m_allButtonInput[i].GetButton();
            if (button) {
                m_lastPressedButtonInput = i;
            }

            m_allButtonProcessor[i].Update(button);
        }

        RefreshControls();
        if (!ControlsScreen.IsVisible && m_lastPressedButtonInput != -1) {
            bool flag = WasKeyboardUsedLast;
            if (m_lastPressedButtonInput != -1) {
                flag = KeyboardUsedLast(m_allButtonInput[m_lastPressedButtonInput]);
            }

            if (flag != WasKeyboardUsedLast) {
                GameSettings.Instance.CurrentControlScheme = ((!flag) ? ControlScheme.Controller : GameSettings.Instance.KeyboardScheme);
            }
        }
    }

    public void RefreshControls() {
        Input.Horizontal = Mathf.Clamp(Input.HorizontalDigiPad + Input.HorizontalAnalogLeft, -1f, 1f);
        Input.Vertical = Mathf.Clamp(Input.VerticalDigiPad + Input.VerticalAnalogLeft, -1f, 1f);
        Input.Down.Update(Input.NormalizedVertical == -1f);
        Input.Up.Update(Input.NormalizedVertical == 1f);
        Input.Left.Update(Input.NormalizedHorizontal == -1);
        Input.Right.Update(Input.NormalizedHorizontal == 1);
        for (int i = 0; i < Input.Buttons.Length; i++) {
            Input.Buttons[i].Used = false;
        }

        RandomizerRebinding.FixedUpdate();
    }

    public void RefreshControlScheme() {
        ClearControls();
        AddControllerControls();
        AddXboxOneControls();
        AddKeyboardControls();
        PlayerInputRebinding.RefreshControllerButtonRemappings();
    }

    private void RefreshLastPressedButton() {
        m_lastPressedButtonInput = -1;
        m_lastPressedAxisInput = -1;
        for (int i = 0; i < m_allButtonInput.Count; i++) {
            if (m_allButtonInput[i].GetButton()) {
                m_lastPressedButtonInput = i;
                return;
            }
        }
    }

    public bool WasKeyboardUsedLast {
        get { return GameSettings.Instance.CurrentControlScheme > ControlScheme.Controller; }
    }

    private bool KeyboardUsedLast(IButtonInput iButtonInput) {
        if (iButtonInput is KeyCodeButtonInput) {
            return true;
        }

        AxisButtonInput axisButtonInput = iButtonInput as AxisButtonInput;
        if (axisButtonInput != null) {
            return KeyboardUsedLast(axisButtonInput.GetAxisInput());
        }

        CompoundButtonInput compoundButtonInput = iButtonInput as CompoundButtonInput;
        if (compoundButtonInput != null) {
            return KeyboardUsedLast(compoundButtonInput.GetLastPressed());
        }

        return iButtonInput is ControllerButtonInput && false;
    }

    private bool KeyboardUsedLast(IAxisInput iAxisInput) {
        if (iAxisInput is ButtonAxisInput) {
            return KeyboardUsedLast((iAxisInput as ButtonAxisInput).GetButtonInput());
        }

        if (iAxisInput is CompoundAxisInput) {
            return KeyboardUsedLast((iAxisInput as CompoundAxisInput).GetLastPressed());
        }

        return iAxisInput is ControllerAxisInput && false;
    }

    private bool IsAnyStartPressed() {
        return XboxControllerInput.GetButton(XboxControllerInput.Button.Start) || XboxControllerInput.GetButton(XboxControllerInput.Button.ButtonA) || XboxControllerInput.GetButton(XboxControllerInput.Button.ButtonB) || XboxControllerInput.GetButton(XboxControllerInput.Button.ButtonX) || XboxControllerInput.GetButton(XboxControllerInput.Button.ButtonY) || MoonInput.GetKey(KeyCode.Space) || MoonInput.GetKey(KeyCode.X) || MoonInput.GetKey(KeyCode.Mouse0) || MoonInput.GetKey(KeyCode.Return) || MoonInput.GetKey(KeyCode.Escape) || MoonInput.anyKey;
    }

    public void AddControllerButtonsToButtonInput(PlayerInputRebinding.ControllerButton[] buttons, CompoundButtonInput buttonInput) {
        for (int i = 0; i < buttons.Length; i++) {
            buttonInput.Add(ControllerButtonToButtonInput(buttons[i]));
        }
    }

    public IButtonInput ControllerButtonToButtonInput(PlayerInputRebinding.ControllerButton button) {
        switch (button) {
            case PlayerInputRebinding.ControllerButton.A:
                return new ControllerButtonInput(XboxControllerInput.Button.ButtonA);
            case PlayerInputRebinding.ControllerButton.B:
                return new ControllerButtonInput(XboxControllerInput.Button.ButtonB);
            case PlayerInputRebinding.ControllerButton.X:
                return new ControllerButtonInput(XboxControllerInput.Button.ButtonX);
            case PlayerInputRebinding.ControllerButton.Y:
                return new ControllerButtonInput(XboxControllerInput.Button.ButtonY);
            case PlayerInputRebinding.ControllerButton.LT:
                return new ControllerButtonInput(XboxControllerInput.Button.LeftTrigger);
            case PlayerInputRebinding.ControllerButton.RT:
                return new ControllerButtonInput(XboxControllerInput.Button.RightTrigger);
            case PlayerInputRebinding.ControllerButton.LB:
                return new ControllerButtonInput(XboxControllerInput.Button.LeftShoulder);
            case PlayerInputRebinding.ControllerButton.RB:
                return new ControllerButtonInput(XboxControllerInput.Button.RightShoulder);
            case PlayerInputRebinding.ControllerButton.LS:
                return new ControllerButtonInput(XboxControllerInput.Button.LeftStick);
            case PlayerInputRebinding.ControllerButton.RS:
                return new ControllerButtonInput(XboxControllerInput.Button.RightStick);
            case PlayerInputRebinding.ControllerButton.LUp:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.LeftStickY), AxisButtonInput.AxisMode.GreaterThan, 0.5f);
            case PlayerInputRebinding.ControllerButton.LDown:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.LeftStickY), AxisButtonInput.AxisMode.LessThan, -0.5f);
            case PlayerInputRebinding.ControllerButton.LLeft:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.LeftStickX), AxisButtonInput.AxisMode.LessThan, -0.5f);
            case PlayerInputRebinding.ControllerButton.LRight:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.LeftStickX), AxisButtonInput.AxisMode.GreaterThan, 0.5f);
            case PlayerInputRebinding.ControllerButton.DUp:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.DpadY), AxisButtonInput.AxisMode.GreaterThan, 0.5f);
            case PlayerInputRebinding.ControllerButton.DDown:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.DpadY), AxisButtonInput.AxisMode.LessThan, -0.5f);
            case PlayerInputRebinding.ControllerButton.DLeft:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.DpadX), AxisButtonInput.AxisMode.LessThan, -0.5f);
            case PlayerInputRebinding.ControllerButton.DRight:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.DpadX), AxisButtonInput.AxisMode.GreaterThan, 0.5f);
            case PlayerInputRebinding.ControllerButton.RUp:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.RightStickY), AxisButtonInput.AxisMode.GreaterThan, 0.5f);
            case PlayerInputRebinding.ControllerButton.RDown:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.RightStickY), AxisButtonInput.AxisMode.LessThan, -0.5f);
            case PlayerInputRebinding.ControllerButton.RLeft:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.RightStickX), AxisButtonInput.AxisMode.LessThan, -0.5f);
            case PlayerInputRebinding.ControllerButton.RRight:
                return new AxisButtonInput(new ControllerAxisInput(XboxControllerInput.Axis.RightStickX), AxisButtonInput.AxisMode.GreaterThan, 0.5f);
            case PlayerInputRebinding.ControllerButton.Back:
                return new ControllerButtonInput(XboxControllerInput.Button.Select);
            case PlayerInputRebinding.ControllerButton.Start:
                return new ControllerButtonInput(XboxControllerInput.Button.Start);
            default:
                return null;
        }
    }

    public static PlayerInput Instance;

    public bool Active = true;

    public CompoundAxisInput HorizontalAnalogLeft = new CompoundAxisInput();

    public CompoundAxisInput VerticalAnalogLeft = new CompoundAxisInput();

    public CompoundAxisInput HorizontalAnalogRight = new CompoundAxisInput();

    public CompoundAxisInput VerticalAnalogRight = new CompoundAxisInput();

    public CompoundAxisInput HorizontalDigiPad = new CompoundAxisInput();

    public CompoundAxisInput VerticalDigiPad = new CompoundAxisInput();

    public CompoundButtonInput Jump = new CompoundButtonInput();

    public CompoundButtonInput SpiritFlame = new CompoundButtonInput();

    public CompoundButtonInput SoulFlame = new CompoundButtonInput();

    public CompoundButtonInput Bash = new CompoundButtonInput();

    public CompoundButtonInput ChargeJump = new CompoundButtonInput();

    public CompoundButtonInput Glide = new CompoundButtonInput();

    public CompoundButtonInput Grab = new CompoundButtonInput();

    public CompoundButtonInput ZoomIn = new CompoundButtonInput();

    public CompoundButtonInput ZoomOut = new CompoundButtonInput();

    public CompoundButtonInput LeftShoulder = new CompoundButtonInput();

    public CompoundButtonInput RightShoulder = new CompoundButtonInput();

    public CompoundButtonInput Select = new CompoundButtonInput();

    public CompoundButtonInput Start = new CompoundButtonInput();

    public CompoundButtonInput LeftStick = new CompoundButtonInput();

    public CompoundButtonInput RightStick = new CompoundButtonInput();

    public CompoundButtonInput MenuDown = new CompoundButtonInput();

    public CompoundButtonInput MenuUp = new CompoundButtonInput();

    public CompoundButtonInput MenuLeft = new CompoundButtonInput();

    public CompoundButtonInput MenuRight = new CompoundButtonInput();

    public CompoundButtonInput MenuPageLeft = new CompoundButtonInput();

    public CompoundButtonInput MenuPageRight = new CompoundButtonInput();

    public CompoundButtonInput ActionButtonA = new CompoundButtonInput();

    public CompoundButtonInput Cancel = new CompoundButtonInput();

    public CompoundButtonInput Copy = new CompoundButtonInput();

    public CompoundButtonInput Delete = new CompoundButtonInput();

    public CompoundButtonInput Focus = new CompoundButtonInput();

    public CompoundButtonInput Filter = new CompoundButtonInput();

    public CompoundButtonInput Legend = new CompoundButtonInput();

    public IButtonInput LeftClick;

    public IButtonInput RightClick;

    public List<IButtonInput> m_allButtonInput;

    public List<Input.InputButtonProcessor> m_allButtonProcessor;

    public List<IAxisInput> m_allAxisInput;

    private int m_lastPressedButtonInput;

    private int m_lastPressedAxisInput;

    public CompoundButtonInput Stomp = new CompoundButtonInput();
}

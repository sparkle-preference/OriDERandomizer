using System.Collections.Generic;
using SmartInput;
using UnityEngine;

public static class ButtonIconUtility {
    private const string IconAnalogueUp = "<icon>a</>";

    private const string IconAnalogueDown = "<icon>b</>";

    private const string IconAnalogueLeft = "<icon>c</>";

    private const string IconAnalogueRight = "<icon>d</>";

    private const string IconAnalogueStick = "<icon>J</>";

    private const string IconButtonA = "<icon>e</>";

    private const string IconButtonB = "<icon>f</>";

    private const string IconButtonMenu = "<icon>g</>";

    private const string IconButtonX = "<icon>h</>";

    private const string IconButtonY = "<icon>i</>";

    private const string IconRightTrigger = "<icon>k</>";

    private const string IconButtonSelect = "<icon>l</>";

    private const string IconLeftTrigger = "<icon>m</>";

    private const string IconLeftShoulder = "<icon>R</>";

    private const string IconRightShoulder = "<icon>S</>";

    private const string IconKeyboardA = "<icon>n</>";

    private const string IconKeyboardB = "<icon>o</>";

    private const string IconKeyboardC = "<icon>p</>";

    private const string IconKeyboardD = "<icon>q</>";

    private const string IconKeyboardE = "<icon>H</>";

    private const string IconKeyboardF = "<icon>N</>";

    private const string IconKeyboardK = "<icon>O</>";

    private const string IconKeyboardL = "<icon>P</>";

    private const string IconKeyboardQ = "<icon>I</>";

    private const string IconKeyboardR = "<icon>T</>";

    private const string IconKeyboardS = "<icon>u</>";

    private const string IconKeyboardW = "<icon>w</>";

    private const string IconKeyboardX = "<icon>x</>";

    private const string IconKeyboardV = "<icon>A</>";

    private const string IconKeyboardZ = "<icon>B</>";

    private const string IconKeyboardUp = "<icon>v</>";

    private const string IconKeyboardDown = "<icon>r</>";

    private const string IconKeyboardLeft = "<icon>s</>";

    private const string IconKeyboardRight = "<icon>t</>";

    private const string IconKeyboardArrows = "<icon>Q</>";

    private const string IconKeyboardNavigation = "<icon>K</>";

    private const string IconKeyboardEsc = "<icon>y</>";

    private const string IconKeyboardTab = "<icon>z</>";

    private const string IconKeyboardSpace = "<icon>C</>";

    private const string IconKeyboardEnter = "<icon>D</>";

    private const string IconKeyboardControl = "<icon>G</>";

    private const string IconKeyboardShift = "<icon>L</>";

    private const string IconKeyboardDelete = "<icon>M</>";

    private const string IconMouseLeft = "<icon>E</>";

    private const string IconMouseRight = "<icon>F</>";

    private static Dictionary<KeyCode, string> m_keycodeToIconString = new Dictionary<KeyCode, string> {
        { KeyCode.A, "<icon>n</>" },
        { KeyCode.B, "<icon>o</>" },
        { KeyCode.C, "<icon>p</>" },
        { KeyCode.D, "<icon>q</>" },
        { KeyCode.DownArrow, "<icon>r</>" },
        { KeyCode.LeftArrow, "<icon>s</>" },
        { KeyCode.RightArrow, "<icon>t</>" },
        { KeyCode.S, "<icon>u</>" },
        { KeyCode.UpArrow, "<icon>v</>" },
        { KeyCode.W, "<icon>w</>" },
        { KeyCode.X, "<icon>x</>" },
        { KeyCode.Escape, "<icon>y</>" },
        { KeyCode.Tab, "<icon>z</>" },
        { KeyCode.V, "<icon>A</>" },
        { KeyCode.Z, "<icon>B</>" },
        { KeyCode.Space, "<icon>C</>" },
        { KeyCode.Return, "<icon>D</>" },
        { KeyCode.Mouse0, "<icon>E</>" },
        { KeyCode.Mouse1, "<icon>F</>" },
        { KeyCode.LeftControl, "<icon>G</>" },
        { KeyCode.RightControl, "<icon>G</>" },
        { KeyCode.E, "<icon>H</>" },
        { KeyCode.Q, "<icon>I</>" },
        { KeyCode.Alpha7, "<icon>Q</>" },
        { KeyCode.Alpha8, "<icon>Q</>" },
        { KeyCode.LeftShift, "<icon>L</>" },
        { KeyCode.RightShift, "<icon>L</>" },
        { KeyCode.Delete, "<icon>M</>" },
        { KeyCode.K, "<icon>O</>" },
        { KeyCode.L, "<icon>P</>" },
        { KeyCode.F, "<icon>N</>" },
        { KeyCode.R, "<icon>T</>" }
    };

    private static Dictionary<XboxControllerInput.Button, string> m_controllerButtonToIconString = new Dictionary<XboxControllerInput.Button, string> {
        { XboxControllerInput.Button.ButtonA, "<icon>e</>" },
        { XboxControllerInput.Button.ButtonB, "<icon>f</>" },
        { XboxControllerInput.Button.Start, "<icon>g</>" },
        { XboxControllerInput.Button.ButtonX, "<icon>h</>" },
        { XboxControllerInput.Button.ButtonY, "<icon>i</>" },
        { XboxControllerInput.Button.RightTrigger, "<icon>k</>" },
        { XboxControllerInput.Button.Select, "<icon>l</>" },
        { XboxControllerInput.Button.LeftTrigger, "<icon>m</>" },
        { XboxControllerInput.Button.LeftStick, "<icon>J</>" },
        { XboxControllerInput.Button.RightShoulder, "<icon>S</>" },
        { XboxControllerInput.Button.LeftShoulder, "<icon>R</>" }
    };

    private static Dictionary<XboxOneController.Button, string> m_xboxOneControllerButtonToIconString = new Dictionary<XboxOneController.Button, string> {
        { XboxOneController.Button.GamepadButtonA, "<icon>e</>" },
        { XboxOneController.Button.GamepadButtonB, "<icon>f</>" },
        { XboxOneController.Button.GamepadButtonMenu, "<icon>g</>" },
        { XboxOneController.Button.GamepadButtonX, "<icon>h</>" },
        { XboxOneController.Button.GamepadButtonY, "<icon>i</>" },
        { XboxOneController.Button.GamepadButtonView, "<icon>l</>" },
        { XboxOneController.Button.GamepadButtonLeftThumbstick, "<icon>J</>" },
        { XboxOneController.Button.GamepadButtonRightShoulder, "<icon>S</>" },
        { XboxOneController.Button.GamepadButtonLeftShoulder, "<icon>R</>" }
    };

    private static Dictionary<XboxOneController.Axis, string> m_xboxOneControllerAxisToIconString = new Dictionary<XboxOneController.Axis, string> {
        { XboxOneController.Axis.Gamepad1LeftTrigger, "<icon>m</>" }, { XboxOneController.Axis.Gamepad1RightTrigger, "<icon>k</>" }
    };

    // The same answer the game gives its own messages, for text of the randomizer's own.
    public static string IconFor(KeyCode keyCode) {
        return KeyCodeToString(keyCode);
    }

    private static string KeyCodeToString(KeyCode keyCode) {
        if (keyCode == KeyCode.Alpha7 && GameSettings.Instance.KeyboardScheme == ControlScheme.Keyboard) {
            return "<icon>K</>";
        }
        if (m_keycodeToIconString.ContainsKey(keyCode)) {
            return m_keycodeToIconString[keyCode];
        }

        // A cap borrowed for a key the game shipped without one. Neither answer is remembered
        // until the caps are in: the table below is a cache of misses as well as hits, and a
        // name cached before then is a name for the rest of the session.
        var borrowed = RandomizerKeyIcons.Glyph(keyCode);
        if (borrowed != null) {
            return borrowed;
        }

        string text = keyCode.ToString();
        if (RandomizerKeyIcons.Ready) {
            m_keycodeToIconString[keyCode] = text;
        }

        return text;
    }

    private static string ControllerButtonToString(XboxControllerInput.Button button) {
        if (m_controllerButtonToIconString.ContainsKey(button)) {
            return m_controllerButtonToIconString[button];
        }
        string text = button.ToString();
        m_controllerButtonToIconString[button] = text;
        return text;
    }

    private static string XboxOneControllerButtonToString(XboxOneController.Button button) {
        if (m_xboxOneControllerButtonToIconString.ContainsKey(button)) {
            return m_xboxOneControllerButtonToIconString[button];
        }
        string text = button.ToString();
        m_xboxOneControllerButtonToIconString[button] = text;
        return text;
    }

    private static string XboxOneControllerAxisToString(XboxOneController.Axis axis) {
        if (m_xboxOneControllerAxisToIconString.ContainsKey(axis)) {
            return m_xboxOneControllerAxisToIconString[axis];
        }
        string text = axis.ToString();
        m_xboxOneControllerAxisToIconString[axis] = text;
        return text;
    }

    private static string GetButtonString(KeyCodeButtonInput keyCodeButtonInput) {
        return KeyCodeToString(keyCodeButtonInput.KeyCode);
    }

    private static string GetButtonString(XboxOneController.ButtonInput xboxOneButtonInput) {
        return XboxOneControllerButtonToString(xboxOneButtonInput.Button);
    }

    private static string GetAxisString(XboxOneController.AxisInput xboxOneAxisInput) {
        return XboxOneControllerAxisToString(xboxOneAxisInput.Axis);
    }

    private static string GetButtonString(ControllerButtonInput controllerButtonInput) {
        return ControllerButtonToString(controllerButtonInput.Button);
    }

    private static string GetButtonString(AxisButtonInput axisButtonInput) {
        XboxOneController.AxisInput axisInput = axisButtonInput.GetAxisInput() as XboxOneController.AxisInput;
        return axisInput.Axis switch {
            XboxOneController.Axis.LeftTrigger => ControllerButtonToString(XboxControllerInput.Button.LeftTrigger), 
            XboxOneController.Axis.RightTrigger => ControllerButtonToString(XboxControllerInput.Button.RightTrigger), 
            _ => string.Empty, 
        };
    }

    public static string GetAxisIcon(IAxisInput axisInput, bool positive) {
        bool wasKeyboardUsedLast = PlayerInput.Instance.WasKeyboardUsedLast;
        CompoundAxisInput compoundAxisInput = axisInput as CompoundAxisInput;
        if (compoundAxisInput.Axis != null) {
            for (int i = 0; i < compoundAxisInput.Axis.Length; i++) {
                IAxisInput axisInput2 = compoundAxisInput.Axis[i];
                if (wasKeyboardUsedLast) {
                    if (axisInput2 is ButtonAxisInput buttonAxisInput && buttonAxisInput.Positive == positive) {
                        IButtonInput buttonInput = buttonAxisInput.GetButtonInput();
                        if (buttonInput is KeyCodeButtonInput keyCodeButtonInput) {
                            return GetButtonString(keyCodeButtonInput);
                        }
                    }
                }
                else if (XboxOne.ControllerReady) {
                    if (axisInput2 is XboxOneController.AxisInput axisInput3) {
                        if (axisInput3.Axis == XboxOneController.Axis.DpadX || axisInput3.Axis == XboxOneController.Axis.LeftStickX || axisInput3.Axis == XboxOneController.Axis.RightStickX) {
                            return (!positive) ? "<icon>c</>" : "<icon>d</>";
                        }
                        if (axisInput3.Axis == XboxOneController.Axis.DpadY || axisInput3.Axis == XboxOneController.Axis.LeftStickY || axisInput3.Axis == XboxOneController.Axis.RightStickY) {
                            return (!positive) ? "<icon>b</>" : "<icon>a</>";
                        }
                    }
                }
                else if (axisInput2 is ControllerAxisInput controllerAxisInput) {
                    if (controllerAxisInput.Axis == XboxControllerInput.Axis.DpadX || controllerAxisInput.Axis == XboxControllerInput.Axis.LeftStickX || controllerAxisInput.Axis == XboxControllerInput.Axis.RightStickX) {
                        return (!positive) ? "<icon>c</>" : "<icon>d</>";
                    }
                    if (controllerAxisInput.Axis == XboxControllerInput.Axis.DpadY || controllerAxisInput.Axis == XboxControllerInput.Axis.LeftStickY || controllerAxisInput.Axis == XboxControllerInput.Axis.RightStickY) {
                        return (!positive) ? "<icon>b</>" : "<icon>a</>";
                    }
                }
            }
        }
        return string.Empty;
    }

    public static string GetButtonString(CompoundButtonInput compoundButtonInput) {
        bool wasKeyboardUsedLast = PlayerInput.Instance.WasKeyboardUsedLast;
        if (compoundButtonInput.Buttons != null) {
            for (int i = 0; i < compoundButtonInput.Buttons.Length; i++) {
                IButtonInput buttonInput = compoundButtonInput.Buttons[i];
                if (wasKeyboardUsedLast) {
                    if (buttonInput is KeyCodeButtonInput keyCodeButtonInput) {
                        return GetButtonString(keyCodeButtonInput);
                    }
                }
                else if (XboxOne.ControllerReady) {
                    if (buttonInput is XboxOneController.ButtonInput xboxOneButtonInput) {
                        return GetButtonString(xboxOneButtonInput);
                    }
                    if (buttonInput is XboxOneController.AxisInput xboxOneAxisInput) {
                        return GetAxisString(xboxOneAxisInput);
                    }
                    if (buttonInput is AxisButtonInput axisButtonInput) {
                        return GetButtonString(axisButtonInput);
                    }
                }
                else if (buttonInput is ControllerButtonInput controllerButtonInput) {
                    return GetButtonString(controllerButtonInput);
                }
            }
        }
        return string.Empty;
    }
}

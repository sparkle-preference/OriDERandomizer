using System;
using System.Collections.Generic;
using System.IO;
using SmartInput;
using UnityEngine;

public class PlayerInputRebinding {
    static PlayerInputRebinding() {
        controllerButtonRemappings = new[] {
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
        };
        hasReadControllerRemappingsFile = false;
        intToButton = new[] {
            XboxControllerInput.Button.ButtonA,
            XboxControllerInput.Button.ButtonB,
            XboxControllerInput.Button.ButtonX,
            XboxControllerInput.Button.ButtonY,
            XboxControllerInput.Button.LeftShoulder,
            XboxControllerInput.Button.RightShoulder,
            XboxControllerInput.Button.Select,
            XboxControllerInput.Button.Start,
            XboxControllerInput.Button.LeftStick,
            XboxControllerInput.Button.RightStick,
            XboxControllerInput.Button.Button10,
            XboxControllerInput.Button.Button11,
            XboxControllerInput.Button.LeftTrigger,
            XboxControllerInput.Button.RightTrigger,
        };
    }

    public static KeyBindingSettings KeyRebindings {
        get {
            if (keyRebindings == null) {
                if (!File.Exists(KeyRebindingFile)) {
                    SetDefaultKeyBindingSettings();
                    WriteKeyRebindSettings();
                } else {
                    GetKeyRebindSettingsFromFile();
                }
            }

            return keyRebindings;
        }
    }

    private static string KeyRebindingFile => Path.Combine(OutputFolder.PlayerDataFolderPath, keyRebindingFileName);

    public static void GetKeyRebindSettingsFromFile() {
        try {
            using (var streamReader = new StreamReader(new FileStream(KeyRebindingFile, FileMode.Open))) {
                streamReader.ReadLine();
                streamReader.ReadLine();
                streamReader.ReadLine();
                streamReader.ReadLine();
                keyRebindings = new KeyBindingSettings {
                    IsRebinding = true,
                    HorizontalDigiPadLeft = StringToKeyBinding(streamReader.ReadLine()),
                    HorizontalDigiPadRight = StringToKeyBinding(streamReader.ReadLine()),
                    VerticalDigiPadDown = StringToKeyBinding(streamReader.ReadLine()),
                    VerticalDigiPadUp = StringToKeyBinding(streamReader.ReadLine()),
                    MenuLeft = StringToKeyBinding(streamReader.ReadLine()),
                    MenuRight = StringToKeyBinding(streamReader.ReadLine()),
                    MenuDown = StringToKeyBinding(streamReader.ReadLine()),
                    MenuUp = StringToKeyBinding(streamReader.ReadLine()),
                    MenuPageLeft = StringToKeyBinding(streamReader.ReadLine()),
                    MenuPageRight = StringToKeyBinding(streamReader.ReadLine()),
                    ActionButtonA = StringToKeyBinding(streamReader.ReadLine()),
                    SoulFlame = StringToKeyBinding(streamReader.ReadLine()),
                    Jump = StringToKeyBinding(streamReader.ReadLine()),
                    Grab = StringToKeyBinding(streamReader.ReadLine()),
                    SpiritFlame = StringToKeyBinding(streamReader.ReadLine()),
                    Bash = StringToKeyBinding(streamReader.ReadLine()),
                    Glide = StringToKeyBinding(streamReader.ReadLine()),
                    ChargeJump = StringToKeyBinding(streamReader.ReadLine()),
                    Select = StringToKeyBinding(streamReader.ReadLine()),
                    Start = StringToKeyBinding(streamReader.ReadLine()),
                    Cancel = StringToKeyBinding(streamReader.ReadLine()),
                    LeftShoulder = StringToKeyBinding(streamReader.ReadLine()),
                    RightShoulder = StringToKeyBinding(streamReader.ReadLine()),
                    LeftStick = StringToKeyBinding(streamReader.ReadLine()),
                    RightStick = StringToKeyBinding(streamReader.ReadLine()),
                    ZoomIn = StringToKeyBinding(streamReader.ReadLine()),
                    ZoomOut = StringToKeyBinding(streamReader.ReadLine()),
                    Copy = StringToKeyBinding(streamReader.ReadLine()),
                    Delete = StringToKeyBinding(streamReader.ReadLine()),
                    Focus = StringToKeyBinding(streamReader.ReadLine()),
                    Filter = StringToKeyBinding(streamReader.ReadLine()),
                    Legend = StringToKeyBinding(streamReader.ReadLine()),
                };
                try {
                    keyRebindings.Stomp = StringToKeyBinding(streamReader.ReadLine());
                } catch {
                    keyRebindings.Stomp = new[] {
                        KeyCode.S,
                    };
                }
            }
        } catch (Exception) {
            SetDefaultKeyBindingSettings();
        }
    }

    private static KeyCode[] StringToKeyBinding(string s) {
        s = s.Split(
            new[] {
                ": ",
            },
            StringSplitOptions.None
        )[1];
        var array = s.Split(
            new[] {
                ", ",
            },
            StringSplitOptions.None
        );
        var list = new List<KeyCode>();
        foreach (var value in array) {
            list.Add((KeyCode)(int)Enum.Parse(typeof(KeyCode), value));
        }

        return list.ToArray();
    }

    public static void WriteKeyRebindSettings() {
        using (var streamWriter = new StreamWriter(new FileStream(KeyRebindingFile, FileMode.OpenOrCreate))) {
            var keyRebindings = PlayerInputRebinding.keyRebindings;
            streamWriter.WriteLine("Keyboard Rebindings");
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Activate Key Rebinding: " + keyRebindings.IsRebinding);
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Movement Left: " + KeyBindingToString(keyRebindings.HorizontalDigiPadLeft));
            streamWriter.WriteLine("Movement Right: " + KeyBindingToString(keyRebindings.HorizontalDigiPadRight));
            streamWriter.WriteLine("Movement Down: " + KeyBindingToString(keyRebindings.VerticalDigiPadDown));
            streamWriter.WriteLine("Movement Up: " + KeyBindingToString(keyRebindings.VerticalDigiPadUp));
            streamWriter.WriteLine("Menu Left: " + KeyBindingToString(keyRebindings.MenuLeft));
            streamWriter.WriteLine("Menu Right: " + KeyBindingToString(keyRebindings.MenuRight));
            streamWriter.WriteLine("Menu Down: " + KeyBindingToString(keyRebindings.MenuDown));
            streamWriter.WriteLine("Menu Up: " + KeyBindingToString(keyRebindings.MenuUp));
            streamWriter.WriteLine("Menu Previous: " + KeyBindingToString(keyRebindings.MenuPageLeft));
            streamWriter.WriteLine("Menu Next: " + KeyBindingToString(keyRebindings.MenuPageRight));
            streamWriter.WriteLine("Proceed: " + KeyBindingToString(keyRebindings.ActionButtonA));
            streamWriter.WriteLine("Soul Link: " + KeyBindingToString(keyRebindings.SoulFlame));
            streamWriter.WriteLine("Jump: " + KeyBindingToString(keyRebindings.Jump));
            streamWriter.WriteLine("Grab: " + KeyBindingToString(keyRebindings.Grab));
            streamWriter.WriteLine("Spirit Flame: " + KeyBindingToString(keyRebindings.SpiritFlame));
            streamWriter.WriteLine("Bash: " + KeyBindingToString(keyRebindings.Bash));
            streamWriter.WriteLine("Glide: " + KeyBindingToString(keyRebindings.Glide));
            streamWriter.WriteLine("Charge Jump: " + KeyBindingToString(keyRebindings.ChargeJump));
            streamWriter.WriteLine("Select: " + KeyBindingToString(keyRebindings.Select));
            streamWriter.WriteLine("Start: " + KeyBindingToString(keyRebindings.Start));
            streamWriter.WriteLine("Cancel: " + KeyBindingToString(keyRebindings.Cancel));
            streamWriter.WriteLine("Grenade: " + KeyBindingToString(keyRebindings.LeftShoulder));
            streamWriter.WriteLine("Dash: " + KeyBindingToString(keyRebindings.RightShoulder));
            streamWriter.WriteLine("Left Stick: " + KeyBindingToString(keyRebindings.LeftStick));
            streamWriter.WriteLine("Debug Menu (shhh): " + KeyBindingToString(keyRebindings.RightStick));
            streamWriter.WriteLine("Zoom In World Map: " + KeyBindingToString(keyRebindings.ZoomIn));
            streamWriter.WriteLine("Zoom Out World Map: " + KeyBindingToString(keyRebindings.ZoomOut));
            streamWriter.WriteLine("Copy: " + KeyBindingToString(keyRebindings.Copy));
            streamWriter.WriteLine("Delete: " + KeyBindingToString(keyRebindings.Delete));
            streamWriter.WriteLine("Focus: " + KeyBindingToString(keyRebindings.Focus));
            streamWriter.WriteLine("Filter: " + KeyBindingToString(keyRebindings.Filter));
            streamWriter.WriteLine("Legend: " + KeyBindingToString(keyRebindings.Legend));
            streamWriter.WriteLine("Stomp: " + KeyBindingToString(keyRebindings.Stomp));
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Usage:");
            streamWriter.WriteLine("- There is no guarantee of the game still being playable after key rebinding. Please use with caution and delete this file in case of breakage");
            streamWriter.WriteLine("- Spelling and syntactical errors will result in the key rebindings not registering properly, and the game will get set to default");
            streamWriter.WriteLine("- Deleting this file will result in this file being recreated by the game, containing the default settings");
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Don't forget to restart the game after editing this file!");
            streamWriter.WriteLine("Don't forget to close this file before restarting the game!");
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Mouse0 is left mouse button");
            streamWriter.WriteLine("Mouse1 is right mouse button");
            streamWriter.WriteLine("AlphaX is the number X on the keyboard (not num pad, the stuff above your keys)");
        }
    }

    private static string KeyBindingToString(KeyCode[] codes) {
        var text = string.Empty;
        var flag = true;
        foreach (var keyCode in codes) {
            text += !flag ? ", " : string.Empty;
            text += keyCode;
            flag = false;
        }

        return text;
    }

    public static void SetDefaultKeyBindingSettings() {
        keyRebindings = DefaultKeyBindingSettings();
    }

    private static KeyBindingSettings DefaultKeyBindingSettings() {
        var flag = GameSettings.Instance.CurrentControlScheme == ControlScheme.KeyboardAndMouse;
        var flag2 = GameSettings.Instance.KeyboardLayout == KeyboardLayout.QWERTY;
        var keyBindingSettings = new KeyBindingSettings();
        keyBindingSettings.IsRebinding = false;
        keyBindingSettings.HorizontalDigiPadLeft = new[] {
            !flag2 ? KeyCode.Q : KeyCode.A,
            KeyCode.LeftArrow,
        };
        keyBindingSettings.HorizontalDigiPadRight = new[] {
            KeyCode.D,
            KeyCode.RightArrow,
        };
        keyBindingSettings.VerticalDigiPadDown = new[] {
            KeyCode.S,
            KeyCode.DownArrow,
        };
        keyBindingSettings.VerticalDigiPadUp = new[] {
            !flag2 ? KeyCode.Z : KeyCode.W,
            KeyCode.UpArrow,
        };
        keyBindingSettings.MenuLeft = new[] {
            !flag2 ? KeyCode.Q : KeyCode.A,
            KeyCode.LeftArrow,
        };
        keyBindingSettings.MenuRight = new[] {
            KeyCode.D,
            KeyCode.RightArrow,
        };
        keyBindingSettings.MenuDown = new[] {
            KeyCode.S,
            KeyCode.DownArrow,
        };
        keyBindingSettings.MenuUp = new[] {
            !flag2 ? KeyCode.Z : KeyCode.W,
            KeyCode.UpArrow,
        };
        keyBindingSettings.MenuPageLeft = new[] {
            KeyCode.K,
            KeyCode.PageUp,
        };
        keyBindingSettings.MenuPageRight = new[] {
            KeyCode.L,
            KeyCode.PageDown,
        };
        keyBindingSettings.ActionButtonA = new[] {
            KeyCode.Space,
            KeyCode.Return,
        };
        keyBindingSettings.SoulFlame = new[] {
            KeyCode.E,
        };
        keyBindingSettings.Jump = new[] {
            KeyCode.Space,
            !flag2 ? KeyCode.W : KeyCode.Z,
            KeyCode.Y,
        };
        keyBindingSettings.Grab = new[] {
            KeyCode.LeftShift,
            KeyCode.RightShift,
        };
        var keyBindingSettings2 = keyBindingSettings;
        KeyCode[] spiritFlame;
        if (flag) {
            var array = new KeyCode[2];
            array[0] = KeyCode.Mouse0;
            spiritFlame = array;
            array[1] = KeyCode.X;
        } else {
            var array2 = new KeyCode[2];
            array2[0] = KeyCode.X;
            spiritFlame = array2;
            array2[1] = KeyCode.Mouse0;
        }

        keyBindingSettings2.SpiritFlame = spiritFlame;
        var keyBindingSettings3 = keyBindingSettings;
        KeyCode[] bash;
        if (flag) {
            var array3 = new KeyCode[2];
            array3[0] = KeyCode.Mouse1;
            bash = array3;
            array3[1] = KeyCode.C;
        } else {
            var array4 = new KeyCode[2];
            array4[0] = KeyCode.C;
            bash = array4;
            array4[1] = KeyCode.Mouse1;
        }

        keyBindingSettings3.Bash = bash;
        keyBindingSettings.Glide = new[] {
            KeyCode.LeftShift,
            KeyCode.RightShift,
        };
        var keyBindingSettings4 = keyBindingSettings;
        KeyCode[] chargeJump;
        if (flag) {
            var array5 = new KeyCode[2];
            array5[0] = !flag2 ? KeyCode.Z : KeyCode.W;
            chargeJump = array5;
            array5[1] = KeyCode.UpArrow;
        } else {
            var array6 = new KeyCode[2];
            array6[0] = KeyCode.UpArrow;
            chargeJump = array6;
            array6[1] = !flag2 ? KeyCode.Z : KeyCode.W;
        }

        keyBindingSettings4.ChargeJump = chargeJump;
        keyBindingSettings.Select = new[] {
            KeyCode.Tab,
        };
        keyBindingSettings.Start = new[] {
            KeyCode.Escape,
        };
        keyBindingSettings.Cancel = new[] {
            KeyCode.Escape,
            KeyCode.Mouse1,
        };
        keyBindingSettings.LeftShoulder = new[] {
            KeyCode.R,
        };
        keyBindingSettings.RightShoulder = new[] {
            KeyCode.LeftControl,
            KeyCode.RightControl,
        };
        keyBindingSettings.LeftStick = new[] {
            KeyCode.Alpha7,
        };
        keyBindingSettings.RightStick = new[] {
            KeyCode.Alpha8,
        };
        keyBindingSettings.ZoomIn = new[] {
            KeyCode.RightShift,
            KeyCode.LeftShift,
        };
        keyBindingSettings.ZoomOut = new[] {
            KeyCode.RightControl,
            KeyCode.LeftControl,
        };
        keyBindingSettings.Copy = new[] {
            KeyCode.C,
        };
        keyBindingSettings.Delete = new[] {
            KeyCode.Delete,
        };
        keyBindingSettings.Focus = new[] {
            KeyCode.F,
        };
        keyBindingSettings.Filter = new[] {
            KeyCode.F,
        };
        keyBindingSettings.Legend = new[] {
            KeyCode.L,
        };
        keyBindingSettings.Stomp = new[] {
            KeyCode.S,
        };
        return keyBindingSettings;
    }

    private static string ControllerRemappingFile => Path.Combine(OutputFolder.PlayerDataFolderPath, controllerRebindingFileName);

    public static XboxControllerInput.Button GetRemappedJoystickButton(XboxControllerInput.Button joystickButtonIndex) {
        return intToButton[controllerButtonRemappings[ButtonToInt(joystickButtonIndex)]];
    }

    public static void GetControllerButtonRemappingsFromFile() {
        try {
            using (var streamReader = new StreamReader(new FileStream(ControllerRemappingFile, FileMode.Open))) {
                SetDefaultControllerButtonRemappings();
                streamReader.ReadLine();
                streamReader.ReadLine();
                streamReader.ReadLine();
                var flag = bool.Parse(
                    streamReader.ReadLine().Split(
                        new[] {
                            ": ",
                        },
                        StringSplitOptions.None
                    )[1]
                );
                if (flag) {
                    streamReader.ReadLine();
                    controllerIsRemappingButtons = flag;
                    controllerButtonRemappings[0] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[1] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[2] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[3] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[4] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[5] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[6] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[7] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[8] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[9] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[12] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    controllerButtonRemappings[13] = int.Parse(
                        streamReader.ReadLine().Split(
                            new[] {
                                ": ",
                            },
                            StringSplitOptions.None
                        )[1]
                    ) - 1;
                    for (var i = 0; i < 10; i++) {
                        if (controllerButtonRemappings[i] < 0 || controllerButtonRemappings[i] > 11) {
                            SetDefaultControllerButtonRemappings();
                        }
                    }

                    for (var j = 12; j < 14; j++) {
                        if (controllerButtonRemappings[j] < 0 || controllerButtonRemappings[j] > 13) {
                            SetDefaultControllerButtonRemappings();
                        }
                    }
                }
            }
        } catch (Exception) {
            SetDefaultControllerButtonRemappings();
        }
    }

    public static void WriteControllerButtonRemappings() {
        using (var streamWriter = new StreamWriter(new FileStream(ControllerRemappingFile, FileMode.OpenOrCreate))) {
            streamWriter.WriteLine("Controller Button Remapping - remaps controller buttons to different DirectInput button IDs");
            streamWriter.WriteLine("Only for DirectInput controllers");
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Activate DirectInput Button Rebinding: " + controllerIsRemappingButtons);
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("A: " + (controllerButtonRemappings[0] + 1));
            streamWriter.WriteLine("B: " + (controllerButtonRemappings[1] + 1));
            streamWriter.WriteLine("X: " + (controllerButtonRemappings[2] + 1));
            streamWriter.WriteLine("Y: " + (controllerButtonRemappings[3] + 1));
            streamWriter.WriteLine("LShoulder: " + (controllerButtonRemappings[4] + 1));
            streamWriter.WriteLine("RShoulder: " + (controllerButtonRemappings[5] + 1));
            streamWriter.WriteLine("Select: " + (controllerButtonRemappings[6] + 1));
            streamWriter.WriteLine("Start: " + (controllerButtonRemappings[7] + 1));
            streamWriter.WriteLine("LStick: " + (controllerButtonRemappings[8] + 1));
            streamWriter.WriteLine("RStick: " + (controllerButtonRemappings[9] + 1));
            streamWriter.WriteLine("LTrigger: " + (controllerButtonRemappings[12] + 1));
            streamWriter.WriteLine("RTrigger: " + (controllerButtonRemappings[13] + 1));
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Usage:");
            streamWriter.WriteLine("- There is no guarantee of the game still being playable after button remapping. Please use with caution and delete this file in case of breakage");
            streamWriter.WriteLine("- Syntactical errors will result in the key rebindings not registering properly, and the button bindings will get set to default");
            streamWriter.WriteLine("- Only numbers ranging from 1-12 should be used (with the exception of LTrigger and RTrigger)");
            streamWriter.WriteLine("- Deleting this file will result in this file being recreated by the game, containing the default settings");
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Don't forget to restart the game after editing this file!");
            streamWriter.WriteLine("Don't forget to close this file before restarting the game!");
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("To determine the correct button remaps you need, you can look at:");
            streamWriter.WriteLine("    \"Game Controllers\"->your controller->\"properties\"; and bash buttons until you know your current button IDs.");
            streamWriter.WriteLine("    Alternatively, you could look up controller test software that does the same.");
            streamWriter.WriteLine("After you found your controller's button mapping, you can fill them in accordingly, in the above list.");
            streamWriter.WriteLine("Leave left and right trigger at 13 and 14 to keep them working as controller axes 9 and 10");
        }
    }

    public static void RefreshControllerButtonRemappings() {
        if (!hasReadControllerRemappingsFile) {
            if (!File.Exists(ControllerRemappingFile)) {
                SetDefaultControllerButtonRemappings();
                WriteControllerButtonRemappings();
            } else {
                GetControllerButtonRemappingsFromFile();
            }

            hasReadControllerRemappingsFile = true;
        }
    }

    public static void SetDefaultControllerButtonRemappings() {
        controllerButtonRemappings = new[] {
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
            14,
            15,
        };
    }

    private static int ButtonToInt(XboxControllerInput.Button button) {
        switch (button) {
            case XboxControllerInput.Button.ButtonA:
                return 0;
            case XboxControllerInput.Button.ButtonX:
                return 2;
            case XboxControllerInput.Button.ButtonY:
                return 3;
            case XboxControllerInput.Button.ButtonB:
                return 1;
            case XboxControllerInput.Button.LeftTrigger:
                return 12;
            case XboxControllerInput.Button.RightTrigger:
                return 13;
            case XboxControllerInput.Button.LeftShoulder:
                return 4;
            case XboxControllerInput.Button.RightShoulder:
                return 5;
            case XboxControllerInput.Button.LeftStick:
                return 8;
            case XboxControllerInput.Button.RightStick:
                return 9;
            case XboxControllerInput.Button.Select:
                return 6;
            case XboxControllerInput.Button.Start:
                return 7;
            case XboxControllerInput.Button.Button10:
                return 10;
            case XboxControllerInput.Button.Button11:
                return 11;
            default:
                return 0;
        }
    }

    public static void GetControllerRebindSettingsFromFile() {
        try {
            using (var streamReader = new StreamReader(new FileStream(ControllerRebindingFile, FileMode.Open))) {
                streamReader.ReadLine();
                streamReader.ReadLine();
                ControllerRebindings = new ControllerBindingSettings {
                    HorizontalDigiPadLeft = StringToControllerBinding(streamReader.ReadLine()),
                    HorizontalDigiPadRight = StringToControllerBinding(streamReader.ReadLine()),
                    VerticalDigiPadDown = StringToControllerBinding(streamReader.ReadLine()),
                    VerticalDigiPadUp = StringToControllerBinding(streamReader.ReadLine()),
                    MenuLeft = StringToControllerBinding(streamReader.ReadLine()),
                    MenuRight = StringToControllerBinding(streamReader.ReadLine()),
                    MenuDown = StringToControllerBinding(streamReader.ReadLine()),
                    MenuUp = StringToControllerBinding(streamReader.ReadLine()),
                    MenuPageLeft = StringToControllerBinding(streamReader.ReadLine()),
                    MenuPageRight = StringToControllerBinding(streamReader.ReadLine()),
                    ActionButtonA = StringToControllerBinding(streamReader.ReadLine()),
                    SoulFlame = StringToControllerBinding(streamReader.ReadLine()),
                    Jump = StringToControllerBinding(streamReader.ReadLine()),
                    Grab = StringToControllerBinding(streamReader.ReadLine()),
                    SpiritFlame = StringToControllerBinding(streamReader.ReadLine()),
                    Bash = StringToControllerBinding(streamReader.ReadLine()),
                    Glide = StringToControllerBinding(streamReader.ReadLine()),
                    ChargeJump = StringToControllerBinding(streamReader.ReadLine()),
                    Select = StringToControllerBinding(streamReader.ReadLine()),
                    Start = StringToControllerBinding(streamReader.ReadLine()),
                    Cancel = StringToControllerBinding(streamReader.ReadLine()),
                    LeftShoulder = StringToControllerBinding(streamReader.ReadLine()),
                    RightShoulder = StringToControllerBinding(streamReader.ReadLine()),
                    LeftStick = StringToControllerBinding(streamReader.ReadLine()),
                    RightStick = StringToControllerBinding(streamReader.ReadLine()),
                    ZoomIn = StringToControllerBinding(streamReader.ReadLine()),
                    ZoomOut = StringToControllerBinding(streamReader.ReadLine()),
                    Copy = StringToControllerBinding(streamReader.ReadLine()),
                    Delete = StringToControllerBinding(streamReader.ReadLine()),
                    Focus = StringToControllerBinding(streamReader.ReadLine()),
                    Filter = StringToControllerBinding(streamReader.ReadLine()),
                    Legend = StringToControllerBinding(streamReader.ReadLine()),
                    Stomp = StringToControllerBinding(streamReader.ReadLine()),
                };
            }
        } catch (Exception) {
            SetDefaultControllerBindingSettings();
        }
    }

    public static ControllerButton[] StringToControllerBinding(string s) {
        s = s.Split(
            new[] {
                ": ",
            },
            StringSplitOptions.None
        )[1];
        var array = s.Split(
            new[] {
                ", ",
            },
            StringSplitOptions.RemoveEmptyEntries
        );
        var list = new List<ControllerButton>();
        foreach (var value in array) {
            list.Add((ControllerButton)(int)Enum.Parse(typeof(ControllerButton), value));
        }

        return list.ToArray();
    }

    public static void WriteControllerRebindSettings() {
        using (var streamWriter = new StreamWriter(new FileStream(ControllerRebindingFile, FileMode.OpenOrCreate))) {
            var controllerRebindings = ControllerRebindings;
            streamWriter.WriteLine("Controller Rebindings");
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Movement Left: " + ControllerBindingToString(controllerRebindings.HorizontalDigiPadLeft));
            streamWriter.WriteLine("Movement Right: " + ControllerBindingToString(controllerRebindings.HorizontalDigiPadRight));
            streamWriter.WriteLine("Movement Down: " + ControllerBindingToString(controllerRebindings.VerticalDigiPadDown));
            streamWriter.WriteLine("Movement Up: " + ControllerBindingToString(controllerRebindings.VerticalDigiPadUp));
            streamWriter.WriteLine("Menu Left: " + ControllerBindingToString(controllerRebindings.MenuLeft));
            streamWriter.WriteLine("Menu Right: " + ControllerBindingToString(controllerRebindings.MenuRight));
            streamWriter.WriteLine("Menu Down: " + ControllerBindingToString(controllerRebindings.MenuDown));
            streamWriter.WriteLine("Menu Up: " + ControllerBindingToString(controllerRebindings.MenuUp));
            streamWriter.WriteLine("Menu Previous: " + ControllerBindingToString(controllerRebindings.MenuPageLeft));
            streamWriter.WriteLine("Menu Next: " + ControllerBindingToString(controllerRebindings.MenuPageRight));
            streamWriter.WriteLine("Proceed: " + ControllerBindingToString(controllerRebindings.ActionButtonA));
            streamWriter.WriteLine("Soul Link: " + ControllerBindingToString(controllerRebindings.SoulFlame));
            streamWriter.WriteLine("Jump: " + ControllerBindingToString(controllerRebindings.Jump));
            streamWriter.WriteLine("Grab: " + ControllerBindingToString(controllerRebindings.Grab));
            streamWriter.WriteLine("Spirit Flame: " + ControllerBindingToString(controllerRebindings.SpiritFlame));
            streamWriter.WriteLine("Bash: " + ControllerBindingToString(controllerRebindings.Bash));
            streamWriter.WriteLine("Glide: " + ControllerBindingToString(controllerRebindings.Glide));
            streamWriter.WriteLine("Charge Jump: " + ControllerBindingToString(controllerRebindings.ChargeJump));
            streamWriter.WriteLine("Map: " + ControllerBindingToString(controllerRebindings.Select));
            streamWriter.WriteLine("Start: " + ControllerBindingToString(controllerRebindings.Start));
            streamWriter.WriteLine("Cancel: " + ControllerBindingToString(controllerRebindings.Cancel));
            streamWriter.WriteLine("Grenade: " + ControllerBindingToString(controllerRebindings.LeftShoulder));
            streamWriter.WriteLine("Dash: " + ControllerBindingToString(controllerRebindings.RightShoulder));
            streamWriter.WriteLine("Left Stick: " + ControllerBindingToString(controllerRebindings.LeftStick));
            streamWriter.WriteLine("Debug Menu (shhh): " + ControllerBindingToString(controllerRebindings.RightStick));
            streamWriter.WriteLine("Zoom In World Map: " + ControllerBindingToString(controllerRebindings.ZoomIn));
            streamWriter.WriteLine("Zoom Out World Map: " + ControllerBindingToString(controllerRebindings.ZoomOut));
            streamWriter.WriteLine("Copy: " + ControllerBindingToString(controllerRebindings.Copy));
            streamWriter.WriteLine("Delete: " + ControllerBindingToString(controllerRebindings.Delete));
            streamWriter.WriteLine("Focus: " + ControllerBindingToString(controllerRebindings.Focus));
            streamWriter.WriteLine("Filter: " + ControllerBindingToString(controllerRebindings.Filter));
            streamWriter.WriteLine("Legend: " + ControllerBindingToString(controllerRebindings.Legend));
            streamWriter.WriteLine("Stomp: " + ControllerBindingToString(controllerRebindings.Stomp));
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Usage:");
            streamWriter.WriteLine("- There is no guarantee of the game still being playable after key rebinding. Please use with caution and delete this file in case of breakage");
            streamWriter.WriteLine("- Spelling and syntactical errors will result in the key rebindings not registering properly, and the game will get set to default");
            streamWriter.WriteLine("- Deleting this file will result in this file being recreated by the game, containing the default settings");
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Don't forget to restart the game after editing this file!");
            streamWriter.WriteLine("Don't forget to close this file before restarting the game!");
            streamWriter.WriteLine("--------");
            streamWriter.WriteLine("Movement is always on the left thumbstick");
            streamWriter.WriteLine("LLeft, LRight, LUp, LDown is for the left thumbstick");
            streamWriter.WriteLine("RLeft, RRight, RUp, RDown is for the right thumbstick");
            streamWriter.WriteLine("DLeft, DRight, DUp, DDown is for the D-Pad");
            streamWriter.WriteLine("Based on an xbox controller - i.e. A, B, X, Y, LT, RT, LB, RB, LS, RS correspond to Cross, Circle, Square, Triangle, L2, R2, L1, R1, L3, R3");
            streamWriter.WriteLine("Not guaranteed to work on controllers other than xbox");
        }
    }

    public static string ControllerBindingToString(ControllerButton[] codes) {
        var text = string.Empty;
        var flag = true;
        foreach (var controllerButton in codes) {
            text += !flag ? ", " : string.Empty;
            text += controllerButton;
            flag = false;
        }

        return text;
    }

    public static void SetDefaultControllerBindingSettings() {
        ControllerRebindings = DefaultControllerBindingSettings();
    }

    public static ControllerBindingSettings DefaultControllerBindingSettings() {
        return new ControllerBindingSettings {
            HorizontalDigiPadLeft = new[] {
                ControllerButton.DLeft,
            },
            HorizontalDigiPadRight = new[] {
                ControllerButton.DRight,
            },
            VerticalDigiPadUp = new[] {
                ControllerButton.DUp,
            },
            VerticalDigiPadDown = new[] {
                ControllerButton.DDown,
            },
            ActionButtonA = new ControllerButton[1],
            Bash = new[] {
                ControllerButton.Y,
            },
            Cancel = new[] {
                ControllerButton.B,
            },
            ChargeJump = new[] {
                ControllerButton.LT,
            },
            Copy = new[] {
                ControllerButton.X,
            },
            Delete = new[] {
                ControllerButton.Y,
            },
            Filter = new[] {
                ControllerButton.X,
            },
            Focus = new[] {
                ControllerButton.X,
            },
            Glide = new[] {
                ControllerButton.RT,
            },
            Grab = new[] {
                ControllerButton.RT,
            },
            Jump = new ControllerButton[1],
            LeftShoulder = new[] {
                ControllerButton.LB,
            },
            LeftStick = new[] {
                ControllerButton.LS,
            },
            Legend = new[] {
                ControllerButton.Y,
            },
            MenuDown = new[] {
                ControllerButton.LDown,
                ControllerButton.DDown,
            },
            MenuLeft = new[] {
                ControllerButton.LLeft,
                ControllerButton.DLeft,
            },
            MenuPageLeft = new[] {
                ControllerButton.LT,
            },
            MenuPageRight = new[] {
                ControllerButton.RT,
            },
            MenuRight = new[] {
                ControllerButton.LRight,
                ControllerButton.DRight,
            },
            MenuUp = new[] {
                ControllerButton.LUp,
                ControllerButton.DUp,
            },
            RightShoulder = new[] {
                ControllerButton.RB,
            },
            RightStick = new[] {
                ControllerButton.RS,
            },
            Select = new[] {
                ControllerButton.Back,
            },
            SoulFlame = new[] {
                ControllerButton.B,
            },
            SpiritFlame = new[] {
                ControllerButton.X,
            },
            Start = new[] {
                ControllerButton.Start,
            },
            Stomp = new[] {
                ControllerButton.LDown,
                ControllerButton.DDown,
            },
            ZoomIn = new[] {
                ControllerButton.RT,
            },
            ZoomOut = new[] {
                ControllerButton.LT,
            },
        };
    }

    public static ControllerBindingSettings ControllerRebindingSettings {
        get {
            if (ControllerRebindings == null) {
                if (!File.Exists(ControllerRebindingFile)) {
                    SetDefaultControllerBindingSettings();
                    WriteControllerRebindSettings();
                } else {
                    GetControllerRebindSettingsFromFile();
                }
            }

            return ControllerRebindings;
        }
    }

    public static string ControllerRebindingFile => Path.Combine(OutputFolder.PlayerDataFolderPath, ControllerInputRebindingsFileName);

    private static string keyRebindingFileName = "KeyRebindings.txt";

    private static string controllerRebindingFileName = "ControllerButtonRemaps.txt";

    private static KeyBindingSettings keyRebindings;

    private static int[] controllerButtonRemappings;

    private static bool controllerIsRemappingButtons;

    private static bool hasReadControllerRemappingsFile;

    private static XboxControllerInput.Button[] intToButton;

    public static string ControllerInputRebindingsFileName = "ControllerRebindings.txt";

    public static ControllerBindingSettings ControllerRebindings;

    public class KeyBindingSettings {
        public KeyBindingSettings() {
            Glide = new KeyCode[0];
            ChargeJump = new KeyCode[0];
            Select = new KeyCode[0];
            Start = new KeyCode[0];
            Cancel = new KeyCode[0];
            LeftShoulder = new KeyCode[0];
            RightShoulder = new KeyCode[0];
            LeftStick = new KeyCode[0];
            RightStick = new KeyCode[0];
            ZoomIn = new KeyCode[0];
            ZoomOut = new KeyCode[0];
            Copy = new KeyCode[0];
            Delete = new KeyCode[0];
            Focus = new KeyCode[0];
            Filter = new KeyCode[0];
            Legend = new KeyCode[0];
        }

        public bool IsRebinding;

        public KeyCode[] HorizontalDigiPadLeft = new KeyCode[0];

        public KeyCode[] HorizontalDigiPadRight = new KeyCode[0];

        public KeyCode[] VerticalDigiPadDown = new KeyCode[0];

        public KeyCode[] VerticalDigiPadUp = new KeyCode[0];

        public KeyCode[] MenuLeft = new KeyCode[0];

        public KeyCode[] MenuRight = new KeyCode[0];

        public KeyCode[] MenuDown = new KeyCode[0];

        public KeyCode[] MenuUp = new KeyCode[0];

        public KeyCode[] MenuPageLeft = new KeyCode[0];

        public KeyCode[] MenuPageRight = new KeyCode[0];

        public KeyCode[] ActionButtonA = new KeyCode[0];

        public KeyCode[] SoulFlame = new KeyCode[0];

        public KeyCode[] Jump = new KeyCode[0];

        public KeyCode[] Grab = new KeyCode[0];

        public KeyCode[] SpiritFlame = new KeyCode[0];

        public KeyCode[] Bash = new KeyCode[0];

        public KeyCode[] Glide;

        public KeyCode[] ChargeJump;

        public KeyCode[] Select;

        public KeyCode[] Start;

        public KeyCode[] Cancel;

        public KeyCode[] LeftShoulder;

        public KeyCode[] RightShoulder;

        public KeyCode[] LeftStick;

        public KeyCode[] RightStick;

        public KeyCode[] ZoomIn;

        public KeyCode[] ZoomOut;

        public KeyCode[] Copy;

        public KeyCode[] Delete;

        public KeyCode[] Focus;

        public KeyCode[] Filter;

        public KeyCode[] Legend;

        public KeyCode[] Stomp = new KeyCode[0];
    }

    public enum ControllerButton {
        A,
        B,
        X,
        Y,
        LT,
        RT,
        LB,
        RB,
        LS,
        RS,
        LUp,
        LDown,
        LLeft,
        LRight,
        DUp,
        DDown,
        DLeft,
        DRight,
        RUp,
        RDown,
        RLeft,
        RRight,
        Back,
        Start,
    }

    public class ControllerBindingSettings {
        public ControllerButton[] HorizontalDigiPadLeft = new ControllerButton[0];

        public ControllerButton[] HorizontalDigiPadRight = new ControllerButton[0];

        public ControllerButton[] VerticalDigiPadDown = new ControllerButton[0];

        public ControllerButton[] VerticalDigiPadUp = new ControllerButton[0];

        public ControllerButton[] MenuLeft = new ControllerButton[0];

        public ControllerButton[] MenuRight = new ControllerButton[0];

        public ControllerButton[] MenuDown = new ControllerButton[0];

        public ControllerButton[] MenuUp = new ControllerButton[0];

        public ControllerButton[] MenuPageLeft = new ControllerButton[0];

        public ControllerButton[] MenuPageRight = new ControllerButton[0];

        public ControllerButton[] ActionButtonA = new ControllerButton[0];

        public ControllerButton[] SoulFlame = new ControllerButton[0];

        public ControllerButton[] Jump = new ControllerButton[0];

        public ControllerButton[] Grab = new ControllerButton[0];

        public ControllerButton[] SpiritFlame = new ControllerButton[0];

        public ControllerButton[] Bash = new ControllerButton[0];

        public ControllerButton[] Glide = new ControllerButton[0];

        public ControllerButton[] ChargeJump = new ControllerButton[0];

        public ControllerButton[] Select = new ControllerButton[0];

        public ControllerButton[] Start = new ControllerButton[0];

        public ControllerButton[] Cancel = new ControllerButton[0];

        public ControllerButton[] LeftShoulder = new ControllerButton[0];

        public ControllerButton[] RightShoulder = new ControllerButton[0];

        public ControllerButton[] LeftStick = new ControllerButton[0];

        public ControllerButton[] RightStick = new ControllerButton[0];

        public ControllerButton[] ZoomIn = new ControllerButton[0];

        public ControllerButton[] ZoomOut = new ControllerButton[0];

        public ControllerButton[] Copy = new ControllerButton[0];

        public ControllerButton[] Delete = new ControllerButton[0];

        public ControllerButton[] Focus = new ControllerButton[0];

        public ControllerButton[] Filter = new ControllerButton[0];

        public ControllerButton[] Legend = new ControllerButton[0];

        public ControllerButton[] Stomp = new ControllerButton[0];
    }
}

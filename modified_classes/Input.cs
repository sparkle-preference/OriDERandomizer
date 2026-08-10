using Game;
using UnityEngine;

namespace Core {
    public class Input {
        static Input() {
            Filter = new InputButtonProcessor();
            Legend = new InputButtonProcessor();
            Buttons = new[] {
                Down,
                Up,
                Left,
                Right,
                Jump,
                SpiritFlame,
                Bash,
                SoulFlame,
                ChargeJump,
                Glide,
                Grab,
                LeftShoulder,
                RightShoulder,
                Start,
                AnyStart,
                Select,
                LeftStick,
                RightStick,
                MenuDown,
                MenuUp,
                MenuLeft,
                MenuRight,
                MenuPageLeft,
                MenuPageRight,
                ActionButtonA,
                ZoomIn,
                ZoomOut,
                Cancel,
                Copy,
                Delete,
                Focus,
                Filter,
                Legend,
                Stomp
            };
        }

        public static int NormalizedHorizontal {
            get {
                if (Horizontal < -0.4f) {
                    return -1;
                }

                if (Horizontal > 0.4f) {
                    return 1;
                }

                return 0;
            }
        }

        public static float NormalizedVertical {
            get {
                if (Vertical < -0.6f) {
                    return -1f;
                }

                if (Vertical > 0.6f) {
                    return 1f;
                }

                return 0f;
            }
        }

        public static Vector2 Axis => new Vector2(Horizontal, Vertical);

        public static Vector2 AnalogAxisLeft => new Vector2(HorizontalAnalogLeft, VerticalAnalogLeft);

        public static Vector2 AnalogAxisRight => new Vector2(HorizontalAnalogRight, VerticalAnalogRight);

        public static Vector2 DigiPadAxis => new Vector2(HorizontalDigiPad, VerticalDigiPad);

        public static Vector2 CursorPositionUI {
            get {
                var camera = UI.Cameras.System.GUICamera.Camera;
                var cursorPosition = CursorPosition;
                return camera.ViewportToWorldPoint(cursorPosition);
            }
        }

        public static bool OnAnyButtonPressed {
            get {
                for (var i = 0; i < Buttons.Length; i++) {
                    if (Buttons[i].OnPressed) {
                        return true;
                    }
                }

                return false;
            }
        }

        public static bool AnyButtonPressed {
            get {
                for (var i = 0; i < Buttons.Length; i++) {
                    if (Buttons[i].IsPressed) {
                        return true;
                    }
                }

                return false;
            }
        }

        public static bool AnyButtonReleased {
            get {
                for (var i = 0; i < Buttons.Length; i++) {
                    if (Buttons[i].Released) {
                        return true;
                    }
                }

                return false;
            }
        }

        public static bool OnAnyButtonReleased {
            get {
                for (var i = 0; i < Buttons.Length; i++) {
                    if (Buttons[i].OnReleased) {
                        return true;
                    }
                }

                return false;
            }
        }

        public static InputButtonProcessor GetButton(Button button) {
            switch (button) {
                case Button.ButtonA:
                    return Jump;
                case Button.ButtonX:
                    return SpiritFlame;
                case Button.ButtonY:
                    return Bash;
                case Button.ButtonB:
                    return SoulFlame;
                case Button.LeftTrigger:
                    return ChargeJump;
                case Button.RightTrigger:
                    return Glide;
                case Button.LeftShoulder:
                    return LeftShoulder;
                case Button.RightShoulder:
                    return RightShoulder;
                case Button.Left:
                    return Left;
                case Button.Right:
                    return Right;
                case Button.Up:
                    return Up;
                case Button.Down:
                    return Down;
                case Button.LeftStick:
                    return LeftStick;
                case Button.RightStick:
                    return RightStick;
            }

            return Unassigned;
        }

        public static float Horizontal;

        public static float Vertical;

        public static int HorizontalDigiPad;

        public static int VerticalDigiPad;

        public static float HorizontalAnalogLeft;

        public static float VerticalAnalogLeft;

        public static float HorizontalAnalogRight;

        public static float VerticalAnalogRight;

        public static InputButtonProcessor Down = new InputButtonProcessor();

        public static InputButtonProcessor Up = new InputButtonProcessor();

        public static InputButtonProcessor Left = new InputButtonProcessor();

        public static InputButtonProcessor Right = new InputButtonProcessor();

        public static InputButtonProcessor Jump = new InputButtonProcessor();

        public static InputButtonProcessor SpiritFlame = new InputButtonProcessor();

        public static InputButtonProcessor Bash = new InputButtonProcessor();

        public static InputButtonProcessor SoulFlame = new InputButtonProcessor();

        public static InputButtonProcessor ChargeJump = new InputButtonProcessor();

        public static InputButtonProcessor Glide = new InputButtonProcessor();

        public static InputButtonProcessor Grab = new InputButtonProcessor();

        public static InputButtonProcessor ZoomIn = new InputButtonProcessor();

        public static InputButtonProcessor ZoomOut = new InputButtonProcessor();

        public static InputButtonProcessor LeftShoulder = new InputButtonProcessor();

        public static InputButtonProcessor RightShoulder = new InputButtonProcessor();

        public static InputButtonProcessor Start = new InputButtonProcessor();

        public static InputButtonProcessor AnyStart = new InputButtonProcessor();

        public static InputButtonProcessor Select = new InputButtonProcessor();

        public static InputButtonProcessor Unassigned = new InputButtonProcessor();

        public static InputButtonProcessor LeftStick = new InputButtonProcessor();

        public static InputButtonProcessor RightStick = new InputButtonProcessor();

        public static InputButtonProcessor MenuDown = new InputButtonProcessor();

        public static InputButtonProcessor MenuUp = new InputButtonProcessor();

        public static InputButtonProcessor MenuLeft = new InputButtonProcessor();

        public static InputButtonProcessor MenuRight = new InputButtonProcessor();

        public static InputButtonProcessor MenuPageLeft = new InputButtonProcessor();

        public static InputButtonProcessor MenuPageRight = new InputButtonProcessor();

        public static InputButtonProcessor ActionButtonA = new InputButtonProcessor();

        public static InputButtonProcessor Cancel = new InputButtonProcessor();

        public static InputButtonProcessor LeftClick = new InputButtonProcessor();

        public static InputButtonProcessor RightClick = new InputButtonProcessor();

        public static InputButtonProcessor Copy = new InputButtonProcessor();

        public static InputButtonProcessor Delete = new InputButtonProcessor();

        public static InputButtonProcessor Focus = new InputButtonProcessor();

        public static InputButtonProcessor Filter;

        public static InputButtonProcessor Legend;

        public static Vector2 CursorPosition;

        public static bool CursorMoved;

        public static InputButtonProcessor[] Buttons;

        public static InputButtonProcessor Stomp = new InputButtonProcessor();

        public class InputButtonProcessor {
            public void Update(bool isPressed) {
                WasPressed = IsPressed;
                IsPressed = isPressed;
            }

            public bool OnPressed => IsPressed && !WasPressed;

            public bool OnPressedNotUsed => IsPressed && !WasPressed && !Used;

            public bool OnReleased => !IsPressed && WasPressed;

            public bool Pressed => IsPressed;

            public bool Released => !IsPressed;

            public bool WasPressed;

            public bool IsPressed;

            public bool Used;
        }

        public enum Button {
            ButtonA,
            ButtonX,
            ButtonY,
            ButtonB,
            LeftTrigger,
            RightTrigger,
            LeftShoulder,
            RightShoulder,
            Left,
            Right,
            Up,
            Down,
            Unassigned,
            Any,
            LeftStick,
            RightStick
        }
    }
}

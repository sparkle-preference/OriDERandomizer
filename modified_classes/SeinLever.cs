using System;
using Core;
using Game;
using UnityEngine;

public class SeinLever : CharacterState, ISeinReceiver {
    public bool IsUsingLever => m_lever != null && m_lever.IsGrabbed;

    public bool FaceLeft {
        get {
            return m_sein.PlatformBehaviour.LeftRightMovement.SpriteMirror.FaceLeft;
        }
        set {
            m_sein.PlatformBehaviour.LeftRightMovement.SpriteMirror.FaceLeft = value;
        }
    }

    public bool InputLocked => m_lever != null && m_lever.IsGrabbed;

    private void ShowGrabHint() {
        if (PressToGrabMessage && !m_lever.GetComponent<RandomizerLeverHint>() && !GameController.Instance.InputLocked) {
            m_pressToGrabHint = UI.Hints.Show(PressToGrabMessage, HintLayer.HintZone, 3f);
        }
    }

    private void HideGrabHint() {
        if (m_pressToGrabHint) {
            m_pressToGrabHint.HideMessageScreen();
        }
    }

    private void ShowLeftRightHint() {
        var hint = m_lever.GetComponent<RandomizerLeverHint>();
        var message = hint ? hint.Message : PressLeftRightMessage;
        if (message && !GameController.Instance.InputLocked) {
            m_pressLeftRightHint = UI.Hints.Show(message, HintLayer.HintZone, 3f);
        }
    }

    private void HideLeftRightHint() {
        if (m_pressLeftRightHint) {
            m_pressLeftRightHint.HideMessageScreen();
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        m_sein = sein;
    }

    public void EnterLever(Lever lever) {
        m_lever = lever;
        m_lever.OnEnterLever();
        if (m_lever.CanBeGrabbed) {
            ShowGrabHint();
        }
    }

    public void ExitLever() {
        m_lever.OnExitLever();
        m_lever = null;
        HideGrabHint();
        HideLeftRightHint();
    }

    public override void OnExit() {
        if (m_lever) {
            ExitLever();
        }
    }

    public override void Awake() {
        base.Awake();
    }

    public override void OnDestroy() {
        base.OnDestroy();
    }

    public override void Serialize(Archive ar) {
        if (ar.Reading) {
            if (m_lever != null) {
                m_lever.OnExitLever();
            }

            m_moveSeinPosition = false;
            m_lever = null;
        }
    }

    public override void UpdateCharacterState() {
        var position = m_sein.Position;
        if (GameController.Instance.LockInputByAction) {
            return;
        }

        if (m_lever) {
            if (m_lever.IsGrabbed) {
                if (Core.Input.Glide.Released) {
                    ReleaseLever();
                } else {
                    UpdateLeverDirection();
                }
            } else {
                var num = Vector3.Distance(position, m_lever.Position);
                if (m_lever.InRange && num > m_lever.Radius) {
                    ExitLever();
                } else if (m_lever.InRange && Core.Input.Glide.OnPressed && !Core.Input.Glide.Used && m_lever.CanBeGrabbed && (!m_lever.NeedsToBeOnGround || m_sein.PlatformBehaviour.PlatformMovement.IsOnGround) && !m_sein.Controller.IsCarrying) {
                    Core.Input.Glide.Used = true;
                    GrabLever();
                }
            }
        } else {
            for (var i = 0; i < Lever.All.Count; i++) {
                var lever = Lever.All[i];
                var num2 = Vector3.Distance(position, lever.Position);
                if (num2 < lever.Radius) {
                    EnterLever(lever);
                }
            }
        }

        if (m_moveSeinPosition && m_lever) {
            var vector = m_lever.Position - m_sein.PlatformBehaviour.PlatformMovement.FeetPosition;
            vector += m_sein.PlatformBehaviour.PlatformMovement.GroundBinormal * m_seinTargetPositionX;
            vector = MoonMath.Angle.Unrotate(vector, m_sein.PlatformBehaviour.PlatformMovement.GroundAngle);
            vector.y = 0f;
            vector.x *= 0.5f;
            vector = MoonMath.Angle.Rotate(vector, m_sein.PlatformBehaviour.PlatformMovement.GroundAngle);
            vector.z = 0f;
            m_sein.Position += vector;
        }
    }

    public void PlayLeftAnimation() {
        m_sein.Animation.PlayLoop(!FaceLeft ? Animations.LeverLeft : Animations.LeverRight, 200, m_lever.PlayLeverAnimation);
    }

    public void PlayMiddleAnimation() {
        m_sein.Animation.PlayLoop(Animations.LeverMiddle, 200, m_lever.PlayLeverAnimation);
    }

    public void PlayRightAnimation() {
        m_sein.Animation.PlayLoop(!FaceLeft ? Animations.LeverRight : Animations.LeverLeft, 200, m_lever.PlayLeverAnimation);
    }

    public void PushLeverMiddle() {
        m_lever.OnPushLeverMiddle();
        PlayMiddleAnimation();
        HideLeftRightHint();
    }

    public void PushLeverLeft() {
        m_lever.OnPushLeverLeft();
        PlayLeftAnimation();
        HideLeftRightHint();
    }

    public void PushLeverRight() {
        m_lever.OnPushLeverRight();
        PlayRightAnimation();
        HideLeftRightHint();
    }

    public void GrabLever() {
        m_lever.OnGrabLever();
        m_moveSeinPosition = true;
        var flag = Vector3.Dot(m_sein.PlatformBehaviour.PlatformMovement.GroundBinormal, m_sein.PlatformBehaviour.PlatformMovement.Position - m_lever.Position) < 0f;
        m_seinTargetPositionX = m_lever.SeinPositionOffset * (!flag ? 1 : -1);
        FaceLeft = !flag;
        switch (m_lever.Direction) {
            case Lever.LeverDirections.Left:
                PlayLeftAnimation();
                break;
            case Lever.LeverDirections.Middle:
                PlayMiddleAnimation();
                break;
            case Lever.LeverDirections.Right:
                PlayRightAnimation();
                break;
        }

        HideGrabHint();
        ShowLeftRightHint();
    }

    public void ReleaseLever() {
        m_lever.OnReleaseLever();
        m_moveSeinPosition = false;
    }

    private void UpdateLeverDirection() {
        switch (m_lever.Direction) {
            case Lever.LeverDirections.Left:
                UpdateLeft();
                break;
            case Lever.LeverDirections.Right:
                UpdateRight();
                break;
            case Lever.LeverDirections.Middle:
                UpdateMiddle();
                break;
        }

        // a lever's own hint stays up while it is held in a direction
        if (m_lever.Direction != Lever.LeverDirections.Middle && m_lever.GetComponent<RandomizerLeverHint>()) {
            if (m_pressLeftRightHint) {
                m_pressLeftRightHint.Visibility.ResetWaitDuration();
            } else {
                ShowLeftRightHint();
            }
        }
    }

    private void UpdateMiddle() {
        switch (m_lever.LeverType) {
            case Lever.LeverMode.LeftMiddleRightSpring:
                if (Core.Input.Left.Pressed) {
                    PushLeverLeft();
                } else if (Core.Input.Right.Pressed) {
                    PushLeverRight();
                }

                break;
            case Lever.LeverMode.LeftMiddleRightStay:
                if (Core.Input.Left.OnPressed) {
                    PushLeverLeft();
                } else if (Core.Input.Right.OnPressed) {
                    PushLeverRight();
                }

                break;
        }
    }

    private void UpdateLeft() {
        switch (m_lever.LeverType) {
            case Lever.LeverMode.LeftRightGrab:
                if (Core.Input.Right.Pressed) {
                    PushLeverRight();
                }

                break;
            case Lever.LeverMode.LeftMiddleRightSpring:
                if (Core.Input.Right.Pressed) {
                    PushLeverRight();
                } else if (Core.Input.Left.Released || !m_lever.CanLeverLeft()) {
                    PushLeverMiddle();
                }

                break;
            case Lever.LeverMode.LeftMiddleRightStay:
                if (Core.Input.Right.OnPressed || !m_lever.CanLeverLeft()) {
                    PushLeverMiddle();
                }

                break;
        }
    }

    private void UpdateRight() {
        switch (m_lever.LeverType) {
            case Lever.LeverMode.LeftRightToggle:
                if (Core.Input.Glide.Released) {
                    ReleaseLever();
                }

                break;
            case Lever.LeverMode.LeftRightGrab:
                if (Core.Input.Left.Pressed) {
                    PushLeverLeft();
                }

                break;
            case Lever.LeverMode.LeftMiddleRightSpring:
                if (Core.Input.Left.OnPressed) {
                    PushLeverLeft();
                } else if (Core.Input.Right.Released || !m_lever.CanLeverRight()) {
                    PushLeverMiddle();
                }

                break;
            case Lever.LeverMode.LeftMiddleRightStay:
                if (Core.Input.Left.OnPressed || !m_lever.CanLeverRight()) {
                    PushLeverMiddle();
                }

                break;
        }
    }

    public LeverAnimations Animations;

    public MessageProvider PressToGrabMessage;

    public MessageProvider PressLeftRightMessage;

    private Lever m_lever;

    private bool m_moveSeinPosition;

    private SeinCharacter m_sein;

    private float m_seinTargetPositionX;

    private MessageBox m_pressToGrabHint;

    private MessageBox m_pressLeftRightHint;

    [Serializable]
    public class LeverAnimations {
        public TextureAnimationWithTransitions LeverLeft;

        public TextureAnimationWithTransitions LeverMiddle;

        public TextureAnimationWithTransitions LeverRight;
    }
}

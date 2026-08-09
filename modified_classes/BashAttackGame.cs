using System;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

internal class BashAttackGame : Suspendable, IPooled {
    public event Action<float> BashGameComplete;

    public override bool IsSuspended { get; set; }

    public void OnPoolSpawned() {
        m_bashLoopingAudioSource = null;
        m_keyboardSpeed = 0f;
        m_keyboardAngle = 0f;
        m_keyboardClockwise = false;
        m_mode = Modes.Keyboard;
        m_currentState = State.Appearing;
        Angle = 0f;
        m_stateCurrentTime = 0f;
        m_nextBashLoopPlayedTime = 0f;
        BashAttackCritical.enabled = true;
        IsSuspended = false;
        BashGameComplete = null;
    }

    public void ChangeState(State state) {
        m_currentState = state;
        m_stateCurrentTime = 0f;
        switch (state) {
            case State.Appearing:
                BashAttackCritical.enabled = false;
                return;
            case State.Playing:
                BashAttackCritical.enabled = true;
                return;
            case State.Disappearing:
                BashAttackCritical.enabled = false;
                if (m_bashLoopingAudioSource) {
                    InstantiateUtility.Destroy(m_bashLoopingAudioSource.gameObject);
                }

                return;
            default:
                return;
        }
    }

    public void UpdateMode() {
        if (Input.AnalogAxisLeft.magnitude > 0.2f) {
            m_mode = Modes.Controller;
            return;
        }

        if (Input.CursorMoved || GameSettings.Instance.CurrentControlScheme == ControlScheme.KeyboardAndMouse) {
            m_mode = Modes.Mouse;
            return;
        }

        if (Input.DigiPadAxis.magnitude > 0.2f && m_mode != Modes.Mouse) {
            m_mode = Modes.Keyboard;
        }
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (m_currentState != State.Disappearing) {
            UpdateMode();
            switch (m_mode) {
                case Modes.Mouse: {
                    Vector2 v = UI.Cameras.Current.Camera.WorldToScreenPoint(transform.position);
                    Vector2 b = UI.Cameras.System.GUICamera.ScreenToWorldPoint(v);
                    Vector2 vector = Input.CursorPositionUI - b;
                    if (vector.magnitude > 0.001f) {
                        vector.Normalize();
                        Angle = Mathf.LerpAngle(Angle, Mathf.Atan2(-vector.x, vector.y) * 57.29578f, 0.5f);
                    }

                    break;
                }
                case Modes.Keyboard: {
                    Vector2 digiPadAxis = Input.DigiPadAxis;
                    if (digiPadAxis.magnitude > 0.2) {
                        float target = MoonMath.Angle.AngleFromVector(digiPadAxis) - 90f;
                        float f = Mathf.DeltaAngle(m_keyboardAngle, target);
                        if (Mathf.Sign(f) != (!m_keyboardClockwise ? -1 : 1)) {
                            m_keyboardClockwise = Mathf.Sign(f) > 0f;
                            m_keyboardSpeed = 0f;
                        }

                        m_keyboardSpeed += Mathf.Min(Mathf.Abs(f), Time.deltaTime * 2000f);
                        m_keyboardAngle = Mathf.MoveTowardsAngle(m_keyboardAngle, target, m_keyboardSpeed * Time.deltaTime);
                    } else {
                        m_keyboardSpeed = 0f;
                    }

                    Angle = Mathf.LerpAngle(Angle, m_keyboardAngle, 0.5f);
                    break;
                }
                case Modes.Controller: {
                    Vector2 vector2 = Input.AnalogAxisLeft;
                    float sqrMagnitude = vector2.sqrMagnitude;
                    if (sqrMagnitude > RandomizerSettings.Controls.BashDeadzone) {
                        vector2 /= Mathf.Sqrt(sqrMagnitude);
                        Angle = Mathf.LerpAngle(Angle, Mathf.Atan2(-vector2.x, vector2.y) * 57.29578f, 0.5f);
                    }

                    break;
                }
            }
        }

        ArrowSprite.transform.parent.rotation = Quaternion.Euler(0f, 0f, Angle);
        UpdateState();
        if (Characters.Sein && !Characters.Sein.Active) {
            InstantiateUtility.Destroy(gameObject);
        }
    }

    public void SendDirection(Vector2 direction) {
        m_keyboardAngle = MoonMath.Angle.AngleFromVector(direction) - 90f;
    }

    public void UpdateState() {
        switch (m_currentState) {
            case State.Appearing:
                UpdateAppearingState();
                break;
            case State.Playing:
                UpdatePlayingState();
                break;
            case State.Disappearing:
                UpdateDisappearingState();
                break;
        }

        m_stateCurrentTime += Time.deltaTime;
    }

    private void UpdateDisappearingState() {
        float time = Mathf.Clamp01(m_stateCurrentTime / DisappearTime);
        ArrowSprite.localScale = m_originalArrowScale * ArrowDisappearScaleCurve.Evaluate(time);
        InstantiateUtility.Destroy(gameObject, 1f);
    }

    private void UpdatePlayingState() {
        if (m_nextBashLoopPlayedTime <= m_stateCurrentTime) {
            m_bashLoopingAudioSource = Sound.Play(!Characters.Sein.PlayerAbilities.BashBuff.HasAbility ? Characters.Sein.Abilities.Bash.BashLoopSound.GetSound(null) : Characters.Sein.Abilities.Bash.UpgradedBashLoopSound.GetSound(null), transform.position, delegate { m_bashLoopingAudioSource = null; });
            if (!InstantiateUtility.IsDestroyed(m_bashLoopingAudioSource)) {
                m_nextBashLoopPlayedTime = m_stateCurrentTime + m_bashLoopingAudioSource.Length;
            }
        }

        if (BashAttackCritical.CurrentState == BashAttackCritical.State.Finished) {
            GameFinished();
        }

        if (ButtonBash.Released || (RandomizerRebinding.DoubleBash.Pressed && Randomizer.BashTap)) {
            GameFinished();
        }
    }

    private void UpdateAppearingState() {
        float num = Mathf.Clamp01(m_stateCurrentTime / AppearTime);
        ArrowSprite.localScale = m_originalArrowScale * ArrowAppearScaleCurve.Evaluate(num);
        if (num == 1f) {
            ChangeState(State.Playing);
        }
    }

    public new void Awake() {
        base.Awake();
        m_originalArrowScale = ArrowSprite.localScale;
    }

    public void Start() {
        ChangeState(m_currentState);
        ArrowSprite.localScale = Vector3.zero;
    }

    private void GameFinished() {
        Sound.Play(!Characters.Sein.PlayerAbilities.BashBuff.HasAbility ? Characters.Sein.Abilities.Bash.BashEndSound.GetSound(null) : Characters.Sein.Abilities.Bash.UpgradedBashEndSound.GetSound(null), transform.position, null);
        BashGameComplete(Angle);
        ChangeState(State.Disappearing);
        if (RandomizerRebinding.DoubleBash.Pressed && !Randomizer.BashWasQueued) {
            Randomizer.QueueBash = true;
        }

        Randomizer.BashWasQueued = false;
    }

    public Input.InputButtonProcessor ButtonBash => Input.Bash;

    public float Angle;

    public float ArrowSpeed = 45f;

    public Transform ArrowSprite;

    public BashAttackCritical BashAttackCritical;

    public float AppearTime;

    public float DisappearTime;

    public AnimationCurve ArrowAppearScaleCurve;

    public AnimationCurve ArrowDisappearScaleCurve;

    private State m_currentState;

    private float m_stateCurrentTime;

    private float m_nextBashLoopPlayedTime;

    private Vector3 m_originalArrowScale;

    private SoundPlayer m_bashLoopingAudioSource;

    private float m_keyboardSpeed;

    private float m_keyboardAngle;

    private bool m_keyboardClockwise;

    private Modes m_mode = Modes.Keyboard;

    public enum State {
        Appearing,
        Playing,
        Disappearing
    }

    public enum Modes {
        Mouse,
        Keyboard,
        Controller
    }
}

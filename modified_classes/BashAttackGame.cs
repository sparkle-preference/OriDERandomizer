using System;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

internal class BashAttackGame : Suspendable, IPooled {
    public event Action<float> OnBashGameComplete;

    public override bool IsSuspended { get; set; }

    public void OnPoolSpawned() {
        bashLoopingAudioSource = null;
        keyboardSpeed = 0f;
        keyboardAngle = 0f;
        keyboardClockwise = false;
        mode = Modes.Keyboard;
        currentState = State.Appearing;
        Angle = 0f;
        stateCurrentTime = 0f;
        nextBashLoopPlayedTime = 0f;
        BashAttackCritical.enabled = true;
        IsSuspended = false;
        OnBashGameComplete = null;
    }

    public void ChangeState(State state) {
        currentState = state;
        stateCurrentTime = 0f;
        switch (state) {
            case State.Appearing:
                BashAttackCritical.enabled = false;
                return;
            case State.Playing:
                BashAttackCritical.enabled = true;
                return;
            case State.Disappearing:
                BashAttackCritical.enabled = false;
                if (bashLoopingAudioSource) {
                    InstantiateUtility.Destroy(bashLoopingAudioSource.gameObject);
                }

                return;
            default:
                return;
        }
    }

    public void UpdateMode() {
        if (Input.AnalogAxisLeft.magnitude > 0.2f) {
            mode = Modes.Controller;
            return;
        }

        if (Input.CursorMoved || GameSettings.Instance.CurrentControlScheme == ControlScheme.KeyboardAndMouse) {
            mode = Modes.Mouse;
            return;
        }

        if (Input.DigiPadAxis.magnitude > 0.2f && mode != Modes.Mouse) {
            mode = Modes.Keyboard;
        }
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (currentState != State.Disappearing) {
            UpdateMode();
            switch (mode) {
                case Modes.Mouse: {
                    Vector2 v = UI.Cameras.Current.Camera.WorldToScreenPoint(transform.position);
                    Vector2 b = UI.Cameras.System.GUICamera.ScreenToWorldPoint(v);
                    var vector = Input.CursorPositionUI - b;
                    if (vector.magnitude > 0.001f) {
                        vector.Normalize();
                        Angle = Mathf.LerpAngle(Angle, Mathf.Atan2(-vector.x, vector.y) * 57.29578f, 0.5f);
                    }

                    break;
                }
                case Modes.Keyboard: {
                    var digiPadAxis = Input.DigiPadAxis;
                    if (digiPadAxis.magnitude > 0.2) {
                        var target = MoonMath.Angle.AngleFromVector(digiPadAxis) - 90f;
                        var f = Mathf.DeltaAngle(keyboardAngle, target);
                        if (Mathf.Sign(f) != (!keyboardClockwise ? -1 : 1)) {
                            keyboardClockwise = Mathf.Sign(f) > 0f;
                            keyboardSpeed = 0f;
                        }

                        keyboardSpeed += Mathf.Min(Mathf.Abs(f), Time.deltaTime * 2000f);
                        keyboardAngle = Mathf.MoveTowardsAngle(keyboardAngle, target, keyboardSpeed * Time.deltaTime);
                    } else {
                        keyboardSpeed = 0f;
                    }

                    Angle = Mathf.LerpAngle(Angle, keyboardAngle, 0.5f);
                    break;
                }
                case Modes.Controller: {
                    var vector2 = Input.AnalogAxisLeft;
                    var sqrMagnitude = vector2.sqrMagnitude;
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
        keyboardAngle = MoonMath.Angle.AngleFromVector(direction) - 90f;
    }

    public void UpdateState() {
        switch (currentState) {
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

        stateCurrentTime += Time.deltaTime;
    }

    private void UpdateDisappearingState() {
        var time = Mathf.Clamp01(stateCurrentTime / DisappearTime);
        ArrowSprite.localScale = originalArrowScale * ArrowDisappearScaleCurve.Evaluate(time);
        InstantiateUtility.Destroy(gameObject, 1f);
    }

    private void UpdatePlayingState() {
        if (nextBashLoopPlayedTime <= stateCurrentTime) {
            bashLoopingAudioSource = Sound.Play(!Characters.Sein.PlayerAbilities.BashBuff.HasAbility ? Characters.Sein.Abilities.Bash.BashLoopSound.GetSound(null) : Characters.Sein.Abilities.Bash.UpgradedBashLoopSound.GetSound(null), transform.position, delegate { bashLoopingAudioSource = null; });
            if (!InstantiateUtility.IsDestroyed(bashLoopingAudioSource)) {
                nextBashLoopPlayedTime = stateCurrentTime + bashLoopingAudioSource.Length;
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
        var num = Mathf.Clamp01(stateCurrentTime / AppearTime);
        ArrowSprite.localScale = originalArrowScale * ArrowAppearScaleCurve.Evaluate(num);
        if (num == 1f) {
            ChangeState(State.Playing);
        }
    }

    public new void Awake() {
        base.Awake();
        originalArrowScale = ArrowSprite.localScale;
    }

    public void Start() {
        ChangeState(currentState);
        ArrowSprite.localScale = Vector3.zero;
    }

    private void GameFinished() {
        Sound.Play(!Characters.Sein.PlayerAbilities.BashBuff.HasAbility ? Characters.Sein.Abilities.Bash.BashEndSound.GetSound(null) : Characters.Sein.Abilities.Bash.UpgradedBashEndSound.GetSound(null), transform.position, null);
        OnBashGameComplete(Angle);
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

    private State currentState;

    private float stateCurrentTime;

    private float nextBashLoopPlayedTime;

    private Vector3 originalArrowScale;

    private SoundPlayer bashLoopingAudioSource;

    private float keyboardSpeed;

    private float keyboardAngle;

    private bool keyboardClockwise;

    private Modes mode = Modes.Keyboard;

    public enum State {
        Appearing,
        Playing,
        Disappearing,
    }

    public enum Modes {
        Mouse,
        Keyboard,
        Controller,
    }
}

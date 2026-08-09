using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinWallChargeJump : CharacterState, ISeinReceiver {
    public PlayerAbilities PlayerAbilities => m_sein.PlayerAbilities;

    public PlatformMovement PlatformMovement => m_sein.PlatformBehaviour.PlatformMovement;

    public void OnDoubleJump() {
        ChangeState(State.Normal);
    }

    public override void UpdateCharacterState() {
        if (m_sein.IsSuspended) {
            return;
        }

        UpdateState();
    }

    public float AngularElevation => m_angularElevation;

    public override void OnExit() {
        base.OnExit();
        ChangeState(State.Normal);
    }

    public void Start() {
        m_sein.PlatformBehaviour.Gravity.ModifyGravityPlatformMovementSettingsEvent += ModifyGravityPlatformMovementSettings;
    }

    public override void OnDestroy() {
        base.OnDestroy();
        m_sein.PlatformBehaviour.Gravity.ModifyGravityPlatformMovementSettingsEvent -= ModifyGravityPlatformMovementSettings;
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
    }

    public void OnAnimationEnd() {
        SpriteMirrorLock = false;
    }

    public void OnAnimationStart() {
        SpriteMirrorLock = true;
    }

    public void ModifyGravityPlatformMovementSettings(GravityPlatformMovementSettings settings) {
        if (m_currentState == State.Jumping) {
            settings.GravityStrength = 0f;
        }
    }

    public void ChangeState(State state) {
        m_attackablesIgnore.Clear();
        State currentState = m_currentState;
        if (currentState == State.Aiming) {
            if (Arrow) {
                Arrow.AnimatorDriver.ContinueBackwards();
            }
        }

        m_currentState = state;
        m_stateCurrentTime = 0f;
        currentState = m_currentState;
        if (currentState != State.Normal) {
            if (currentState == State.Aiming) {
                if (m_sein.Abilities.GrabWall) {
                    m_sein.Abilities.GrabWall.LockVerticalMovement = true;
                }

                if (Arrow) {
                    Arrow.AnimatorDriver.ContinueForward();
                }
            }
        } else if (m_sein.Abilities.GrabWall) {
            m_sein.Abilities.GrabWall.LockVerticalMovement = false;
        }
    }

    public bool IsCharged => m_sein.Controller.IsGrabbingWall && m_sein.Abilities.GrabWall.IsGrabbingAway && Characters.Sein.Controller.CanMove && m_sein.Abilities.ChargeJumpCharging.IsCharged;

    public bool IsCharging => m_sein.Controller.IsGrabbingWall && m_sein.Abilities.GrabWall.IsGrabbingAway && Characters.Sein.Controller.CanMove && m_sein.Abilities.ChargeJumpCharging.IsCharging;

    public void UpdateState() {
        switch (m_currentState) {
            case State.Normal:
                UpdateNormalState();
                break;
            case State.Aiming:
                UpdateAimingState();
                break;
            case State.Jumping:
                UpdateJumpingState();
                break;
        }

        m_stateCurrentTime += Time.deltaTime;
    }

    public void UpdateNormalState() {
        if (PlayerAbilities.ChargeJump.HasAbility) {
            if (IsCharged) {
                ChangeState(State.Aiming);
            } else if (IsCharging) {
                UpdateAimElevation();
            } else {
                m_angularElevation = 0f;
            }
        }
    }

    public void UpdateJumpingState() {
        float adjustedDrag = HorizontalDrag - HorizontalDrag * 0.08f * (RandomizerBonus.Velocity() + RandomizerBonus.Jumpgrades());
        PlatformMovement.LocalSpeedX = PlatformMovement.LocalSpeedX * (1f - adjustedDrag);
        PlatformMovement.LocalSpeedY = PlatformMovement.LocalSpeedY * (1f - adjustedDrag);
        if (m_stateCurrentTime > AntiGravityDuration + AntiGravityDuration * 0.08f * (RandomizerBonus.Velocity() + RandomizerBonus.Jumpgrades())) {
            ChangeState(State.Normal);
            return;
        }

        m_sein.PlatformBehaviour.Visuals.SpriteRotater.CenterAngle = m_angleDirection;
        m_sein.PlatformBehaviour.Visuals.SpriteRotater.UpdateRotation();
        for (int i = 0; i < Targets.Attackables.Count; i++) {
            IAttackable attackable = Targets.Attackables[i];
            if (!m_attackablesIgnore.Contains(attackable)) {
                if (attackable.CanBeStomped()) {
                    Vector3 vector = attackable.Position - m_sein.PlatformBehaviour.PlatformMovement.Position;
                    float magnitude = vector.magnitude;
                    if (magnitude < 4f && Vector2.Dot(vector.normalized, PlatformMovement.LocalSpeed.normalized) > 0f) {
                        m_attackablesIgnore.Add(attackable);
                        Damage damage = new Damage(Damage, PlatformMovement.WorldSpeed.normalized * 3f, m_sein.Position, DamageType.Stomp, gameObject);
                        damage.DealToComponents(((Component)attackable).gameObject);
                        if (ExplosionEffect) {
                            InstantiateUtility.Instantiate(ExplosionEffect, Vector3.Lerp(transform.position, attackable.Position, 0.5f), Quaternion.identity);
                        }

                        break;
                    }
                }
            }
        }
    }

    public void UpdateAimElevation() {
        float normalizedFacing = PlatformMovement.HasWallLeft ? 1 : -1;
        Vector2 analogAxisLeft = Input.AnalogAxisLeft;

        if (analogAxisLeft.magnitude > 0.2f) {
            m_angularElevationSpeed = 0f;
            m_angularElevation = Mathf.Atan2(analogAxisLeft.y, analogAxisLeft.x * normalizedFacing) * 57.29578f;
            return;
        }

        if (Input.Up.Pressed && !Input.Down.Pressed) {
            m_angularElevationSpeed = Mathf.Clamp(m_angularElevationSpeed + Time.deltaTime * 500f, 0f, 200f);
            return;
        }

        if (Input.Down.Pressed) {
            m_angularElevationSpeed = Mathf.Clamp(m_angularElevationSpeed - Time.deltaTime * 500f, -200f, 0f);
            return;
        }

        m_angularElevationSpeed = 0f;

        if (RandomizerSettings.Controls.WallChargeMouseAim) {
            Vector2 arrowScreenPos = UI.Cameras.Current.Camera.WorldToScreenPoint(Arrow.transform.position);
            Vector2 arrowWorldPos = UI.Cameras.System.GUICamera.Camera.ScreenToWorldPoint(arrowScreenPos);
            Vector2 cursorAxis = Input.CursorPositionUI - arrowWorldPos;

            if (Input.CursorMoved && cursorAxis.magnitude > 1f && MoonMath.Float.Normalize(cursorAxis.x) == normalizedFacing) {
                float axisElevation = Mathf.Atan2(cursorAxis.y, cursorAxis.x * normalizedFacing) * 57.29578f;
                if (Mathf.Abs(axisElevation) <= 60f) {
                    m_angularElevation = axisElevation;
                }
            }
        }
    }

    public void UpdateAimingState() {
        if (!IsCharged) {
            ChangeState(State.Normal);
        }

        if (Arrow) {
            UpdateAimElevation();
            bool hasWallLeft = PlatformMovement.HasWallLeft;
            m_angularElevation = Mathf.Clamp(m_angularElevation + m_angularElevationSpeed * Time.deltaTime, -45f, 45f);
            Arrow.transform.eulerAngles = new Vector3(0f, 0f, !hasWallLeft ? 180f - m_angularElevation : m_angularElevation);
        }
    }

    public bool CanChargeJump => m_sein.Abilities.GrabWall.IsGrabbing && m_sein.Abilities.ChargeJumpCharging.IsCharged && m_currentState == State.Aiming;

    public void OnRestoreCheckpoint() {
        m_spriteMirrorLock = false;
    }

    public override void Awake() {
        base.Awake();
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
    }

    public CharacterSpriteMirror CharacterSpriteMirror => m_sein.PlatformBehaviour.Visuals.SpriteMirror;

    public bool SpriteMirrorLock {
        get => m_spriteMirrorLock;
        set {
            if (m_spriteMirrorLock != value) {
                m_spriteMirrorLock = value;
                if (value) {
                    CharacterSpriteMirror.Lock++;
                } else {
                    CharacterSpriteMirror.Lock--;
                }
            }
        }
    }

    public void PerformChargeJump() {
        float chargedJumpStrength = ChargedJumpStrength + ChargedJumpStrength * 0.08f * (RandomizerBonus.Velocity() + RandomizerBonus.Jumpgrades());
        PlatformMovement.LocalSpeedX = chargedJumpStrength * Arrow.transform.right.x;
        PlatformMovement.LocalSpeedY = chargedJumpStrength * Arrow.transform.right.y;
        Vector2 normalized = m_sein.PlatformBehaviour.PlatformMovement.LocalSpeed.normalized;
        m_angleDirection = Mathf.Atan2(normalized.y, Mathf.Abs(normalized.x)) * 57.29578f * (normalized.x >= 0f ? 1 : -1);
        Sound.Play(JumpSound.GetSound(null), m_sein.PlatformBehaviour.PlatformMovement.Position, null);
        m_sein.Mortality.DamageReciever.MakeInvincibleToEnemies(AntiGravityDuration);
        ChangeState(State.Jumping);
        m_sein.FaceLeft = PlatformMovement.LocalSpeedX < 0f;
        CharacterAnimationSystem.CharacterAnimationState characterAnimationState = m_sein.PlatformBehaviour.Visuals.Animation.Play(JumpAnimation, 10, ShouldChargeJumpAnimationKeepPlaying);
        characterAnimationState.OnStartPlaying = OnAnimationStart;
        characterAnimationState.OnStopPlaying = OnAnimationEnd;
        m_sein.PlatformBehaviour.Visuals.SpriteRotater.BeginTiltUpDownInAir(1.5f);
        if (m_sein.Abilities.Glide) {
            m_sein.Abilities.Glide.NeedsRightTriggerReleased = true;
        }

        JumpFlipPlatform.OnSeinChargeJumpEvent();
        m_sein.Abilities.ChargeJumpCharging.EndCharge();
    }

    public bool ShouldChargeJumpAnimationKeepPlaying() {
        return PlatformMovement.IsInAir && !PlatformMovement.IsOnWall && !PlatformMovement.IsOnCeiling;
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        m_sein = sein;
        m_sein.Abilities.WallChargeJump = this;
    }

    public TextureAnimationWithTransitions ChargeAnimation;

    public TextureAnimationWithTransitions JumpAnimation;

    public SoundProvider JumpSound;

    public float AntiGravityDuration = 0.2f;

    public float HorizontalDrag = 30f;

    public BaseAnimator Arrow;

    public int Damage = 50;

    public float ChargedJumpStrength;

    public State m_currentState;

    public float m_angularElevation;

    public float m_angularElevationSpeed;

    public float m_stateCurrentTime;

    public float m_angleDirection;

    public bool m_spriteMirrorLock;

    public SeinCharacter m_sein;

    public HashSet<IAttackable> m_attackablesIgnore = new HashSet<IAttackable>();

    public GameObject ExplosionEffect;

    public enum State {
        Normal,
        Aiming,
        Jumping
    }
}

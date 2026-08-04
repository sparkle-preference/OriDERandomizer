using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using UnityEngine.Serialization;
using Input = Core.Input;

public class SeinWallChargeJump : CharacterState, ISeinReceiver {
    public PlayerAbilities PlayerAbilities => Sein.PlayerAbilities;

    public PlatformMovement PlatformMovement => Sein.PlatformBehaviour.PlatformMovement;

    public void OnDoubleJump() {
        ChangeState(State.Normal);
    }

    public override void UpdateCharacterState() {
        if (Sein.IsSuspended) {
            return;
        }

        UpdateState();
    }

    public override void OnExit() {
        base.OnExit();
        ChangeState(State.Normal);
    }

    public void Start() {
        Sein.PlatformBehaviour.Gravity.ModifyGravityPlatformMovementSettingsEvent += ModifyGravityPlatformMovementSettings;
    }

    public override void OnDestroy() {
        base.OnDestroy();
        Sein.PlatformBehaviour.Gravity.ModifyGravityPlatformMovementSettingsEvent -= ModifyGravityPlatformMovementSettings;
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
    }

    public void OnAnimationEnd() {
        SpriteMirrorLock = false;
    }

    public void OnAnimationStart() {
        SpriteMirrorLock = true;
    }

    public void ModifyGravityPlatformMovementSettings(GravityPlatformMovementSettings settings) {
        if (CurrentState == State.Jumping) {
            settings.GravityStrength = 0f;
        }
    }

    public void ChangeState(State state) {
        AttackablesIgnore.Clear();
        var currentState = this.CurrentState;
        if (currentState == State.Aiming) {
            if (Arrow) {
                Arrow.AnimatorDriver.ContinueBackwards();
            }
        }

        this.CurrentState = state;
        StateCurrentTime = 0f;
        currentState = this.CurrentState;
        if (currentState != State.Normal) {
            if (currentState == State.Aiming) {
                if (Sein.Abilities.GrabWall) {
                    Sein.Abilities.GrabWall.LockVerticalMovement = true;
                }

                if (Arrow) {
                    Arrow.AnimatorDriver.ContinueForward();
                }
            }
        } else if (Sein.Abilities.GrabWall) {
            Sein.Abilities.GrabWall.LockVerticalMovement = false;
        }
    }

    public bool IsCharged => Sein.Controller.IsGrabbingWall && Sein.Abilities.GrabWall.IsGrabbingAway && Characters.Sein.Controller.CanMove && Sein.Abilities.ChargeJumpCharging.IsCharged;

    public bool IsCharging => Sein.Controller.IsGrabbingWall && Sein.Abilities.GrabWall.IsGrabbingAway && Characters.Sein.Controller.CanMove && Sein.Abilities.ChargeJumpCharging.IsCharging;

    public void UpdateState() {
        switch (CurrentState) {
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

        StateCurrentTime += Time.deltaTime;
    }

    public void UpdateNormalState() {
        if (PlayerAbilities.ChargeJump.HasAbility) {
            if (IsCharged) {
                ChangeState(State.Aiming);
            } else if (IsCharging) {
                UpdateAimElevation();
            } else {
                AngularElevation = 0f;
            }
        }
    }

    public void UpdateJumpingState() {
        var adjustedDrag = HorizontalDrag - HorizontalDrag * 0.08f * (RandomizerBonus.Velocity() + RandomizerBonus.Jumpgrades());
        PlatformMovement.LocalSpeedX *= (1f - adjustedDrag);
        PlatformMovement.LocalSpeedY *= (1f - adjustedDrag);
        if (StateCurrentTime > AntiGravityDuration + AntiGravityDuration * 0.08f * (RandomizerBonus.Velocity() + RandomizerBonus.Jumpgrades())) {
            ChangeState(State.Normal);
            return;
        }

        Sein.PlatformBehaviour.Visuals.SpriteRotater.CenterAngle = AngleDirection;
        Sein.PlatformBehaviour.Visuals.SpriteRotater.UpdateRotation();
        for (var i = 0; i < Targets.Attackables.Count; i++) {
            var attackable = Targets.Attackables[i];
            if (!AttackablesIgnore.Contains(attackable)) {
                if (attackable.CanBeStomped()) {
                    var vector = attackable.Position - Sein.PlatformBehaviour.PlatformMovement.Position;
                    var magnitude = vector.magnitude;
                    if (magnitude < 4f && Vector2.Dot(vector.normalized, PlatformMovement.LocalSpeed.normalized) > 0f) {
                        AttackablesIgnore.Add(attackable);
                        var damage = new Damage(Damage, PlatformMovement.WorldSpeed.normalized * 3f, Sein.Position, DamageType.Stomp, gameObject);
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
        var analogAxisLeft = Input.AnalogAxisLeft;

        if (analogAxisLeft.magnitude > 0.2f) {
            AngularElevationSpeed = 0f;
            AngularElevation = Mathf.Atan2(analogAxisLeft.y, analogAxisLeft.x * normalizedFacing) * 57.29578f;
            return;
        }

        if (Input.Up.Pressed && !Input.Down.Pressed) {
            AngularElevationSpeed = Mathf.Clamp(AngularElevationSpeed + Time.deltaTime * 500f, 0f, 200f);
            return;
        }

        if (Input.Down.Pressed) {
            AngularElevationSpeed = Mathf.Clamp(AngularElevationSpeed - Time.deltaTime * 500f, -200f, 0f);
            return;
        }

        AngularElevationSpeed = 0f;

        if (RandomizerSettings.Controls.WallChargeMouseAim) {
            Vector2 arrowScreenPos = UI.Cameras.Current.Camera.WorldToScreenPoint(Arrow.transform.position);
            Vector2 arrowWorldPos = UI.Cameras.System.GUICamera.Camera.ScreenToWorldPoint(arrowScreenPos);
            var cursorAxis = Input.CursorPositionUI - arrowWorldPos;

            if (Input.CursorMoved && cursorAxis.magnitude > 1f && MoonMath.Float.Normalize(cursorAxis.x) == normalizedFacing) {
                var axisElevation = Mathf.Atan2(cursorAxis.y, cursorAxis.x * normalizedFacing) * 57.29578f;
                if (Mathf.Abs(axisElevation) <= 60f) {
                    AngularElevation = axisElevation;
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
            var hasWallLeft = PlatformMovement.HasWallLeft;
            AngularElevation = Mathf.Clamp(AngularElevation + AngularElevationSpeed * Time.deltaTime, -45f, 45f);
            Arrow.transform.eulerAngles = new Vector3(0f, 0f, !hasWallLeft ? 180f - AngularElevation : AngularElevation);
        }
    }

    public bool CanChargeJump => Sein.Abilities.GrabWall.IsGrabbing && Sein.Abilities.ChargeJumpCharging.IsCharged && CurrentState == State.Aiming;

    public void OnRestoreCheckpoint() {
        spriteMirrorLock = false;
    }

    public override void Awake() {
        base.Awake();
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
    }

    public CharacterSpriteMirror CharacterSpriteMirror => Sein.PlatformBehaviour.Visuals.SpriteMirror;

    public bool SpriteMirrorLock {
        get => spriteMirrorLock;
        set {
            if (spriteMirrorLock != value) {
                spriteMirrorLock = value;
                if (value) {
                    CharacterSpriteMirror.Lock++;
                } else {
                    CharacterSpriteMirror.Lock--;
                }
            }
        }
    }

    public void PerformChargeJump() {
        var chargedJumpStrength = ChargedJumpStrength + ChargedJumpStrength * 0.08f * (RandomizerBonus.Velocity() + RandomizerBonus.Jumpgrades());
        PlatformMovement.LocalSpeedX = chargedJumpStrength * Arrow.transform.right.x;
        PlatformMovement.LocalSpeedY = chargedJumpStrength * Arrow.transform.right.y;
        var normalized = Sein.PlatformBehaviour.PlatformMovement.LocalSpeed.normalized;
        AngleDirection = Mathf.Atan2(normalized.y, Mathf.Abs(normalized.x)) * 57.29578f * (normalized.x >= 0f ? 1 : -1);
        Sound.Play(JumpSound.GetSound(null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        Sein.Mortality.DamageReciever.MakeInvincibleToEnemies(AntiGravityDuration);
        ChangeState(State.Jumping);
        Sein.FaceLeft = PlatformMovement.LocalSpeedX < 0f;
        var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(JumpAnimation, 10, ShouldChargeJumpAnimationKeepPlaying);
        characterAnimationState.OnStartPlaying = OnAnimationStart;
        characterAnimationState.OnStopPlaying = OnAnimationEnd;
        Sein.PlatformBehaviour.Visuals.SpriteRotater.BeginTiltUpDownInAir(1.5f);
        if (Sein.Abilities.Glide) {
            Sein.Abilities.Glide.NeedsRightTriggerReleased = true;
        }

        JumpFlipPlatform.OnSeinChargeJumpEvent();
        Sein.Abilities.ChargeJumpCharging.EndCharge();
    }

    public bool ShouldChargeJumpAnimationKeepPlaying() {
        return PlatformMovement.IsInAir && !PlatformMovement.IsOnWall && !PlatformMovement.IsOnCeiling;
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        this.Sein = sein;
        this.Sein.Abilities.WallChargeJump = this;
    }

    public TextureAnimationWithTransitions ChargeAnimation;

    public TextureAnimationWithTransitions JumpAnimation;

    public SoundProvider JumpSound;

    public float AntiGravityDuration = 0.2f;

    public float HorizontalDrag = 30f;

    public BaseAnimator Arrow;

    public int Damage = 50;

    public float ChargedJumpStrength;

    public State CurrentState;

    public float AngularElevation;

    public float AngularElevationSpeed;

    public float StateCurrentTime;

    public float AngleDirection;

    private bool spriteMirrorLock;

    public SeinCharacter Sein;

    public HashSet<IAttackable> AttackablesIgnore = new HashSet<IAttackable>();

    public GameObject ExplosionEffect;

    public enum State {
        Normal,
        Aiming,
        Jumping,
    }
}

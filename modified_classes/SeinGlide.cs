using Core;
using UnityEngine;
using Input = Core.Input;

public class SeinGlide : CharacterState, ISeinReceiver {
    public CharacterGravity CharacterGravity => Sein.PlatformBehaviour.Gravity;

    public CharacterLeftRightMovement CharacterLeftRightMovement => Sein.PlatformBehaviour.LeftRightMovement;

    public PlatformMovement PlatformMovement => Sein.PlatformBehaviour.PlatformMovement;

    public void Start() {
        CharacterGravity.ModifyGravityPlatformMovementSettingsEvent += ModifyGravityPlatformMovementSettings;
        CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent += ModifyHorizontalPlatformMovementSettings;
    }

    public new void OnDestroy() {
        base.OnDestroy();
        CharacterGravity.ModifyGravityPlatformMovementSettingsEvent -= ModifyGravityPlatformMovementSettings;
        CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent -= ModifyHorizontalPlatformMovementSettings;
        IsGliding = false;
    }

    public override void OnExit() {
        IsGliding = false;
    }

    public void ModifyGravityPlatformMovementSettings(GravityPlatformMovementSettings settings) {
        if (IsGliding && PlatformMovement.LocalSpeedY < 0f) {
            settings.GravityStrength *= GravityMultiplier;
        }
    }

    public void ModifyHorizontalPlatformMovementSettings(HorizontalPlatformMovementSettings settings) {
        if (IsGliding) {
            settings.Air.ApplySpeedMultiplier(MoveSpeed);
        }
    }

    public bool IsGliding {
        get => isGliding;
        set {
            if (isGliding != value) {
                isGliding = value;
                if (isGliding) {
                    OnEnterGlide();
                } else {
                    OnExitGlide();
                }
            }
        }
    }

    public void OnEnterGlide() {
        UpdateAnimations();
    }

    public void OnExitGlide() {
        if (parachuteLoopLastSound) {
            parachuteLoopLastSound.FadeOut(1f, true);
        }

        base.OnExit();
        if (RunningTime > 0.3f) {
            Sound.Play(CloseParachuteSound.GetSound(null), PlatformMovement.Position, null);
        }

        RunningTime = 0f;
        playedOpenSound = false;
    }

    public bool CanGlide => !PlatformMovement.IsOnGround && !PlatformMovement.IsOnWall && !Sein.Controller.InputLocked && !SeinAbilityRestrictZone.IsInside();

    public bool WantsToGlide => Input.Glide.Pressed && !NeedsRightTriggerReleased && lockGlidingRemainingTime <= 0f;

    public void LockGliding(float time) {
        lockGlidingRemainingTime = time;
    }

    public void UpdateGliding() {
        if (!CanGlide || !WantsToGlide) {
            IsGliding = false;
        }

        pressedMoveHorizontally = false;
        RunningTime += Time.deltaTime;
        if (!playedOpenSound && RunningTime > 0.15f && RunningTime < 0.2f) {
            Sound.Play(OpenParachuteSound.GetSound(null), PlatformMovement.Position, null);
            playedOpenSound = true;
        }

        if (!IsGliding) {
            Exit();
            return;
        }

        if (PlatformMovement.LocalSpeedY < -GlideSpeed) {
            PlatformMovement.LocalSpeedY = -GlideSpeed;
        }

        UpdateAnimations();
        if (pressedMoveHorizontally && !wasMovingHorizontally) {
            Sound.Play(TurnLeftRightSound.GetSound(null), PlatformMovement.Position, null);
        } else if (parachuteLoopLastSound == null) {
            parachuteLoopLastSound = Sound.Play(ParachuteLoopSound.GetSound(null), PlatformMovement.Position, delegate { parachuteLoopLastSound = null; });
            if (parachuteLoopLastSound) {
                parachuteLoopLastSound.AttachTo = PlatformMovement.transform;
            }
        }

        wasMovingHorizontally = pressedMoveHorizontally;
        HandleFloatZones();
    }

    private void UpdateAnimations() {
        if (ShouldGlideMovingAnimationPlay) {
            pressedMoveHorizontally = true;
            Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(MovingAnimation, 110, ShouldGlideMovingAnimationKeepPlaying);
        } else if (ShouldGlideIdleAnimationPlay) {
            Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(IdleAnimation, 110, ShouldGlideIdleAnimationKeepPlaying);
        }
    }

    public void HandleFloatZones() {
        for (var i = 0; i < FloatZone.All.Count; i++) {
            var floatZone = FloatZone.All[i];
            if (floatZone.BoundingRect.Contains(Sein.Position)) {
                var platformMovement = Sein.PlatformBehaviour.PlatformMovement;
                var b = Vector2.up * Sein.PlatformBehaviour.Gravity.CurrentSettings.GravityStrength * Time.deltaTime;
                platformMovement.LocalSpeed += b;
                var localSpeed = platformMovement.LocalSpeed;
                if (localSpeed.y < 0f) {
                    localSpeed.y = MoonMath.Float.ClampedAdd(localSpeed.y, floatZone.Deceleration * Time.deltaTime, 0f, 0f);
                }

                if (localSpeed.y >= 0f) {
                    localSpeed.y = MoonMath.Float.ClampedAdd(localSpeed.y, floatZone.Acceleration * Time.deltaTime, 0f, floatZone.DesiredSpeed);
                    localSpeed.y = MoonMath.Float.ClampedSubtract(localSpeed.y, floatZone.TooFastDeceleration * Time.deltaTime, 0f, floatZone.DesiredSpeed);
                }

                platformMovement.LocalSpeed = localSpeed;
                Sein.ResetAirLimits();
                return;
            }
        }

        if (RandomizerBonus.EnhancedGlide) {
            var platformMovement = Sein.PlatformBehaviour.PlatformMovement;
            var b = Vector2.up * Sein.PlatformBehaviour.Gravity.CurrentSettings.GravityStrength * Time.deltaTime;
            platformMovement.LocalSpeed += b;
            var localSpeed = platformMovement.LocalSpeed;
            if (localSpeed.y < 0f) {
                localSpeed.y = MoonMath.Float.ClampedAdd(localSpeed.y, 1000f * Time.deltaTime, 0f, 0f);
            }

            if (localSpeed.y >= 0f) {
                localSpeed.y = MoonMath.Float.ClampedAdd(localSpeed.y, 20f * Time.deltaTime, 0f, 10f);
                localSpeed.y = MoonMath.Float.ClampedSubtract(localSpeed.y, 1000f * Time.deltaTime, 0f, 10f);
            }

            platformMovement.LocalSpeed = localSpeed;
            Sein.ResetAirLimits();
        }
    }

    public override void UpdateCharacterState() {
        if (CharacterLeftRightMovement.HorizontalInput != 0f) {
            isMoveAnimation = 3;
        } else if (isMoveAnimation > 0) {
            isMoveAnimation--;
        }

        if (lockGlidingRemainingTime > 0f) {
            lockGlidingRemainingTime -= Time.deltaTime;
            if (lockGlidingRemainingTime < 0f) {
                lockGlidingRemainingTime = 0f;
            }
        }

        if (NeedsRightTriggerReleased && Input.Glide.Released) {
            NeedsRightTriggerReleased = false;
        }

        if (IsGliding) {
            UpdateGliding();
        } else if (CanGlide && WantsToGlide && Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY < 0f) {
            IsGliding = true;
        }
    }

    public bool CanEnter {
        get {
            var isGliding = IsGliding;
            if (isGliding) {
            }

            return isGliding;
        }
    }

    public float GlideOpeningTime => 0.5f;

    public bool ShouldGlideIdleAnimationPlay => ShouldGlideIdleAnimationKeepPlaying();

    public bool ShouldGlideMovingAnimationPlay => ShouldGlideMovingAnimationKeepPlaying();

    public bool ShouldGlideIdleAnimationKeepPlaying() {
        return IsGliding;
    }

    public bool ShouldGlideMovingAnimationKeepPlaying() {
        return IsGliding && isMoveAnimation > 0;
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        Sein.Abilities.Glide = this;
    }

    public SeinCharacter Sein;

    public TextureAnimationWithTransitions IdleAnimation;

    public TextureAnimationWithTransitions MovingAnimation;

    public SoundProvider OpenParachuteSound;

    public SoundProvider CloseParachuteSound;

    public SoundProvider ParachuteLoopSound;

    public SoundProvider TurnLeftRightSound;

    private SoundPlayer parachuteLoopLastSound;

    private bool playedOpenSound;

    private bool pressedMoveHorizontally;

    private bool wasMovingHorizontally;

    private bool isGliding;

    public bool NeedsRightTriggerReleased;

    private float lockGlidingRemainingTime;

    private int isMoveAnimation;

    public float RunningTime;

    public float GlideSpeed;

    public float GravityMultiplier = 0.5f;

    public HorizontalPlatformMovementSettings.SpeedMultiplierSet MoveSpeed;
}

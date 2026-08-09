using Core;
using UnityEngine;
using Input = Core.Input;

public class SeinGlide : CharacterState, ISeinReceiver {
    public CharacterGravity CharacterGravity {
        get { return Sein.PlatformBehaviour.Gravity; }
    }

    public CharacterLeftRightMovement CharacterLeftRightMovement {
        get { return Sein.PlatformBehaviour.LeftRightMovement; }
    }

    public PlatformMovement PlatformMovement {
        get { return Sein.PlatformBehaviour.PlatformMovement; }
    }

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
        get { return m_isGliding; }
        set {
            if (m_isGliding != value) {
                m_isGliding = value;
                if (m_isGliding) {
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
        if (m_parachuteLoopLastSound) {
            m_parachuteLoopLastSound.FadeOut(1f, true);
        }

        base.OnExit();
        if (RunningTime > 0.3f) {
            Sound.Play(CloseParachuteSound.GetSound(null), PlatformMovement.Position, null);
        }

        RunningTime = 0f;
        m_playedOpenSound = false;
    }

    public bool CanGlide {
        get { return !PlatformMovement.IsOnGround && !PlatformMovement.IsOnWall && !Sein.Controller.InputLocked && !SeinAbilityRestrictZone.IsInside(); }
    }

    public bool WantsToGlide {
        get { return Input.Glide.Pressed && !NeedsRightTriggerReleased && m_lockGlidingRemainingTime <= 0f; }
    }

    public void LockGliding(float time) {
        m_lockGlidingRemainingTime = time;
    }

    public void UpdateGliding() {
        if (!CanGlide || !WantsToGlide) {
            IsGliding = false;
        }

        m_pressedMoveHorizontally = false;
        RunningTime += Time.deltaTime;
        if (!m_playedOpenSound && RunningTime > 0.15f && RunningTime < 0.2f) {
            Sound.Play(OpenParachuteSound.GetSound(null), PlatformMovement.Position, null);
            m_playedOpenSound = true;
        }

        if (!IsGliding) {
            Exit();
            return;
        }

        if (PlatformMovement.LocalSpeedY < -GlideSpeed) {
            PlatformMovement.LocalSpeedY = -GlideSpeed;
        }

        UpdateAnimations();
        if (m_pressedMoveHorizontally && !m_wasMovingHorizantally) {
            Sound.Play(TurnLeftRightSound.GetSound(null), PlatformMovement.Position, null);
        } else if (m_parachuteLoopLastSound == null) {
            m_parachuteLoopLastSound = Sound.Play(ParachuteLoopSound.GetSound(null), PlatformMovement.Position, delegate { m_parachuteLoopLastSound = null; });
            if (m_parachuteLoopLastSound) {
                m_parachuteLoopLastSound.AttachTo = PlatformMovement.transform;
            }
        }

        m_wasMovingHorizantally = m_pressedMoveHorizontally;
        HandleFloatZones();
    }

    private void UpdateAnimations() {
        if (ShouldGlideMovingAnimationPlay) {
            m_pressedMoveHorizontally = true;
            Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(MovingAnimation, 110, ShouldGlideMovingAnimationKeepPlaying);
        } else if (ShouldGlideIdleAnimationPlay) {
            Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(IdleAnimation, 110, ShouldGlideIdleAnimationKeepPlaying);
        }
    }

    public void HandleFloatZones() {
        for (int i = 0; i < FloatZone.All.Count; i++) {
            FloatZone floatZone = FloatZone.All[i];
            if (floatZone.BoundingRect.Contains(Sein.Position)) {
                PlatformMovement platformMovement = Sein.PlatformBehaviour.PlatformMovement;
                Vector2 b = Vector2.up * Sein.PlatformBehaviour.Gravity.CurrentSettings.GravityStrength * Time.deltaTime;
                platformMovement.LocalSpeed += b;
                Vector2 localSpeed = platformMovement.LocalSpeed;
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
            PlatformMovement platformMovement = Sein.PlatformBehaviour.PlatformMovement;
            Vector2 b = Vector2.up * Sein.PlatformBehaviour.Gravity.CurrentSettings.GravityStrength * Time.deltaTime;
            platformMovement.LocalSpeed += b;
            Vector2 localSpeed = platformMovement.LocalSpeed;
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
            m_isMoveAnimation = 3;
        } else if (m_isMoveAnimation > 0) {
            m_isMoveAnimation--;
        }

        if (m_lockGlidingRemainingTime > 0f) {
            m_lockGlidingRemainingTime -= Time.deltaTime;
            if (m_lockGlidingRemainingTime < 0f) {
                m_lockGlidingRemainingTime = 0f;
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
            bool isGliding = IsGliding;
            if (isGliding) {
            }

            return isGliding;
        }
    }

    public float GlideOpeningTime {
        get { return 0.5f; }
    }

    public bool ShouldGlideIdleAnimationPlay {
        get { return ShouldGlideIdleAnimationKeepPlaying(); }
    }

    public bool ShouldGlideMovingAnimationPlay {
        get { return ShouldGlideMovingAnimationKeepPlaying(); }
    }

    public bool ShouldGlideIdleAnimationKeepPlaying() {
        return IsGliding;
    }

    public bool ShouldGlideMovingAnimationKeepPlaying() {
        return IsGliding && m_isMoveAnimation > 0;
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

    private SoundPlayer m_parachuteLoopLastSound;

    private bool m_playedOpenSound;

    private bool m_pressedMoveHorizontally;

    private bool m_wasMovingHorizantally;

    private bool m_isGliding;

    public bool NeedsRightTriggerReleased;

    private float m_lockGlidingRemainingTime;

    private int m_isMoveAnimation;

    public float RunningTime;

    public int Level;

    public float MinHeightToGlide = 2f;

    public float GlideSpeed;

    public float GravityMultiplier = 0.5f;

    public HorizontalPlatformMovementSettings.SpeedMultiplierSet MoveSpeed;
}

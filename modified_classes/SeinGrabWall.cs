using System;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinGrabWall : CharacterState, ISeinReceiver {
    public CharacterGravity CharacterGravity => Sein.PlatformBehaviour.Gravity;

    public CharacterLeftRightMovement CharacterLeftRightMovement => Sein.PlatformBehaviour.LeftRightMovement;

    public PlatformMovementListOfColliders ListOfCollidedObjects => Sein.PlatformBehaviour.PlatformMovementListOfColliders;

    public PlatformMovement PlatformMovement => Sein.PlatformBehaviour.PlatformMovement;

    public TextureAnimationWithTransitions PickAwayAnimation() {
        if (Sein.PlayerAbilities.ChargeJump.HasAbility) {
            TextureAnimationWithTransitions[] away = GrabWallAnimation.Away;
            float angularElevation = Sein.Abilities.WallChargeJump.AngularElevation;
            int num = (int)Mathf.Clamp(Mathf.InverseLerp(-45f, 45f, angularElevation) * away.Length, 0f, away.Length - 1);
            return away[num];
        }

        return GrabWallAnimation.GrabAway;
    }

    public void Start() {
        CharacterGravity.ModifyGravityPlatformMovementSettingsEvent += ModifyGravityPlatformMovementSettings;
        CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent += ModifyHorizontalPlatformMovementSettings;
    }

    public new void OnDestroy() {
        base.OnDestroy();
        CharacterGravity.ModifyGravityPlatformMovementSettingsEvent -= ModifyGravityPlatformMovementSettings;
        CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent -= ModifyHorizontalPlatformMovementSettings;
    }

    public void ModifyGravityPlatformMovementSettings(GravityPlatformMovementSettings settings) {
        if (IsGrabbing) {
            settings.GravityStrength = 0f;
        }
    }

    public bool IsNotMoving => PlatformMovement.LocalSpeedY == 0f;

    public void ModifyHorizontalPlatformMovementSettings(HorizontalPlatformMovementSettings settings) {
        if (IsGrabbing && !PlatformMovement.IsOnGround && !PlatformMovement.IsOnCeiling) {
            settings.LockInput = true;
        }
    }

    public override void OnExit() {
        IsGrabbing = false;
        if (m_climbDownSoundPlayer) {
            m_climbDownSoundPlayer.FadeOut(0.3f, true);
            UberPoolManager.Instance.RemoveOnDestroyed(m_climbDownSoundPlayer.gameObject);
            m_climbDownSoundPlayer = null;
        }

        base.OnExit();
    }

    public void OnGrabWall() {
        if (GameController.Instance.GameTime > m_lastWallGrabEnterSoundTime + m_minimumSoundDelay) {
            Sound.Play(WallGrabEnterSound.GetSoundForMaterial(Sein.PlatformBehaviour.WallSurfaceMaterialType, null), PlatformMovement.Position, null);
            m_lastWallGrabEnterSoundTime = GameController.Instance.GameTime;
        }
    }

    public void OnReleaseWall() {
        Sein.Abilities.WallSlide.ResetMovingOffWallLockTimer();
        if (GameController.Instance.GameTime > m_lastWallGrabExitSoundTime + m_minimumSoundDelay) {
            Sound.Play(WallGrabExitSound.GetSoundForMaterial(Sein.PlatformBehaviour.WallSurfaceMaterialType, null), PlatformMovement.Position, null);
            m_lastWallGrabExitSoundTime = GameController.Instance.GameTime;
        }

        if (m_climbDownSoundPlayer) {
            m_climbDownSoundPlayer.FadeOut(0f, true);
            UberPoolManager.Instance.RemoveOnDestroyed(m_climbDownSoundPlayer.gameObject);
            m_climbDownSoundPlayer = null;
        }
    }

    public bool IsGrabbing {
        get => m_isGrabbing;
        set {
            if (m_isGrabbing != value) {
                m_isGrabbing = value;
                if (m_isGrabbing) {
                    OnGrabWall();
                } else {
                    OnReleaseWall();
                }
            }
        }
    }

    public void UpdateGrabbing() {
        if (IsGrabbing && Sein.Controller.CanMove && Sein.Input.Up.Pressed && !PlatformMovement.HeadAgainstWall) {
            if (Sein.Abilities.Glide) {
                Sein.Abilities.Glide.NeedsRightTriggerReleased = true;
            }

            float climbClamberFactor = RandomizerSettings.Controls.SlowClimbVault ? 0.225f : 0.65f;
            Sein.Abilities.EdgeClamber.PerformEdgeClamber(climbClamberFactor);
        }

        if (!CanGrab) {
            if (Randomizer.DoesGrabForgivenessExpire(Time.deltaTime)) {
                IsGrabbing = false;
                return;
            }
        }

        if (!WantToGrab) {
            IsGrabbing = false;
            return;
        }

        if (ForceGrabReleaseZone.InsideZone(Sein.Position)) {
            m_requiresRelease = true;
        }

        Vector2 localSpeed = PlatformMovement.LocalSpeed;
        if (Characters.Sein.Controller.CanMove) {
            if (LockVerticalMovement || Sein.Controller.IsChargingJump) {
                localSpeed.y = 0f;
            } else if (Sein.Input.Up.Pressed) {
                localSpeed.y = Mathf.Clamp(localSpeed.y + Acceleration * Time.deltaTime, 0f, ClimbSpeedUp * RandomizerBonus.Veloscale);
            } else if (Sein.Input.Down.Pressed) {
                localSpeed.y = Mathf.Clamp(localSpeed.y - Acceleration * Time.deltaTime, -ClimbSpeedDown * RandomizerBonus.Veloscale, 0f);
            } else {
                localSpeed.y = 0f;
            }
        }

        HandleWallClimbUpSteps();
        HandleWallClimbDownSteps();
        PlatformMovement.LocalSpeed = localSpeed;
        if (ShouldGrabWallUpAnimationPlay) {
            Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(GrabWallAnimation.ClimbUp, 25, ShouldGrabWallUpAnimationKeepPlaying);
        }

        if (ShouldGrabWallDownAnimationPlay) {
            Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(GrabWallAnimation.ClimbDown, 25, ShouldGrabWallDownAnimationKeepPlaying);
        }

        if (ShouldGrabWallAwayAnimationPlay) {
            Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(PickAwayAnimation(), 25, ShouldGrabWallAwayAnimationKeepPlaying, true);
        }

        if (ShouldGrabWallIdleAnimationPlay) {
            Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(GrabWallAnimation.Idle, 25, ShouldGrabWallIdleAnimationKeepPlaying);
        }

        m_currentTime += Time.deltaTime;
    }

    public override void UpdateCharacterState() {
        if (IsGrabbing) {
            UpdateGrabbing();
        } else if (WantToGrab) {
            if (!Sein.Abilities.WallSlide.IsWallSliding) {
                m_requiresRelease = false;
            }

            if (CanGrab) {
                Randomizer.ApplyGrabForgiveness();
                IsGrabbing = true;
            }
        } else {
            m_requiresRelease = false;
        }
    }

    public bool WantToGrab => RandomizerSettings.Controls.InvertClimb ^ Input.Glide.Pressed;

    public bool CanGrab => Sein.Abilities.WallSlide.IsOnWall && (Sein.PlatformBehaviour.PlatformMovement.HasWallLeft || !Sein.Controller.FaceLeft) && (Sein.PlatformBehaviour.PlatformMovement.HasWallRight || Sein.Controller.FaceLeft) && !m_requiresRelease && !SeinAbilityRestrictZone.IsInside() && PlatformMovement.HeadAgainstWall;

    public bool ShouldGrabWallUpAnimationPlay => ShouldGrabWallUpAnimationKeepPlaying();

    public bool ShouldGrabWallDownAnimationPlay => ShouldGrabWallDownAnimationKeepPlaying();

    public bool ShouldGrabWallAwayAnimationPlay => ShouldGrabWallAwayAnimationKeepPlaying();

    public bool ShouldGrabWallIdleAnimationPlay => ShouldGrabWallIdleAnimationKeepPlaying();

    public bool ShouldGrabWallUpAnimationKeepPlaying() {
        return IsGrabbing && PlatformMovement.LocalSpeedY > 0f;
    }

    public bool ShouldGrabWallDownAnimationKeepPlaying() {
        return IsGrabbing && PlatformMovement.LocalSpeedY < 0f;
    }

    public bool ShouldGrabWallAwayAnimationKeepPlaying() {
        return IsGrabbing && PlatformMovement.LocalSpeedY == 0f && IsGrabbingAway;
    }

    public bool ShouldGrabWallIdleAnimationKeepPlaying() {
        return IsGrabbing && PlatformMovement.LocalSpeedY == 0f && !IsGrabbingAway;
    }

    public bool IsGrabbingAway => (Sein.Input.NormalizedHorizontal == -1 && PlatformMovement.HasWallRight) || (Sein.Input.NormalizedHorizontal == 1 && PlatformMovement.HasWallLeft);

    public void HandleWallClimbUpSteps() {
        if (PlatformMovement.LocalSpeedY > 0f && m_nextWallClimbUpTime < m_currentTime) {
            Sound.Play(WallGrabStepUpSound.GetSoundForMaterial(Sein.PlatformBehaviour.WallSurfaceMaterialType, null), PlatformMovement.Position, null);
            m_nextWallClimbUpTime = m_currentTime + 1f / WallClimbUpStepsPerSecond;
        }
    }

    public void HandleWallClimbDownSteps() {
        if (PlatformMovement.LocalSpeedY < 0f) {
            if (InstantiateUtility.IsDestroyed(m_climbDownSoundPlayer) && GameController.Instance.GameTime > m_lastWallGrabStepDownSoundTime + m_minimumSoundDelay) {
                m_climbDownSoundPlayer = Sound.PlayLooping(WallGrabStepDownSound.GetSoundForMaterial(Sein.PlatformBehaviour.WallSurfaceMaterialType, null), PlatformMovement.Position, delegate { m_climbDownSoundPlayer = null; });
                m_lastWallGrabStepDownSoundTime = GameController.Instance.GameTime;
            }
        } else if (!InstantiateUtility.IsDestroyed(m_climbDownSoundPlayer)) {
            m_climbDownSoundPlayer.FadeOut(0.3f, true);
            UberPoolManager.Instance.RemoveOnDestroyed(m_climbDownSoundPlayer.gameObject);
            m_climbDownSoundPlayer = null;
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        Sein.Abilities.GrabWall = this;
    }

    public SeinCharacter Sein;

    public float WallClimbUpStepsPerSecond;

    public float WallClimbDownStepsPerSecond;

    public SurfaceToSoundProviderMap WallGrabEnterSound;

    public SurfaceToSoundProviderMap WallGrabExitSound;

    public SurfaceToSoundProviderMap WallGrabStepUpSound;

    public SurfaceToSoundProviderMap WallGrabStepDownSound;

    private float m_minimumSoundDelay = 0.4f;

    private float m_lastWallGrabEnterSoundTime;

    private float m_lastWallGrabExitSoundTime;

    private float m_lastWallGrabStepDownSoundTime = -10f;

    public bool LockVerticalMovement;

    public GrabWallAnimationSet GrabWallAnimation;

    public TextureAnimationWithTransitions EdgeClimbAnimation;

    public float ClimbSpeedUp;

    public float ClimbSpeedDown;

    public float Acceleration = 60f;

    private float m_currentTime;

    private bool m_isGrabbing;

    private float m_nextWallClimbUpTime;

    private bool m_requiresRelease;

    private SoundPlayer m_climbDownSoundPlayer;

    [Serializable]
    public class GrabWallAnimationSet {
        public TextureAnimationWithTransitions Idle;

        public TextureAnimationWithTransitions ClimbUp;

        public TextureAnimationWithTransitions ClimbDown;

        public TextureAnimationWithTransitions[] Away;

        public TextureAnimationWithTransitions GrabAway;
    }
}

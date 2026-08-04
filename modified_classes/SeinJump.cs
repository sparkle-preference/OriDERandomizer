using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinJump : CharacterState, ISeinReceiver {
    public event Action<float> OnJumpEvent = delegate { };

    public bool CanJump => enabled && Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY <= 0.0001f && m_timeWeCanJumpRemaining > 0f && !Sein.PlatformBehaviour.PlatformMovement.Ceiling.IsOn && !SeinAbilityRestrictZone.IsInside();

    public PlatformMovement PlatformMovement => Sein.PlatformBehaviour.PlatformMovement;

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

    public CharacterSpriteMirror CharacterSpriteMirror => Sein.PlatformBehaviour.Visuals.SpriteMirror;

    public bool HasSharplyTurnedAround => (m_timeSinceMovingRight > 0f && m_timeSinceMovingRight < 0.2f && PlatformMovement.LocalSpeedX < 0f) || (m_timeSinceMovingLeft > 0f && m_timeSinceMovingLeft < 0.2f && PlatformMovement.LocalSpeedX > 0f);

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        Sein.Abilities.Jump = this;
    }

    public override void UpdateCharacterState() {
        if (m_timeWeCanJumpRemaining > 0f) {
            m_timeWeCanJumpRemaining -= Time.deltaTime;
        }

        if (Sein.PlatformBehaviour.PlatformMovement.Ground.IsOn) {
            m_timeWeCanJumpRemaining = DurationSinceLastOnGroundThatWeCanStillJump;
        } else {
            m_bunnyHopTimeRemaining = 0.2f;
        }

        if (m_bunnyHopTimeRemaining > 0f) {
            m_bunnyHopTimeRemaining -= Time.deltaTime;
            if (m_bunnyHopTimeRemaining < 0f) {
                ResetRunningJumpCount();
            }
        }

        if (!PlatformMovement.MovingHorizontally && PlatformMovement.IsOnGround) {
            ResetRunningJumpCount();
        }

        if (PlatformMovement.MovingHorizontally && PlatformMovement.IsOnGround) {
            ResetJumpIdleCount();
        }

        UpdateTimeSinceFacing();
    }

    public void ResetRunningJumpCount() {
        m_runningJumpNumber = 0;
    }

    public void ResetJumpIdleCount() {
        m_jumpIdleNumber = 0;
    }

    public float CalculateSpeedFromHeight(float height) {
        return PhysicsHelper.CalculateSpeedFromHeight(height * RandomizerBonus.Jumpscale, Sein.PlatformBehaviour.Gravity.BaseSettings.GravityStrength);
    }

    public void PerformTurnAroundBackFlipJump() {
        PlatformMovement.LocalSpeedY = CalculateSpeedFromHeight(BackflipJumpHeight);
        Sein.PlatformBehaviour.AirNoDeceleration.NoDeceleration = true;
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(PlatformMovement.LocalSpeedY * 0.5f, 1f);
        }

        var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(BackflipAnimation, 10, ShouldBackflipAnimationKeepPlaying);
        characterAnimationState.OnStartPlaying = OnAnimationStart;
        characterAnimationState.OnStopPlaying = OnAnimationEnd;
    }

    public void PerformJump() {
        m_currentJumpingMaterial = SurfaceToSoundProviderMap.ColliderMaterialToSurfaceMaterialType(Sein.PlatformBehaviour.PlatformMovementListOfColliders.GroundCollider);
        if (Sein.Controller.IsCrouching) {
            PerformCrouchJump();
            Sound.Play(JumpSoundProvider.GetSoundForMaterial(m_currentJumpingMaterial, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        } else if (HasSharplyTurnedAround) {
            PerformTurnAroundBackFlipJump();
            Sound.Play(FlipJumpSoundProvider.GetSoundForMaterial(m_currentJumpingMaterial, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        } else if (Sein.PlatformBehaviour.LeftRightMovement.HorizontalInput == 0f || PlatformMovement.IsOnWall) {
            if (PlatformMovement.IsOnWall && Sein.PlayerAbilities.WallJump.HasAbility && Sein.Abilities.WallSlide.IsOnWall) {
                PerformWallSlideJump();
                Sound.Play(JumpSoundProvider.GetSoundForMaterial(m_currentJumpingMaterial, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
            } else {
                PerformIdleJump();
            }
        } else {
            PerformRunningJump();
        }

        var gameObject = (GameObject)InstantiateUtility.Instantiate(JumpParticleEffect, Sein.PlatformBehaviour.PlatformMovement.FeetPosition, Quaternion.identity);
        gameObject.transform.eulerAngles = new Vector3(0f, 0f, MoonMath.Angle.AngleFromVector(-Sein.PlatformBehaviour.PlatformMovement.LocalSpeed));
        Sein.PlatformBehaviour.Force.ApplyGroundForce(Vector3.down * JumpImpulse, ForceMode.Impulse);
        OnJumpEvent(PlatformMovement.LocalSpeedY);
        JumpFlipPlatform.OnSeinJumpEvent();
        m_timeWeCanJumpRemaining = 0f;
    }

    public void PerformRunningJump() {
        switch (m_runningJumpNumber) {
            case 0:
                PerformFirstRunningJump();
                break;
            case 1:
                PerformSecondRunningJump();
                break;
            case 2:
                PerformThirdRunningJump();
                break;
        }
    }

    private void CacheDelegates() {
        if (m_shouldJumpMoving == null) {
            m_shouldJumpMoving = ShouldJumpMovingAnimationKeepPlaying;
        }

        if (onAnimationEnd == null) {
            onAnimationEnd = OnAnimationEnd;
        }
    }

    public void PerformFirstRunningJump() {
        var localSpeed = PlatformMovement.LocalSpeed;
        localSpeed.y = CalculateSpeedFromHeight(FirstJumpHeight);
        PlatformMovement.LocalSpeed = localSpeed;
        CacheDelegates();
        var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(JumpAnimation[0], 10, m_shouldJumpMoving);
        characterAnimationState.OnStopPlaying = onAnimationEnd;
        characterAnimationState.OnStartPlaying = null;
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(PlatformMovement.LocalSpeedY, 1f);
        }

        Sound.Play(JumpSoundProvider.GetSoundForMaterial(m_currentJumpingMaterial, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        m_runningJumpNumber++;
    }

    public void PerformSecondRunningJump() {
        var localSpeed = PlatformMovement.LocalSpeed;
        localSpeed.y = CalculateSpeedFromHeight(m_runningJumpNumber != 0 ? SecondJumpHeight : FirstJumpHeight);
        PlatformMovement.LocalSpeed = localSpeed;
        CacheDelegates();
        var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(JumpAnimation[1], 10, m_shouldJumpMoving);
        characterAnimationState.OnStopPlaying = onAnimationEnd;
        characterAnimationState.OnStartPlaying = null;
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(PlatformMovement.LocalSpeedY, 1f);
        }

        Sound.Play(JumpSoundProvider.GetSoundForMaterial(m_currentJumpingMaterial, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        m_runningJumpNumber++;
    }

    public void PerformThirdRunningJump() {
        var localSpeed = PlatformMovement.LocalSpeed;
        localSpeed.y = CalculateSpeedFromHeight(ThirdJumpHeight);
        PlatformMovement.LocalSpeed = localSpeed;
        CacheDelegates();
        var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(JumpAnimation[2], 10, m_shouldJumpMoving);
        characterAnimationState.OnStartPlaying = null;
        characterAnimationState.OnStopPlaying = onAnimationEnd;
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(PlatformMovement.LocalSpeedY * 0.5f, 1f);
        }

        Sound.Play(SpinJumpSoundProvider.GetSoundForMaterial(m_currentJumpingMaterial, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        m_runningJumpNumber = 0;
    }

    private void PerformIdleJump() {
        switch (m_jumpIdleNumber) {
            case 0:
                PerformFirstIdleJump();
                break;
            case 1:
                PerformSecondIdleJump();
                break;
            case 2:
                PerformThirdIldleJump();
                break;
        }
    }

    public void PerformFirstIdleJump() {
        var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(JumpIdleAnimation[0], 10, ShouldJumpIdleAnimationKeepPlaying);
        characterAnimationState.OnStartPlaying = null;
        characterAnimationState.OnStopPlaying = OnAnimationEnd;
        PlatformMovement.LocalSpeedY = CalculateSpeedFromHeight(FirstJumpHeight);
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(PlatformMovement.LocalSpeedY, 1f);
        }

        Sound.Play(JumpSoundProvider.GetSoundForMaterial(m_currentJumpingMaterial, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        m_jumpIdleNumber++;
    }

    public void PerformSecondIdleJump() {
        var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(JumpIdleAnimation[1], 10, ShouldJumpIdleAnimationKeepPlaying);
        characterAnimationState.OnStartPlaying = null;
        characterAnimationState.OnStopPlaying = OnAnimationEnd;
        PlatformMovement.LocalSpeedY = CalculateSpeedFromHeight(SecondJumpHeight);
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(PlatformMovement.LocalSpeedY, 1f);
        }

        Sound.Play(JumpSoundProvider.GetSoundForMaterial(m_currentJumpingMaterial, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        m_jumpIdleNumber++;
    }

    private void PerformThirdIldleJump() {
        var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(JumpIdleAnimation[2], 10, ShouldJumpIdleAnimationKeepPlaying);
        characterAnimationState.OnStartPlaying = null;
        characterAnimationState.OnStopPlaying = OnAnimationEnd;
        PlatformMovement.LocalSpeedY = CalculateSpeedFromHeight(ThirdJumpHeight);
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(PlatformMovement.LocalSpeedY, 1f);
        }

        Sound.Play(SpinJumpSoundProvider.GetSoundForMaterial(m_currentJumpingMaterial, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        m_jumpIdleNumber = 0;
    }

    private void PerformWallSlideJump() {
        var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(WallSlideJumpAnimation, 24, ShouldWallSlideJumpAnimationKeepPlaying);
        characterAnimationState.OnStartPlaying = null;
        characterAnimationState.OnStopPlaying = OnAnimationEnd;
        PlatformMovement.LocalSpeedY = CalculateSpeedFromHeight(FirstJumpHeight);
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(PlatformMovement.LocalSpeedY, 1f);
        }
    }

    private void PerformCrouchJump() {
        var flag = false;
        var groundColliders = Sein.PlatformBehaviour.PlatformMovementListOfColliders.GroundColliders;
        for (var i = 0; i < groundColliders.Count; i++) {
            var component = groundColliders[i];
            if (component.GetComponentInParents<GoThroughPlatform>() && Sein.GetComponent<GoThroughPlatformHandler>().FallThroughPlatform()) {
                Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX = 0f;
                Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY = 0f;
                Sein.PlatformBehaviour.PlatformMovement.Ground.FutureOn = false;
                Sein.PlatformBehaviour.PlatformMovement.Ground.IsOn = false;
                Sein.PlatformBehaviour.PlatformMovement.Ground.WasOn = false;
                flag = true;
            }
        }

        if (!flag) {
            PlatformMovement.LocalSpeedY = CalculateSpeedFromHeight(CrouchJumpHeight);
            PlatformMovement.LocalSpeedX = !CharacterSpriteMirror.FaceLeft ? -3 : 3;
            Sein.PlatformBehaviour.AirNoDeceleration.NoDeceleration = true;
            var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(CrouchJumpAnimation, 10, ShouldBackflipAnimationKeepPlaying);
            characterAnimationState.OnStartPlaying = OnAnimationStart;
            characterAnimationState.OnStopPlaying = OnAnimationEnd;
        }
    }

    public bool ShouldBackflipAnimationKeepPlaying() {
        return Sein.PlatformBehaviour.PlatformMovement.IsInAir;
    }

    public bool ShouldJumpIdleAnimationKeepPlaying() {
        return Sein.PlatformBehaviour.PlatformMovement.IsInAir && (!Characters.Sein.Controller.CanMove || Input.NormalizedHorizontal == 0 || PlatformMovement.IsOnWall);
    }

    public bool ShouldWallSlideJumpAnimationKeepPlaying() {
        return PlatformMovement.IsOnWall && PlatformMovement.IsInAir && PlatformMovement.Jumping && PlatformMovement.HeadAgainstWall && PlatformMovement.FeetAgainstWall;
    }

    public bool ShouldJumpMovingAnimationKeepPlaying() {
        return Sein.PlatformBehaviour.PlatformMovement.IsInAir && (!Characters.Sein.Controller.CanMove || (Sein.PlatformBehaviour.LeftRightMovement.HorizontalInput != 0f && (!PlatformMovement.IsOnWall || !PlatformMovement.HeadAgainstWall)));
    }

    public bool ShouldThirdJumpMovingAnimationKeepPlaying() {
        return Sein.PlatformBehaviour.PlatformMovement.IsInAir;
    }

    public void UpdateTimeSinceFacing() {
        m_timeSinceMovingLeft += Time.deltaTime;
        m_timeSinceMovingRight += Time.deltaTime;
        if (PlatformMovement.LocalSpeedX < 0f) {
            m_timeSinceMovingLeft = 0f;
        }

        if (PlatformMovement.LocalSpeedX > 0f) {
            m_timeSinceMovingRight = 0f;
        }
    }

    public void OnAnimationEnd() {
        SpriteMirrorLock = false;
    }

    public void OnAnimationStart() {
        SpriteMirrorLock = true;
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref m_bunnyHopTimeRemaining);
        ar.Serialize(ref m_jumpIdleNumber);
        ar.Serialize(ref m_runningJumpNumber);
        ar.Serialize(ref m_spriteMirrorLock);
        ar.Serialize(ref m_timeSinceMovingLeft);
        ar.Serialize(ref m_timeSinceMovingRight);
        ar.Serialize(ref m_timeWeCanJumpRemaining);
    }

    public override void Awake() {
        base.Awake();
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
    }

    public override void OnDestroy() {
        base.OnDestroy();
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
    }

    public void OnRestoreCheckpoint() {
        m_spriteMirrorLock = false;
    }

    public TextureAnimationWithTransitions BackflipAnimation;

    public float BackflipJumpHeight = 3f;

    public TextureAnimationWithTransitions CrouchJumpAnimation;

    public float CrouchJumpHeight = 4.5f;

    public float DurationSinceLastOnGroundThatWeCanStillJump = 0.2f;

    public float FirstJumpHeight = 3f;

    public TextureAnimationWithTransitions[] JumpAnimation;

    public TextureAnimationWithTransitions[] JumpIdleAnimation;

    public float JumpIdleHeight = 3f;

    public float JumpImpulse;

    public GameObject JumpParticleEffect;

    public SurfaceToSoundProviderMap JumpSoundProvider;

    public SurfaceToSoundProviderMap FlipJumpSoundProvider;

    public SurfaceToSoundProviderMap SpinJumpSoundProvider;

    private SurfaceMaterialType m_currentJumpingMaterial;

    public float SecondJumpHeight = 3.75f;

    public SeinCharacter Sein;

    public float ThirdJumpHeight = 4.5f;

    public TextureAnimationWithTransitions WallSlideJumpAnimation;

    private float m_bunnyHopTimeRemaining;

    private int m_jumpIdleNumber;

    private int m_runningJumpNumber;

    private bool m_spriteMirrorLock;

    private float m_timeSinceMovingLeft;

    private float m_timeSinceMovingRight;

    private float m_timeWeCanJumpRemaining;

    private Func<bool> m_shouldJumpMoving;

    private Action onAnimationEnd;
}

using System;
using System.Collections;
using Core;
using UnityEngine;
using Input = Core.Input;

public class SeinWallJump : CharacterState, ISeinReceiver {
    public event Action<Vector2> OnWallJumpEvent = delegate { };

    public PlatformMovement PlatformMovement {
        get { return Sein.PlatformBehaviour.PlatformMovement; }
    }

    public SeinDoubleJump DoubleJump {
        get { return Sein.Abilities.DoubleJump; }
    }

    public CharacterLeftRightMovement LeftRightMovement {
        get { return Sein.PlatformBehaviour.LeftRightMovement; }
    }

    public CharacterSpriteMirror CharacterSpriteMirror {
        get { return Sein.PlatformBehaviour.Visuals.SpriteMirror; }
    }

    public bool CanPerformWallJump {
        get { return enabled && Sein.Abilities.WallSlide.IsOnWall && !PlatformMovement.IsOnGround && Sein.PlayerAbilities.WallJump.HasAbility; }
    }

    public bool SpriteMirrorLock {
        get { return m_spriteMirrorLock; }
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

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        Sein.Abilities.WallJump = this;
    }

    public void PerformWallJump() {
        if (PlatformMovement.HasWallLeft) {
            PerformWallJumpRight();
        }

        if (PlatformMovement.HasWallRight) {
            PerformWallJumpLeft();
        }
    }

    public void PerformWallJumpLeft() {
        if (m_hasWallJumpedLeft) {
            return;
        }

        if (DontAllowJumpingTowardsWall && LeftRightMovement.BaseHorizontalInput > 0f) {
            return;
        }

        if (LeftRightMovement.BaseHorizontalInput > 0f && DoubleJump) {
            DoubleJump.LockForDuration(LockDoubleJumpTowardsDuration);
        }

        if (LimitWallJumping) {
            m_hasWallJumpedLeft = true;
        }

        m_hasWallJumpedRight = false;
        PlatformMovement.LocalSpeedX = -JumpStrength.x * RandomizerBonus.Jumpscale;
        PlatformMovement.LocalSpeedY = JumpStrength.y * RandomizerBonus.Jumpscale;
        Vector2 localSpeed = PlatformMovement.LocalSpeed;
        ApplyImpulseToWall(localSpeed);
        if (Sein.Input.NormalizedHorizontal < 0) {
            CharacterSpriteMirror.FaceLeft = true;
            CharacterAnimationSystem.CharacterAnimationState characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(AwayAnimation, 10, ShouldKeepPlayingWallJumpLeftAwayAnimation);
            characterAnimationState.OnStopPlaying = OnAnimationEnd;
            characterAnimationState.OnStartPlaying = OnAnimationStart;
        } else if (Sein.Input.NormalizedHorizontal > 0) {
            Vector3 origin = PlatformMovement.Position2D + PlatformMovement.LocalToWorld(Vector3.up * 2f);
            float maxDistance = PlatformMovement.CapsuleCollider.radius + 2f;
            Ray ray = new Ray(origin, PlatformMovement.LocalToWorld(Vector3.right));
            if (Physics.Raycast(ray, maxDistance)) {
                CharacterAnimationSystem.CharacterAnimationState characterAnimationState2 = Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(TowardsAnimation, 10, ShouldKeepPlayingWallJumpLeftTowardsAnimation);
                characterAnimationState2.OnStopPlaying = OnAnimationEnd;
                StartCoroutine(RoutineForMegWhoPlaysMarioAndSucksAtWallJumping());
            } else {
                CharacterAnimationSystem.CharacterAnimationState characterAnimationState3 = Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(EdgeJumpAnimation, 10, ShouldKeepPlayingWallJumpLeftTowardsAnimation);
                characterAnimationState3.OnStopPlaying = OnAnimationEnd;
                localSpeed.y = 0f;
            }
        } else {
            CharacterAnimationSystem.CharacterAnimationState characterAnimationState4 = Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(RegularAnimation, 10, ShouldKeepPlayingWallJumpLeftRegularAnimation);
            characterAnimationState4.OnStopPlaying = OnAnimationEnd;
            characterAnimationState4.OnStartPlaying = OnAnimationStart;
        }

        Sound.Play(WallJumpSound.GetSoundForMaterial(Sein.PlatformBehaviour.WallSurfaceMaterialType, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        OnWallJumpEvent(localSpeed);
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(localSpeed.y, 1f);
        }

        Sein.PlatformBehaviour.AirNoDeceleration.NoDeceleration = true;
        Sein.ResetAirLimits();
        JumpFlipPlatform.OnSeinWallJumpEvent();
    }

    public IEnumerator RoutineForMegWhoPlaysMarioAndSucksAtWallJumping() {
        float i = Sein.Input.NormalizedHorizontal;
        bool left = i < 0f;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        for (float t = 0f; t < 0.2f; t += Time.deltaTime) {
            if (Input.Jump.OnPressed) {
                break;
            }

            if (PlatformMovement.IsOnWall) {
                break;
            }

            if (Sein.Input.NormalizedHorizontal == -i) {
                PlatformMovement.LocalSpeedX = JumpStrength.x * RandomizerBonus.Jumpscale * ((!left) ? -1 : 1);
                CharacterSpriteMirror.FaceLeft = !left;
                CharacterAnimationSystem.CharacterAnimationState state = Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(AwayAnimation, 10, ShouldKeepPlayingWallJumpLeftAwayAnimation);
                state.OnStopPlaying = OnAnimationEnd;
                state.OnStartPlaying = OnAnimationStart;
                if (DoubleJump) {
                    DoubleJump.ResetLock();
                }

                break;
            }

            yield return new WaitForFixedUpdate();
        }
    }

    public void OnAnimationEnd() {
        SpriteMirrorLock = false;
    }

    public void OnAnimationStart() {
        SpriteMirrorLock = true;
    }

    public bool ShouldKeepPlayingWallJumpLeftTowardsAnimation() {
        return LeftRightMovement.HorizontalInput >= 0f && PlatformMovement.IsInAir && !PlatformMovement.IsOnCeiling && (!PlatformMovement.IsOnWall || !PlatformMovement.HeadAndFeetAgainstWall);
    }

    public bool ShouldKeepPlayingWallJumpLeftAwayAnimation() {
        return PlatformMovement.IsInAir && !PlatformMovement.IsOnCeiling && (!PlatformMovement.IsOnWall || !PlatformMovement.HeadAndFeetAgainstWall);
    }

    public bool ShouldKeepPlayingWallJumpLeftRegularAnimation() {
        return PlatformMovement.IsInAir && !PlatformMovement.IsOnCeiling && (!PlatformMovement.IsOnWall || !PlatformMovement.HeadAndFeetAgainstWall);
    }

    public override void Awake() {
        base.Awake();
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
    }

    public override void OnDestroy() {
        base.OnDestroy();
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
    }

    public void PerformWallJumpRight() {
        if (m_hasWallJumpedRight) {
            return;
        }

        if (DontAllowJumpingTowardsWall && LeftRightMovement.BaseHorizontalInput < 0f) {
            return;
        }

        if (LeftRightMovement.BaseHorizontalInput < 0f && DoubleJump) {
            DoubleJump.LockForDuration(LockDoubleJumpTowardsDuration);
        }

        if (LimitWallJumping) {
            m_hasWallJumpedRight = true;
        }

        m_hasWallJumpedLeft = false;
        PlatformMovement.LocalSpeedX = JumpStrength.x * RandomizerBonus.Jumpscale;
        PlatformMovement.LocalSpeedY = JumpStrength.y * RandomizerBonus.Jumpscale;
        Vector2 localSpeed = PlatformMovement.LocalSpeed;
        ApplyImpulseToWall(localSpeed);
        if (Sein.Input.NormalizedHorizontal > 0) {
            CharacterSpriteMirror.FaceLeft = false;
            CharacterAnimationSystem.CharacterAnimationState characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(AwayAnimation, 10, ShouldKeepPlayingWallJumpRightAwayAnimation);
            characterAnimationState.OnStopPlaying = OnAnimationEnd;
            characterAnimationState.OnStartPlaying = OnAnimationStart;
        } else if (Sein.Input.NormalizedHorizontal < 0) {
            Vector3 origin = PlatformMovement.Position + Vector3.up * 2f;
            float maxDistance = PlatformMovement.CapsuleCollider.radius + 2f;
            Ray ray = new Ray(origin, PlatformMovement.LocalToWorld(Vector3.left));
            if (Physics.Raycast(ray, maxDistance)) {
                CharacterAnimationSystem.CharacterAnimationState characterAnimationState2 = Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(TowardsAnimation, 10, ShouldKeepPlayingWallJumpRightTowardsAnimation);
                characterAnimationState2.OnStopPlaying = OnAnimationEnd;
                StartCoroutine(RoutineForMegWhoPlaysMarioAndSucksAtWallJumping());
            } else {
                CharacterAnimationSystem.CharacterAnimationState characterAnimationState3 = Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(EdgeJumpAnimation, 10, ShouldKeepPlayingWallJumpRightTowardsAnimation);
                characterAnimationState3.OnStopPlaying = OnAnimationEnd;
                localSpeed.y = 0f;
            }
        } else {
            CharacterAnimationSystem.CharacterAnimationState characterAnimationState4 = Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(RegularAnimation, 10, ShouldKeepPlayingWallJumpRightRegularAnimation);
            characterAnimationState4.OnStopPlaying = OnAnimationEnd;
            characterAnimationState4.OnStartPlaying = OnAnimationStart;
        }

        Sound.Play(WallJumpSound.GetSoundForMaterial(Sein.PlatformBehaviour.WallSurfaceMaterialType, null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        OnWallJumpEvent(localSpeed);
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(localSpeed.y, 1f);
        }

        Sein.PlatformBehaviour.AirNoDeceleration.NoDeceleration = true;
        Sein.ResetAirLimits();
        JumpFlipPlatform.OnSeinWallJumpEvent();
    }

    public bool ShouldKeepPlayingWallJumpRightTowardsAnimation() {
        return LeftRightMovement.HorizontalInput <= 0f && PlatformMovement.IsInAir && (!PlatformMovement.IsOnWall || !PlatformMovement.HeadAndFeetAgainstWall);
    }

    public bool ShouldKeepPlayingWallJumpRightAwayAnimation() {
        return PlatformMovement.IsInAir && (!PlatformMovement.IsOnWall || !PlatformMovement.HeadAndFeetAgainstWall);
    }

    public bool ShouldKeepPlayingWallJumpRightRegularAnimation() {
        return PlatformMovement.IsInAir && (!PlatformMovement.IsOnWall || !PlatformMovement.HeadAndFeetAgainstWall);
    }

    public void ApplyImpulseToWall(Vector2 speed) {
        PlatformMovementListOfColliders platformMovementListOfColliders = Sein.PlatformBehaviour.PlatformMovementListOfColliders;
        for (int i = 0; i < platformMovementListOfColliders.WallLeftColliders.Count; i++) {
            Collider collider = platformMovementListOfColliders.WallLeftColliders[i];
            if (collider) {
                Rigidbody attachedRigidbody = collider.attachedRigidbody;
                if (attachedRigidbody) {
                    Vector3 force = PlatformMovement.LocalToWorld(-speed.normalized * WallJumpImpulse);
                    attachedRigidbody.AddForceAtPosition(force, PlatformMovement.Position, ForceMode.Impulse);
                }
            }
        }

        for (int j = 0; j < platformMovementListOfColliders.WallRightColliders.Count; j++) {
            Collider collider2 = platformMovementListOfColliders.WallRightColliders[j];
            if (collider2) {
                Rigidbody attachedRigidbody2 = collider2.attachedRigidbody;
                if (attachedRigidbody2) {
                    Vector3 force2 = PlatformMovement.LocalToWorld(-speed.normalized * WallJumpImpulse);
                    attachedRigidbody2.AddForceAtPosition(force2, PlatformMovement.Position, ForceMode.Impulse);
                }
            }
        }
    }

    public override void UpdateCharacterState() {
        if (PlatformMovement.IsOnGround) {
            m_hasWallJumpedLeft = false;
            m_hasWallJumpedRight = false;
        }
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref m_hasWallJumpedLeft);
        ar.Serialize(ref m_hasWallJumpedRight);
        ar.Serialize(ref m_lockInputTimeRemaining);
        ar.Serialize(ref m_spriteMirrorLock);
    }

    public void OnRestoreCheckpoint() {
        m_spriteMirrorLock = false;
    }

    public TextureAnimationWithTransitions[] AwayAnimation;

    public bool DontAllowJumpingTowardsWall;

    public TextureAnimationWithTransitions[] EdgeJumpAnimation;

    public Vector2 JumpStrength;

    public bool LimitWallJumping;

    public float LockDoubleJumpTowardsDuration = 1.5f;

    public HorizontalPlatformMovementSettings.SpeedMultiplierSet MoveSpeed;

    public TextureAnimationWithTransitions[] RegularAnimation;

    public SeinCharacter Sein;

    public TextureAnimationWithTransitions[] TowardsAnimation;

    public float WallJumpImpulse = 20f;

    public SurfaceToSoundProviderMap WallJumpSound;

    private bool m_hasWallJumpedLeft;

    private bool m_hasWallJumpedRight;

    private float m_lockInputTimeRemaining;

    private bool m_spriteMirrorLock;
}

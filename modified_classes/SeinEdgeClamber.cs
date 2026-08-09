using Core;
using UnityEngine;

public class SeinEdgeClamber : CharacterState, ISeinReceiver {
    public PlatformMovement PlatformMovement {
        get { return Sein.PlatformBehaviour.PlatformMovement; }
    }

    public CharacterLeftRightMovement LeftRightMovement {
        get { return Sein.PlatformBehaviour.LeftRightMovement; }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        Sein.Abilities.EdgeClamber = this;
    }

    public override void UpdateCharacterState() {
        if (!Active) {
            return;
        }

        if (m_isEdgeClambering) {
            if (!PlatformMovement.IsOnWall) {
                m_isEdgeClambering = false;
            }
        } else if (PlatformMovement.IsOnWall && !PlatformMovement.HeadAgainstWall && PlatformMovement.FeetAgainstWall && ((PlatformMovement.HasWallLeft && Sein.Input.NormalizedHorizontal < 0) || (PlatformMovement.HasWallRight && Sein.Input.NormalizedHorizontal > 0)) && PlatformMovement.LocalSpeedY > 0f) {
            if (PlatformMovement.HasWallLeft && Sein.PlatformBehaviour.PlatformMovementListOfColliders.WallLeftCollider && Sein.PlatformBehaviour.PlatformMovementListOfColliders.WallLeftCollider.GetComponent<NonEdgeClamberble>()) {
                return;
            }

            if (PlatformMovement.HasWallRight && Sein.PlatformBehaviour.PlatformMovementListOfColliders.WallRightCollider && Sein.PlatformBehaviour.PlatformMovementListOfColliders.WallRightCollider.GetComponent<NonEdgeClamberble>()) {
                return;
            }

            PerformEdgeClamber();
        }

        base.UpdateCharacterState();
    }

    public void PerformEdgeClamber() {
        PerformEdgeClamber(0.65f);
    }

    public void PerformEdgeClamber(float minSpeedFactor) {
        Sein.PlatformBehaviour.Visuals.Animation.Play(EdgeClamberAnimation, 10, ShouldAnimationKeepPlaying);
        m_isEdgeClambering = true;
        if (PlatformMovement.LocalSpeedY < 9f) {
            PlatformMovement.LocalSpeedY = 9f;
        }

        if (PlatformMovement.HasWallLeft) {
            PlatformMovement.LocalSpeedX = Mathf.Min(PlatformMovement.LocalSpeedX, Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed * -minSpeedFactor);
        } else {
            PlatformMovement.LocalSpeedX = Mathf.Max(PlatformMovement.LocalSpeedX, Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed * minSpeedFactor);
        }

        if (EdgeClamberSound) {
            Sound.Play(EdgeClamberSound.GetSound(null), transform.position, null);
        }

        Sein.PlatformBehaviour.AirNoDeceleration.NoDeceleration = true;
    }

    public bool ShouldAnimationKeepPlaying() {
        return !PlatformMovement.IsOnGround;
    }

    public TextureAnimationWithTransitions EdgeClamberAnimation;

    public SoundProvider EdgeClamberSound;

    public SeinCharacter Sein;

    private bool m_isEdgeClambering;
}

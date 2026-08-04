using System;
using Core;
using UnityEngine;

public class SeinDoubleJump : CharacterState, ISeinReceiver {
    static SeinDoubleJump() {
        OnDoubleJumpEvent = delegate { };
    }

    public static event Action<float> OnDoubleJumpEvent;

    public int ExtraJumpsAvailable {
        get {
            var bonus = RandomizerBonus.DoubleJumpUpgrades();
            if (CheatsHandler.InfiniteDoubleJumps || RandomizerBonus.EnhancedDoubleJump) {
                return 999999;
            }

            if (Sein.PlayerAbilities.DoubleJumpUpgrade.HasAbility) {
                return 2 + bonus;
            }

            return 1 + bonus;
        }
    }

    public PlatformMovement PlatformMovement => Sein.PlatformBehaviour.PlatformMovement;

    public SeinJump Jump => Sein.Abilities.Jump;

    public bool CanDoubleJump => enabled && !PlatformMovement.IsOnGround && numberOfJumpsAvailable != 0 && remainingLockTime <= 0f && !SeinAbilityRestrictZone.IsInside();

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        Sein.Abilities.DoubleJump = this;
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref doubleJumpTime);
        ar.Serialize(ref numberOfJumpsAvailable);
        ar.Serialize(ref remainingLockTime);
    }

    public void PerformDoubleJump() {
        if (Sein.Abilities.ChargeJump) {
            Sein.Abilities.ChargeJump.OnDoubleJump();
        }

        PlatformMovement.LocalSpeedY = JumpStrength * RandomizerBonus.DoubleJumpscale;
        numberOfJumpsAvailable--;
        Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(DoubleJumpAnimation, 10, ShouldDoubleJumpAnimationKeepPlaying);
        doubleJumpSound = Sound.Play(DoubleJumpSound.GetSound(null), Sein.PlatformBehaviour.PlatformMovement.Position, delegate { doubleJumpSound = null; });
        OnDoubleJumpEvent(JumpStrength * RandomizerBonus.DoubleJumpscale);
        var original = DoubleJumpAfterShock;
        if (numberOfJumpsAvailable == 0 && ExtraJumpsAvailable == 2) {
            original = TrippleJumpAfterShock;
        }

        var worldSpeed = PlatformMovement.WorldSpeed;
        var num = Mathf.Atan2(worldSpeed.x, worldSpeed.y) * 57.29578f;
        InstantiateUtility.Instantiate(original, Sein.Position, Quaternion.Euler(0f, 0f, -num));
        JumpFlipPlatform.OnSeinDoubleJumpEvent();
    }

    public bool ShouldDoubleJumpAnimationKeepPlaying() {
        return PlatformMovement.IsInAir && !PlatformMovement.IsOnCeiling;
    }

    public override void UpdateCharacterState() {
        if (Sein.IsSuspended) {
            return;
        }

        if (PlatformMovement.IsOnGround && numberOfJumpsAvailable != ExtraJumpsAvailable) {
            ResetDoubleJump();
        }

        if (doubleJumpSound && (PlatformMovement.IsOnWall || PlatformMovement.IsOnCeiling)) {
            doubleJumpSound.FadeOut(0.5f, true);
            UberPoolManager.Instance.RemoveOnDestroyed(doubleJumpSound.gameObject);
            doubleJumpSound = null;
        }

        if (remainingLockTime > 0f) {
            remainingLockTime -= Time.deltaTime;
        }

        if (doubleJumpTime > 0f) {
            if (PlatformMovement.LocalSpeedY <= 0f) {
                doubleJumpTime = 0f;
            }

            doubleJumpTime -= Time.deltaTime;
        }
    }

    public void ResetDoubleJump() {
        numberOfJumpsAvailable = ExtraJumpsAvailable;
    }

    public void LockForDuration(float duration) {
        remainingLockTime = Mathf.Max(remainingLockTime, duration);
    }

    public void ResetLock() {
        remainingLockTime = 0f;
    }

    public TextureAnimationWithTransitions[] DoubleJumpAnimation;

    public GameObject DoubleJumpAfterShock;

    public GameObject TrippleJumpAfterShock;

    public SoundProvider DoubleJumpSound;

    public float JumpStrength;

    public SeinCharacter Sein;

    private SoundPlayer doubleJumpSound;

    private float doubleJumpTime;

    private int numberOfJumpsAvailable;

    private float remainingLockTime;
}

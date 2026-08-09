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

    public bool CanDoubleJump => enabled && !PlatformMovement.IsOnGround && m_numberOfJumpsAvailable != 0 && m_remainingLockTime <= 0f && !SeinAbilityRestrictZone.IsInside();

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        Sein.Abilities.DoubleJump = this;
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref m_doubleJumpTime);
        ar.Serialize(ref m_numberOfJumpsAvailable);
        ar.Serialize(ref m_remainingLockTime);
    }

    public void PerformDoubleJump() {
        if (Sein.Abilities.ChargeJump) {
            Sein.Abilities.ChargeJump.OnDoubleJump();
        }

        PlatformMovement.LocalSpeedY = JumpStrength * RandomizerBonus.DoubleJumpscale;
        m_numberOfJumpsAvailable--;
        Sein.PlatformBehaviour.Visuals.Animation.PlayRandom(DoubleJumpAnimation, 10, ShouldDoubleJumpAnimationKeepPlaying);
        m_doubleJumpSound = Sound.Play(DoubleJumpSound.GetSound(null), Sein.PlatformBehaviour.PlatformMovement.Position, delegate { m_doubleJumpSound = null; });
        OnDoubleJumpEvent(JumpStrength * RandomizerBonus.DoubleJumpscale);
        var original = DoubleJumpAfterShock;
        if (m_numberOfJumpsAvailable == 0 && ExtraJumpsAvailable == 2) {
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

        if (PlatformMovement.IsOnGround && m_numberOfJumpsAvailable != ExtraJumpsAvailable) {
            ResetDoubleJump();
        }

        if (m_doubleJumpSound && (PlatformMovement.IsOnWall || PlatformMovement.IsOnCeiling)) {
            m_doubleJumpSound.FadeOut(0.5f, true);
            UberPoolManager.Instance.RemoveOnDestroyed(m_doubleJumpSound.gameObject);
            m_doubleJumpSound = null;
        }

        if (m_remainingLockTime > 0f) {
            m_remainingLockTime -= Time.deltaTime;
        }

        if (m_doubleJumpTime > 0f) {
            if (PlatformMovement.LocalSpeedY <= 0f) {
                m_doubleJumpTime = 0f;
            }

            m_doubleJumpTime -= Time.deltaTime;
        }
    }

    public void ResetDoubleJump() {
        m_numberOfJumpsAvailable = ExtraJumpsAvailable;
    }

    public void LockForDuration(float duration) {
        m_remainingLockTime = Mathf.Max(m_remainingLockTime, duration);
    }

    public void ResetLock() {
        m_remainingLockTime = 0f;
    }

    public TextureAnimationWithTransitions[] DoubleJumpAnimation;

    public GameObject DoubleJumpAfterShock;

    public GameObject TrippleJumpAfterShock;

    public SoundProvider DoubleJumpSound;

    public float JumpStrength;

    public SeinCharacter Sein;

    private SoundPlayer m_doubleJumpSound;

    private float m_doubleJumpTime;

    private int m_numberOfJumpsAvailable;

    private float m_remainingLockTime;
}

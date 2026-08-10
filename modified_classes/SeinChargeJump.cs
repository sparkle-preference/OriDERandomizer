using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;

public class SeinChargeJump : CharacterState, ISeinReceiver {
    public event Action<float> OnJumpEvent = delegate { };

    public PlayerAbilities PlayerAbilities => Sein.PlayerAbilities;

    public PlatformMovement PlatformMovement => Sein.PlatformBehaviour.PlatformMovement;

    public SeinChargeJump ChargeJump => Sein.Abilities.ChargeJump;

    public CharacterUpwardsDeceleration UpwardsDeceleration => Sein.PlatformBehaviour.UpwardsDeceleration;

    public void OnDoubleJump() {
        UpwardsDeceleration.Reset();
        ChangeState(State.Normal);
    }

    public override void UpdateCharacterState() {
        if (Sein.IsSuspended) {
            return;
        }

        UpdateState();
    }

    public void ChangeState(State state) {
        CurrentState = state;
        m_stateCurrentTime = 0f;
        m_attackablesIgnore.Clear();
        var currentState = CurrentState;
    }

    public void UpdateState() {
        var currentState = CurrentState;
        if (currentState == State.Jumping) {
            if (m_stateCurrentTime > JumpDuration) {
                ChangeState(State.Normal);
            }

            for (var i = 0; i < Targets.Attackables.Count; i++) {
                var attackable = Targets.Attackables[i];
                if (!InstantiateUtility.IsDestroyed(attackable as Component) && !m_attackablesIgnore.Contains(attackable) && attackable.CanBeStomped()) {
                    var vector = attackable.Position - Sein.PlatformBehaviour.PlatformMovement.HeadPosition;
                    var magnitude = vector.magnitude;
                    if (magnitude < 3f && Vector2.Dot(vector.normalized, PlatformMovement.LocalSpeed.normalized) > 0f) {
                        m_attackablesIgnore.Add(attackable);
                        var damage = new Damage(Damage, PlatformMovement.WorldSpeed.normalized * 3f, Sein.Position, DamageType.Stomp, gameObject);
                        damage.DealToComponents(((Component)attackable).gameObject);
                        if (attackable.IsDead() && attackable is IStompAttackable && ((IStompAttackable)attackable).CountsTowardsSuperJumpAchievement()) {
                            AchievementsLogic.Instance.OnSuperJumpedThroughEnemy();
                        }

                        if (ExplosionEffect) {
                            InstantiateUtility.Instantiate(ExplosionEffect, Vector3.Lerp(transform.position, attackable.Position, 0.5f), Quaternion.identity);
                        }

                        break;
                    }
                }
            }
        } else if (Sein.Abilities.ChargeJumpCharging.IsCharged && RandomizerBonus.EnhancedChargeJump) {
            for (var i = 0; i < Targets.Attackables.Count; i++) {
                var attackable = Targets.Attackables[i];
                if (!InstantiateUtility.IsDestroyed(attackable as Component) && attackable.CanBeStomped()) {
                    var vector = attackable.Position - Sein.PlatformBehaviour.PlatformMovement.HeadPosition;
                    var magnitude = vector.magnitude;
                    if (magnitude < 3.75f) {
                        var damage = new Damage(Damage, PlatformMovement.WorldSpeed.normalized * 3f, Sein.Position, DamageType.Stomp, gameObject);
                        damage.DealToComponents(((Component)attackable).gameObject);
                        if (ExplosionEffect && attackable.IsDead()) {
                            InstantiateUtility.Instantiate(ExplosionEffect, Vector3.Lerp(transform.position, attackable.Position, 0.5f), Quaternion.identity);
                        }
                    }
                }
            }
        }

        m_stateCurrentTime += Time.deltaTime;
    }

    public bool CanChargeJump => Sein.Abilities.ChargeJumpCharging.IsCharged && PlatformMovement.IsOnGround;

    public void PerformChargeJump() {
        var chargedJumpStrength = ChargedJumpStrength + ChargedJumpStrength * 0.08f * (RandomizerBonus.Velocity() + RandomizerBonus.Jumpgrades());
        PlatformMovement.LocalSpeedY = chargedJumpStrength;
        OnJumpEvent(chargedJumpStrength);
        Sound.Play(JumpSound.GetSound(null), Sein.PlatformBehaviour.PlatformMovement.Position, null);
        UpwardsDeceleration.Deceleration = Deceleration;
        Sein.Mortality.DamageReciever.MakeInvincibleToEnemies(JumpDuration);
        ChangeState(State.Jumping);
        Sein.PlatformBehaviour.Visuals.Animation.Play(JumpAnimation, 10, ShouldChargeJumpAnimationKeepPlaying);
        Sein.PlatformBehaviour.Visuals.SpriteRotater.BeginTiltLeftRightInAir(1.5f);
        if (Sein.PlatformBehaviour.JumpSustain) {
            Sein.PlatformBehaviour.JumpSustain.SetAmountOfSpeedToLose(PlatformMovement.LocalSpeedY, 1f);
        }

        Sein.Abilities.ChargeJumpCharging.EndCharge();
        JumpFlipPlatform.OnSeinChargeJumpEvent();
    }

    public bool ShouldChargeJumpAnimationKeepPlaying() {
        return PlatformMovement.IsInAir && !PlatformMovement.IsOnWall && !PlatformMovement.IsOnCeiling;
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        Sein.Abilities.ChargeJump = this;
    }

    public override void Serialize(Archive ar) {
        base.Serialize(ar);
        ar.Serialize(ref m_superJumpedEnemies);
    }

    public SeinCharacter Sein;

    public TextureAnimationWithTransitions JumpAnimation;

    public SoundProvider JumpSound;

    public float JumpDuration = 0.5f;

    public State CurrentState;

    // private, like vanilla: public would put these in Unity's serialized set
    private float m_stateCurrentTime;

    private HashSet<IAttackable> m_attackablesIgnore = new HashSet<IAttackable>();

    public GameObject ExplosionEffect;

    public int Damage = 50;

    public float ChargingTime;

    public float ChargedJumpStrength;

    public float Deceleration = 20f;

    private int m_superJumpedEnemies;

    public enum State {
        Normal,
        Jumping
    }
}

using System;
using System.Collections.Generic;
using fsm;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinStomp : CharacterState, ISeinReceiver {
    static SeinStomp() {
        OnStompIdleEvent = delegate { };
        OnStompLandEvent = delegate { };
        OnStompDownEvent = delegate { };
    }

    public static event Action OnStompIdleEvent;

    public static event Action OnStompLandEvent;

    public static event Action OnStompDownEvent;

    public CharacterLeftRightMovement LeftRightMovement {
        get { return Sein.PlatformBehaviour.LeftRightMovement; }
    }

    public SeinDoubleJump DoubleJump {
        get { return Sein.Abilities.DoubleJump; }
    }

    public CharacterUpwardsDeceleration UpwardsDeceleration {
        get { return Sein.PlatformBehaviour.UpwardsDeceleration; }
    }

    public PlatformMovement PlatformMovement {
        get { return Sein.PlatformBehaviour.PlatformMovement; }
    }

    public bool Finished {
        get { return Logic.CurrentState == State.Inactive; }
    }

    public bool IsStomping {
        get { return Logic.CurrentState != State.Inactive; }
    }

    public void OnRestoreCheckpoint() {
        Logic.ChangeState(State.Inactive);
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        Sein.Abilities.Stomp = this;
    }

    public void Start() {
        Sein.PlatformBehaviour.Gravity.ModifyGravityPlatformMovementSettingsEvent += ModifyVerticalPlatformMovementSettings;
        PlatformMovement.OnCollisionGroundEvent += OnCollisionGround;
        PlatformMovement.OnCollisionCeilingEvent += OnCollisionOther;
        PlatformMovement.OnCollisionWallLeftEvent += OnCollisionOther;
        PlatformMovement.OnCollisionWallRightEvent += OnCollisionOther;
    }

    public override void OnExit() {
        Logic.ChangeState(State.Inactive);
    }

    public override void UpdateCharacterState() {
        Logic.UpdateState(Time.deltaTime);
    }

    public void ModifyVerticalPlatformMovementSettings(GravityPlatformMovementSettings settings) {
        if (Logic.CurrentState != State.Inactive) {
            settings.GravityStrength = 0f;
        }
    }

    public float StompDamage {
        get {
            if (Sein.PlayerAbilities.StompUpgrade.HasAbility) {
                return RandomizerBonusSkill.AbilityDamage(UpgradedDamage);
            }

            return RandomizerBonusSkill.AbilityDamage(Damage);
        }
    }

    public void OnCollisionGround(Vector3 normal, Collider collider) {
        if (Logic.CurrentState == State.StompDown) {
            if (Vector3.Dot(StompDirection, normal) > -0.17365f) {
                return;
            }

            LandStomp();
            if (!Sein.Controller.IsSwimming) {
                IAttackable attackable = collider.gameObject.FindComponent<IAttackable>();
                if (attackable != null && attackable.CanBeStomped()) {
                    Damage damage = new Damage(StompDamage, StompDirection * 3f, Characters.Sein.Position, DamageType.Stomp, gameObject);
                    damage.DealToComponents(collider.gameObject);
                }

                DoBlastRadius(attackable);
            }

            Logic.ChangeState(State.StompFinished);
        }
    }

    public void OnCollisionOther(Vector3 normal, Collider collider) {
        if (Logic.CurrentState != State.StompDown || !RandomizerBonus.EnhancedStomp) {
            return;
        }

        OnCollisionGround(normal, collider);
    }

    public void DoBlastRadius(IAttackable landedStompAttackable) {
        m_stompBlastAttackables.Clear();
        m_stompBlastAttackables.AddRange(Targets.Attackables);
        for (int i = 0; i < m_stompBlastAttackables.Count; i++) {
            IAttackable attackable = m_stompBlastAttackables[i];
            if (!InstantiateUtility.IsDestroyed(attackable as Component)) {
                if (attackable != landedStompAttackable) {
                    if (attackable.CanBeStomped()) {
                        Vector3 vector = attackable.Position - Sein.Position;
                        float magnitude = vector.magnitude;
                        if (magnitude < StompBlashRadius) {
                            Vector3 normalized = (vector.normalized + StompDirection * -2f).normalized;
                            GameObject gameObject = ((Component)attackable).gameObject;
                            float stompDamage = StompDamage;
                            Damage damage = new Damage(stompDamage, normalized * 3f, attackable.Position, DamageType.StompBlast, gameObject);
                            damage.DealToComponents(gameObject);
                        }
                    }
                }
            }
        }

        m_stompBlastAttackables.Clear();
    }

    public override void Awake() {
        State.Inactive = new State {
            OnEnterEvent = OnEnterInactive,
            UpdateStateEvent = UpdateStompInactiveState
        };
        State.StompDown = new State {
            OnEnterEvent = OnEnterStompDownState,
            UpdateStateEvent = UpdateStompDownState
        };
        State.StompIdle = new State {
            OnEnterEvent = OnEnterStompIdleState,
            UpdateStateEvent = UpdateStompIdleState
        };
        State.StompFinished = new State {
            UpdateStateEvent = UpdateStompFinishedState
        };
        Logic.RegisterStates(
            State.Inactive,
            State.StompDown,
            State.StompIdle
        );
        Logic.ChangeState(State.Inactive);
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
    }

    public override void OnDestroy() {
        Sein.PlatformBehaviour.Gravity.ModifyGravityPlatformMovementSettingsEvent -= ModifyVerticalPlatformMovementSettings;
        PlatformMovement.OnCollisionGroundEvent -= OnCollisionGround;
        PlatformMovement.OnCollisionCeilingEvent -= OnCollisionOther;
        PlatformMovement.OnCollisionWallLeftEvent -= OnCollisionOther;
        PlatformMovement.OnCollisionWallRightEvent -= OnCollisionOther;
        Logic.ChangeState(State.Inactive);
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
        base.OnDestroy();
    }

    public void OnEnterStompIdleState() {
        OnStompIdleEvent();
        if (!Sein.PlayerAbilities.StompUpgrade.HasAbility) {
            StompStartSound.Play();
        } else {
            StompStartSoundUpgraded.Play();
        }

        Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(StompIdleAnimation, 111, ShouldStompAnimationKeepPlaying);
    }

    public void OnEnterStompDownState() {
        OnStompDownEvent();
        PlatformMovement.LocalSpeedX *= 0.5f;
        if (!Sein.PlayerAbilities.StompUpgrade.HasAbility) {
            StompFallSound.Play();
        } else {
            StompFallSoundUpgraded.Play();
        }

        Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(StompDownAnimation, 111, ShouldStompAnimationKeepPlaying);
    }

    public void UpdateStompFinishedState() {
        if (Logic.CurrentStateTime > 0.05f) {
            EndStomp();
        }
    }

    public void LandStomp() {
        PlatformMovement.LocalSpeedX = 0f;
        PlatformMovement.LocalSpeedY = 0f;
        OnStompLandEvent();
        if (!Sein.PlayerAbilities.StompUpgrade.HasAbility) {
            StompLandSound.Play();
        } else {
            StompLandSoundUpgraded.Play();
        }

        Sein.PlatformBehaviour.Visuals.Animation.Play(StompLandAnimation, 111, ShouldStompLandAnimationKeepPlaying);
        if (Sein.Controller.IsSwimming) {
            return;
        }

        EndStomp();
        DoStompBlastEffect();
    }

    public void DoStompBlastEffect() {
        if (StompLandEffect != null) {
            if (Sein.PlayerAbilities.StompUpgrade.HasAbility) {
                InstantiateUtility.Instantiate(StompLandEffectUpgraded, Sein.PlatformBehaviour.PlatformMovement.FeetPosition, Quaternion.identity);
            } else {
                InstantiateUtility.Instantiate(StompLandEffect, Sein.PlatformBehaviour.PlatformMovement.FeetPosition, Quaternion.identity);
            }
        }
    }

    public void OnEnterInactive() {
        SpriteRotation = 0f;
    }

    public void UpdateStompIdleState() {
        if (DoubleJump && DoubleJump.CanDoubleJump && Input.Jump.OnPressed) {
            EndStomp();
            DoubleJump.PerformDoubleJump();
            return;
        }

        if (Logic.CurrentStateTime > IdleDuration) {
            Logic.ChangeState(State.StompDown);
        }

        PlatformMovement.LocalSpeedX = 0f;
        PlatformMovement.LocalSpeedY = 0f;
        if (RandomizerBonus.EnhancedStomp && Input.Axis != Vector2.zero) {
            m_stompDirection = Input.Axis.normalized;
        } else {
            m_stompDirection = Vector2.down;
        }
    }

    public void UpdateStompInactiveState() {
        if (Sein.Controller.IsSwimming) {
            return;
        }

        bool flag = Input.Stomp.OnPressed & Sein.Input.NormalizedHorizontal == 0;
        bool flag2 = Input.Stomp.OnPressed && Input.DigiPadAxis.y < 0f;
        bool flag3 = flag || flag2;
        if (flag3 && !Input.Stomp.Used && CanStomp()) {
            Logic.ChangeState(State.StompIdle);
        }
    }

    public bool CanStomp() {
        return Sein.Controller.CanMove && Sein.PlatformBehaviour.PlatformMovement.IsInAir && !Sein.Controller.IsGliding && !Sein.Controller.InputLocked && !SeinAbilityRestrictZone.IsInside();
    }

    public void UpdateStompDownState() {
        if (Input.Jump.OnPressed) {
            EndStomp();
            return;
        }

        bool inputProvided = RandomizerBonus.EnhancedStomp ? Input.Axis != Vector2.zero : Input.Stomp.Pressed;
        if (Logic.CurrentStateTime > StompDownDuration && !inputProvided) {
            EndStomp();
            return;
        }

        if (RandomizerBonus.EnhancedStomp && Input.Axis != Vector2.zero) {
            m_stompDirection = (Input.Axis.normalized + PlatformMovement.LocalSpeed.normalized).normalized;
            SpriteRotation = Mathf.Atan2(m_stompDirection.y, m_stompDirection.x) * 57.29578f + 90f;
        } else {
            m_stompDirection = Vector2.down;
            SpriteRotation = 0f;
        }

        float targetVelocity = StompSpeed + StompSpeed * 0.2f * RandomizerBonus.Velocity();
        PlatformMovement.LocalSpeed = m_stompDirection * targetVelocity;
        Sein.Mortality.DamageReciever.MakeInvincibleToEnemies(0.2f);

        if (Sein.Controller.IsSwimming) {
            EndStomp();
        }

        for (int i = 0; i < Targets.Attackables.Count; i++) {
            IAttackable attackable = Targets.Attackables[i];
            if (!attackable.IsDead()) {
                if (!InstantiateUtility.IsDestroyed(attackable as Component)) {
                    if (attackable.IsStompBouncable()) {
                        Vector3 a = Characters.Sein.Position + StompDirection;
                        if (Vector3.Distance(a, attackable.Position) < 1.5f && Logic.CurrentState == State.StompDown) {
                            GameObject gameObject = ((Component)attackable).gameObject;
                            Damage damage = new Damage(StompDamage, StompDirection * 3f, Characters.Sein.Position, DamageType.Stomp, this.gameObject);
                            damage.DealToComponents(gameObject);
                            if (attackable.IsDead()) {
                                return;
                            }

                            EndStomp();
                            PlatformMovement.LocalSpeed = StompDirection * -17f;
                            Sein.PlatformBehaviour.UpwardsDeceleration.Deceleration = 20f;
                            Sein.Animation.Play(StompBounceAnimation, 111);
                            Sein.ResetAirLimits();
                            StompLandSound.Play();
                            DoBlastRadius(attackable);
                            DoStompBlastEffect();
                            return;
                        }
                    }
                }
            }
        }
    }

    public void EndStomp() {
        Logic.ChangeState(State.Inactive);
    }

    public bool ShouldStompAnimationKeepPlaying() {
        return Logic.CurrentState != State.Inactive;
    }

    public bool ShouldStompLandAnimationKeepPlaying() {
        return PlatformMovement.LocalSpeedX == 0f && PlatformMovement.LocalSpeedY <= 0f;
    }

    public override void Serialize(Archive ar) {
        Logic.Serialize(ar);
        base.Serialize(ar);
    }

    public Vector3 StompDirection {
        get { return new Vector3(m_stompDirection.x, m_stompDirection.y, 0f); }
    }

    public float IdleDuration;

    public StateMachine Logic = new StateMachine();

    public SeinCharacter Sein;

    public States State = new States();

    public float StompBlashRadius = 10f;

    public float Damage = 15f;

    public float UpgradedDamage = 25f;

    public AnimationCurve StompBlastFalloutCurve;

    public TextureAnimationWithTransitions StompBounceAnimation;

    public TextureAnimationWithTransitions StompDownAnimation;

    public float StompDownDuration;

    public SoundSource StompFallSound;

    public SoundSource StompFallSoundUpgraded;

    public TextureAnimationWithTransitions StompIdleAnimation;

    public TextureAnimationWithTransitions StompLandAnimation;

    public float StompLandDuration;

    public GameObject StompLandEffect;

    public GameObject StompLandEffectUpgraded;

    public SoundSource StompLandSound;

    public SoundSource StompLandSoundUpgraded;

    public float StompSpeed;

    public SoundSource StompStartSound;

    public SoundSource StompStartSoundUpgraded;

    public float UpwardDeceleration;

    public List<IAttackable> m_stompBlastAttackables = new List<IAttackable>();

    private Vector2 m_stompDirection;

    public float SpriteRotation;

    public class States {
        public IState Inactive;

        public IState StompDown;

        public IState StompIdle;

        public IState StompFinished;
    }
}

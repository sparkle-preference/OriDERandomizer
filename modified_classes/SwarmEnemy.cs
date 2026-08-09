using System;
using UnityEngine;
using fsm;
using fsm.triggers;

public class SwarmEnemy : GroundEnemy {
    public override bool CanBeOptimized() {
        return this.Controller.StateMachine.CurrentState == this.State.Idle;
    }

    public override void Awake() {
        base.Awake();
        this.DamageReciever.OnDeathEvent.Add(new Action<Damage>(this.OnDeath));
        EntityDamageReciever damageReciever = this.DamageReciever;
        damageReciever.OnModifyDamage = (EntityDamageReciever.ModifyDamageDelegate)Delegate.Combine(damageReciever.OnModifyDamage, new EntityDamageReciever.ModifyDamageDelegate(this.OnPreProcessDamage));
    }

    public void OnPreProcessDamage(Damage damage) {
        EntityDamageDealer component = damage.Sender.GetComponent<EntityDamageDealer>();
        if (component != null) {
            Entity entity = component.Entity;
            if (entity is SwarmEnemy && this.GetInstanceID() > entity.GetInstanceID()) {
                this.PlatformMovement.LocalSpeedX *= 0.7f;
            }
        }
    }

    public new void Start() {
        base.Start();
        this.State.Idle = new State {
            OnEnterEvent = new Action(this.OnEnterIdle),
            UpdateStateEvent = new Action(this.UpdateIdle),
            OnExitEvent = new Action(this.OnExitIdle)
        };
        this.State.Run = new State {
            OnEnterEvent = new Action(this.OnEnterRun),
            UpdateStateEvent = new Action(this.UpdateRun),
            OnExitEvent = new Action(this.OnExitRun)
        };
        this.State.Spawned = new State {
            OnEnterEvent = new Action(this.OnEnterSpawned),
            UpdateStateEvent = new Action(this.UpdateSpawned)
        };
        this.Controller.StateMachine.RegisterStates(
            new IState[] {
                this.State.Idle,
                this.State.Run,
                this.State.Spawned
            }
        );
        this.Controller.StateMachine.Configure(this.State.Idle).AddTransition<OnFixedUpdate>(this.State.Run, new Func<bool>(this.ShouldRun), null);
        this.Controller.StateMachine.Configure(this.State.Run).AddTransition<OnFixedUpdate>(this.State.Idle, () => !this.ShouldRun(), null);
        this.Controller.StateMachine.Configure(this.State.Spawned).AddTransition<OnFixedUpdate>(this.State.Run, () => base.AfterTime(0.5f), null);
        this.Controller.StateMachine.ChangeState((!this.m_wasSpawned) ? this.State.Idle : this.State.Spawned);
    }

    public bool ShouldRun() {
        float num = (float)Math.Sign(base.PositionToPlayerPosition.x);
        bool flag = this.Size != 0f && Physics.Linecast(base.transform.position + new Vector3(num * (this.Size - 1f), 0f), base.transform.position + new Vector3(num * this.Size, 0f));
        bool flag2;
        if (EnemyStopper.InsideEnemyStopper(base.Position, (!base.PlayerIsToLeft) ? Vector3.right : Vector3.left, out flag2)) {
            return false;
        }

        return this.Controller.IsNearSein() && Mathf.Abs(base.PositionToPlayerPosition.x) > 0.5f && !flag;
    }

    public void SetModeToSpawned() {
        this.m_wasSpawned = true;
    }

    public new void FixedUpdate() {
        base.FixedUpdate();
        if (!this.IsSuspended) {
            this.PlatformMovement.LocalSpeedY -= this.Settings.Gravity * Time.deltaTime;
            if (this.PlatformMovement.LocalSpeedY < -this.Settings.MaxFallSpeed) {
                this.PlatformMovement.LocalSpeedY = -this.Settings.MaxFallSpeed;
            }

            this.UpdateRotation();
            if (base.IsInWater) {
                this.Drown();
            }
        }
    }

    public void UpdateRotation() {
        float num = this.SpeedXToRotation.Evaluate(this.PlatformMovement.LocalSpeedX) * this.SpeedYToRotation.Evaluate(this.PlatformMovement.LocalSpeedX) * this.AirTiltAngle;
        float b = (!this.PlatformMovement.IsOnGround) ? num : this.PlatformMovement.GroundAngle;
        this.FeetTransform.eulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(this.FeetTransform.eulerAngles.z, b, 0.1f));
    }

    public void OnEnterIdle() {
        if (this.Idle) {
            this.Idle.Play();
        }
    }

    public void OnEnterRun() {
        if (this.Walking) {
            this.Walking.Play();
        }
    }

    public void OnExitIdle() {
        if (this.Idle) {
            this.Idle.Stop();
        }
    }

    public void OnExitRun() {
        if (this.Walking) {
            this.Walking.Stop();
        }
    }

    public void OnEnterSpawned() {
        this.RestartAnimationLoop(this.Animations.Spawned);
    }

    public void UpdateIdle() {
        if (this.PlatformMovement.IsOnGround) {
            this.PlayAnimationLoop(this.Animations.Idle);
        } else {
            this.PlayAnimationLoop((!this.CanFall) ? this.Animations.Idle : this.Animations.Fall);
        }

        this.PlatformMovement.LocalSpeedX = MoonMath.Movement.DecelerateSpeed(this.PlatformMovement.LocalSpeedX, this.Settings.Decceleration);
    }

    public void UpdateRun() {
        if (this.PlatformMovement.IsOnGround) {
            this.PlayAnimationLoop((!base.PlayerIsToLeft) ? this.Animations.RunRight : this.Animations.RunLeft);
        } else {
            this.PlayAnimationLoop(this.CanFall ? this.Animations.Fall : ((!base.PlayerIsToLeft) ? this.Animations.RunRight : this.Animations.RunLeft));
        }

        this.PlatformMovement.LocalSpeedX = RandomizerBonusSkill.TimeScale(this.Settings.Speed * this.Settings.MoveCurve.Evaluate(base.SpriteAnimator.CurrentAnimationTime) * (float)((!base.PlayerIsToLeft) ? 1 : (-1)));
        if (this.Settings.JumpDelay > 0f) {
            if (this.m_jumpDelay < 0f && this.PlatformMovement.IsOnGround) {
                this.m_jumpDelay = this.Settings.JumpDelay;
                this.PlatformMovement.LocalSpeedY = this.Settings.JumpStrength;
                this.PlayAnimationOnce(this.Animations.Jump, 1);
            }

            this.m_jumpDelay -= Time.deltaTime;
        }
    }

    public void UpdateSpawned() {
    }

    public void OnDeath(Damage damage) {
        if (this.Settings.Child) {
            for (int i = 0; i < 2; i++) {
                Vector3 velocity = (((i != 0) ? Vector3.right : Vector3.left) + Vector3.up * 3f) * 7f;
                SwarmEnemyManager.Instance.QueueSpawn(base.transform.position, velocity, (int)(this.Loot.LootAmount * this.Loot.LootMultiplier), this.OrbSpawner, this.DamageDealer.Damage, this.Settings.Child, this.SceneRootGUID, this.Owner);
            }
        }
    }

    public override void OnDestroy() {
        base.OnDestroy();
        this.Owner.OnChildComponentDestroy(this);
    }

    public SwarmEnemyAnimations Animations;

    public SwarmEnemySettings Settings;

    public SwarmEnemyLootSettings Loot;

    public OrbSpawner OrbSpawner;

    public SoundSource Idle;

    public SoundSource Walking;

    public bool CanFall = true;

    public float Size;

    public SwarmEnemy.States State = new SwarmEnemy.States();

    private bool m_wasSpawned;

    public AnimationCurve SpeedXToRotation;

    public AnimationCurve SpeedYToRotation;

    public float AirTiltAngle;

    private float m_jumpDelay;

    public SwarmEnemyPlaceholder Owner;

    public class States {
        public State Idle;

        public State Run;

        public State Spawned;

        public State Thrown;

        public State Frozen;
    }
}

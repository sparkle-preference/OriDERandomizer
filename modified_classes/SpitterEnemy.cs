using fsm;
using fsm.triggers;
using UnityEngine;

public class SpitterEnemy : GroundEnemy {
    public Vector2 ThrownDirection { get; set; }

    public override void Awake() {
        base.Awake();
    }

    public override bool CanBeOptimized() {
        IState currentState = Controller.StateMachine.CurrentState;
        return currentState == State.Idle || currentState == State.Walk;
    }

    public bool WilhelmScreamZoneRectanglesContain(Vector2 position) {
        for (int i = 0; i < ActionZones.Length; i++) {
            Transform transform = ActionZones[i];
            Rect rect = default(Rect);
            rect.width = transform.lossyScale.x;
            rect.height = transform.lossyScale.y;
            rect.center = transform.position;
            if (rect.Contains(position)) {
                return true;
            }
        }

        return false;
    }

    public new void Start() {
        base.Start();
        State.Idle = new SpitterEnemyIdleState(this);
        State.Walk = new SpitterEnemyWalkState(this);
        State.RunBack = new SpitterEnemyRunBackState(this);
        State.SpitterEnemyCharging = new SpitterEnemyChargingState(this);
        State.Shooting = new SpitterEnemyShootingState(this);
        State.Thrown = new SpitterEnemyThrownState(this);
        State.Stomped = new SpitterEnemyStompedState(this);
        State.Stunned = new SpitterEnemyStunnedState(this);
        Controller.StateMachine.RegisterStates(
            State.Idle,
            State.Walk,
            State.RunBack,
            State.SpitterEnemyCharging,
            State.Shooting,
            State.Stunned,
            State.Thrown,
            State.Stomped
        );
        Controller.StateMachine.Configure(State.Idle).AddTransition<AttackTriggered>(State.SpitterEnemyCharging).AddTransition<OnFixedUpdate>(State.Walk, () => AfterTime(Settings.IdleDuration) && IsOnScreen()).AddTransition<OnFixedUpdate>(State.RunBack, CanSeePlayer).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, State.Thrown.OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, State.Stomped.OnStomped).AddTransition<OnReceiveDamage>(State.SpitterEnemyCharging);
        Controller.StateMachine.Configure(State.Walk).AddTransition<AttackTriggered>(State.SpitterEnemyCharging).AddTransition<OnFixedUpdate>(State.Idle, () => AfterTime(Settings.WalkDuration) && IsOnScreen()).AddTransition<OnFixedUpdate>(State.Idle, () => !IsOnScreen()).AddTransition<OnFixedUpdate>(State.Idle, HasHitWall, TurnAround).AddTransition<OnFixedUpdate>(State.RunBack, CanSeePlayer).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, State.Thrown.OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, State.Stomped.OnStomped).AddTransition<OnReceiveDamage>(State.SpitterEnemyCharging);
        Controller.StateMachine.Configure(State.RunBack).AddTransition<AttackTriggered>(State.SpitterEnemyCharging).AddTransition<OnFixedUpdate>(State.Idle, () => !CanSeePlayer()).AddTransition<OnFixedUpdate>(State.SpitterEnemyCharging, FurtherThanMinChargeDistance).AddTransition<OnFixedUpdate>(State.SpitterEnemyCharging, HasHitWall, TurnAround).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, State.Thrown.OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, State.Stomped.OnStomped).AddTransition<OnReceiveDamage>(State.SpitterEnemyCharging);
        Controller.StateMachine.Configure(State.SpitterEnemyCharging).AddTransition<OnFixedUpdate>(State.Shooting, () => AfterTime(Settings.ChargeDuration)).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, State.Thrown.OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, State.Stomped.OnStomped);
        Controller.StateMachine.Configure(State.Shooting).AddTransition<OnFixedUpdate>(State.Idle, () => AfterTime(Settings.ShootingDuration) && !CanSeePlayer()).AddTransition<OnFixedUpdate>(State.RunBack, () => AfterTime(Settings.ShootingDuration) && CloserThanMinChargeDistance()).AddTransition<OnFixedUpdate>(State.SpitterEnemyCharging, () => AfterTime(Settings.ShootingDuration) && FurtherThanMinChargeDistance()).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, State.Thrown.OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, State.Stomped.OnStomped);
        Controller.StateMachine.Configure(State.Thrown).AddTransition<OnFixedUpdate>(State.Stunned, IsOnGround).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, State.Thrown.OnThrow);
        Controller.StateMachine.Configure(State.Stomped).AddTransition<OnFixedUpdate>(State.Stunned, IsOnGround).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, State.Thrown.OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, State.Stomped.OnStomped);
        Controller.StateMachine.Configure(State.Stunned).AddTransition<OnFixedUpdate>(State.Idle, () => AfterTime(Settings.StunnedDuration) && !CanSeePlayer()).AddTransition<OnFixedUpdate>(State.RunBack, () => AfterTime(Settings.StunnedDuration) && CanSeePlayer()).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, State.Thrown.OnThrow);
        Controller.StateMachine.ChangeState(State.Idle);
    }

    public bool IsOnGround() {
        return PlatformMovement.IsOnGround;
    }

    public bool HasHitWall() {
        return PlatformMovement.IsOnWall;
    }

    public void TurnAround() {
        FaceLeft = !FaceLeft;
    }

    public bool CanSeePlayer() {
        return Controller.NearSein && PositionToPlayerPosition.magnitude < Settings.SeePlayerDistance;
    }

    public bool FurtherThanMinChargeDistance() {
        return PositionToPlayerPosition.magnitude > Settings.MinChargeDistance;
    }

    public bool CloserThanMinChargeDistance() {
        return PositionToPlayerPosition.magnitude < Settings.MinChargeDistance;
    }

    public new void FixedUpdate() {
        base.FixedUpdate();
        if (IsSuspended) {
            return;
        }

        bool flag;
        if (PlatformMovement.MovingHorizontally && EnemyStopper.InsideEnemyStopper(Position, !FaceLeft ? Vector3.right : Vector3.left, out flag)) {
            FaceLeft = !FaceLeft;
            if (Controller.StateMachine.CurrentState == State.RunBack) {
                Controller.StateMachine.ChangeState(State.SpitterEnemyCharging);
            }
        }

        if (!PlatformMovement.IsSuspended && PlatformMovement.IsInAir) {
            PlatformMovement.LocalSpeedY -= Settings.Gravity * RandomizerBonusSkill.TimeScale(Time.deltaTime);
        }

        UpdateRotation();
        if (IsInWater) {
            Drown();
        }

        if (WilhelmScreamZoneRectanglesContain(transform.position) && !m_hasEnteredZone && EnterZoneAction) {
            m_hasEnteredZone = true;
            BingoController.OnScream();
            EnterZoneAction.Perform(null);
        }
    }

    public void UpdateRotation() {
        IState currentState = Controller.StateMachine.CurrentState;
        float currentStateTime = Controller.StateMachine.CurrentStateTime;
        if (currentState == State.Thrown) {
            float num = 1f - Mathf.InverseLerp(0.3f, 0.6f, currentStateTime);
            FeetTransform.eulerAngles = new Vector3(0f, 0f, (MoonMath.Angle.AngleFromVector(ThrownDirection) - 90f) * num);
        } else {
            float b = !PlatformMovement.IsOnGround ? 0f : PlatformMovement.GroundAngle;
            FeetTransform.eulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(FeetTransform.eulerAngles.z, b, 0.2f));
        }
    }

    public bool ShouldThrow() {
        OnReceiveDamage onReceiveDamage = (OnReceiveDamage)Controller.StateMachine.CurrentTrigger;
        return onReceiveDamage.Damage.Type == DamageType.Bash;
    }

    public bool ShouldStomped() {
        OnReceiveDamage onReceiveDamage = (OnReceiveDamage)Controller.StateMachine.CurrentTrigger;
        return onReceiveDamage.Damage.Type == DamageType.StompBlast;
    }

    public PrefabSpawner SpitEffect;

    public PrefabSpawner ProjectileSpawner;

    public ChargingSootEnemyAnimations Animations;

    public ChargingSootEnemySettings Settings;

    public SoundSource IdleSound;

    public SoundSource WalkSound;

    public SoundSource RunAwaySound;

    public SoundSource AttackSound;

    public SoundSource LandSound;

    public ActionMethod EnterZoneAction;

    public Transform[] ActionZones;

    private bool m_hasEnteredZone;

    public States State = new States();

    public class States {
        public SpitterEnemyIdleState Idle;

        public SpitterEnemyWalkState Walk;

        public SpitterEnemyRunBackState RunBack;

        public SpitterEnemyChargingState SpitterEnemyCharging;

        public SpitterEnemyShootingState Shooting;

        public SpitterEnemyThrownState Thrown;

        public SpitterEnemyStompedState Stomped;

        public SpitterEnemyStunnedState Stunned;
    }
}

using fsm;
using fsm.triggers;

public class DashOwlEnemy : OwlEnemy {
    public class States {
        public DashOwlIdleState Idle;

        public DashOwlFlyHomeState FlyHome;

        public DashOwlBashedState Bashed;

        public DashOwlBounceState Bounce;

        public DashOwlDashState Dash;

        public DashOwlDashAlertState DashAlert;

        public DashOwlHurtState Hurt;
    }

    public SoundSource DashSound;

    public SoundSource DashAlertSound;

    public Varying2DSoundProvider HitWallSound;

    public DashOwlEnemyAnimations Animations;

    public DashOwlEnemySettings Settings;

    public States State = new States();

    public override bool CanBeOptimized() {
        IState currentState = Controller.StateMachine.CurrentState;
        return currentState == State.Idle;
    }

    public new void Awake() {
        base.Awake();
        State.Idle = new DashOwlIdleState(this);
        State.Bashed = new DashOwlBashedState(this);
        State.Bounce = new DashOwlBounceState(this);
        State.Dash = new DashOwlDashState(this);
        State.DashAlert = new DashOwlDashAlertState(this);
        State.FlyHome = new DashOwlFlyHomeState(this);
        State.Hurt = new DashOwlHurtState(this);
        Controller.StateMachine.Configure(State.Idle).AddTransition<OnFixedUpdate>(State.DashAlert, PlayerInDashRange).AddTransition<OnReceiveDamage>(State.Bashed, DamageTypeIsBash)
            .AddTransition<OnReceiveDamage>(State.Hurt);
        Controller.StateMachine.Configure(State.Bashed).AddTransition<OnCollisionEnter>(State.Bounce, () => true, OnBashBounce).AddTransition<OnAnimationEnded>(State.DashAlert, PlayerInDashRange)
            .AddTransition<OnAnimationEnded>(State.FlyHome)
            .AddTransition<OnReceiveDamage>(State.Bashed, DamageTypeIsBash)
            .AddTransition<OnReceiveDamage>(State.Hurt, DamageTypeIsStompBlast);
        Controller.StateMachine.Configure(State.Bounce).AddTransition<OnAnimationEnded>(State.DashAlert, PlayerInDashRange).AddTransition<OnAnimationEnded>(State.FlyHome)
            .AddTransition<OnReceiveDamage>(State.Bashed, DamageTypeIsBash)
            .AddTransition<OnReceiveDamage>(State.Hurt, DamageTypeIsStompBlast);
        Controller.StateMachine.Configure(State.Dash).AddTransition<OnCollisionEnter>(State.Bounce).AddTransition<OnAnimationEnded>(State.DashAlert, PlayerInDashRange)
            .AddTransition<OnAnimationEnded>(State.FlyHome)
            .AddTransition<OnReceiveDamage>(State.Bashed, DamageTypeIsBash)
            .AddTransition<OnReceiveDamage>(State.Hurt, DamageTypeIsStompBlast);
        Controller.StateMachine.Configure(State.DashAlert).AddTransition<OnAnimationEnded>(State.Dash).AddTransition<OnReceiveDamage>(State.Bashed, DamageTypeIsBash)
            .AddTransition<OnReceiveDamage>(State.Hurt, DamageTypeIsStompBlast);
        Controller.StateMachine.Configure(State.FlyHome).AddTransition<OnFixedUpdate>(State.DashAlert, PlayerInDashRange).AddTransition<OnFixedUpdate>(State.Idle, State.FlyHome.IsHome)
            .AddTransition<OnReceiveDamage>(State.Bashed, DamageTypeIsBash)
            .AddTransition<OnReceiveDamage>(State.Hurt);
        Controller.StateMachine.Configure(State.Hurt).AddTransition<OnAnimationEnded>(State.DashAlert).AddTransition<OnReceiveDamage>(State.Bashed, DamageTypeIsBash)
            .AddTransition<OnReceiveDamage>(State.Hurt, DamageTypeIsStompBlast);
        Controller.StateMachine.RegisterStates(State.Idle, State.FlyHome, State.Bashed, State.Bounce, State.Dash, State.DashAlert, State.Hurt);
    }

    public new void Start() {
        base.Start();
        Controller.StateMachine.ChangeState(State.Idle);
    }

    public new void FixedUpdate() {
        base.FixedUpdate();
        if (!IsSuspended && base.IsInWater) {
            Drown();
        }
    }

    public void OnBashBounce() {
    }

    public bool CurrentStateTimeGreaterThan(float duration) {
        return Controller.StateMachine.CurrentStateTime > duration;
    }

    public bool NearPlayer() {
        return Controller.NearSein;
    }

    public bool PlayerInAggressiveRange() {
        return NearPlayer();
    }

    public bool PlayerOutsideAggressiveRange() {
        return !NearPlayer();
    }

    public bool PlayerInDashRange() {
        return IsOnScreen() && IsWithinDistanceFromStartPosition() && NearPlayer() && base.PositionToPlayerPosition.magnitude < Settings.DashRange;
    }

    public bool IsWithinDistanceFromStartPosition() {
        return base.StartPositionToPlayerPosition.magnitude < Settings.MaxDistanceFromStartPosition;
    }

    public bool DamageTypeIsBash() {
        return ((OnReceiveDamage)Controller.StateMachine.CurrentTrigger).Damage.Type == DamageType.Bash;
    }

    public bool DamageTypeIsStompBlast() {
        return ((OnReceiveDamage)Controller.StateMachine.CurrentTrigger).Damage.Type == DamageType.StompBlast;
    }
}

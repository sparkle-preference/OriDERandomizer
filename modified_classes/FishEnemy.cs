using System;
using Game;
using UnityEngine;
using fsm;
using fsm.triggers;

public class FishEnemy : Enemy {
    public class States {
        public FishIdleState Idle;

        public FishSwimState Swim;

        public FishAttackState Attack;

        public FishBashedState Bashed;

        public FishBounceState Bounce;

        public FishFlopState Flop;

        public FishFallState Fall;
    }

    public FishEnemyAnimations Animations;

    public FishEnemySounds Sounds;

    public FlyMovement FlyMovement;

    public FishEnemySettings Settings;

    public new Transform Rotation;

    public States State = new States();

    public Transform WanderTarget;

    public AnimationCurve AnimationFromBend;

    private float m_lastAngle;

    private float m_currentAngularVelocity;

    private bool m_inWater;

    public float Angle;

    public float BendValue => AnimationFromBend.Evaluate((float)(base.FaceLeft ? 1 : (-1)) * m_currentAngularVelocity);

    public Vector2 AngleAsVector => MoonMath.Angle.VectorFromAngle(Angle);

    public override bool CanBeOptimized() {
        IState currentState = Controller.StateMachine.CurrentState;
        return currentState == State.Idle;
    }

    public new void FixedUpdate() {
        base.FixedUpdate();
        if (IsSuspended) {
            return;
        }
        float angle = Angle;
        float b = Mathf.DeltaAngle(angle, m_lastAngle) / Time.deltaTime;
        m_lastAngle = angle;
        m_currentAngularVelocity = Mathf.Lerp(m_currentAngularVelocity, b, 0.5f);
        bool flag = false;
        Vector3 position = base.Position;
        for (int i = 0; i < Zones.WaterZones.Count; i++) {
            WaterZone waterZone = Zones.WaterZones[i];
            if (waterZone.Bounds.Contains(position)) {
                flag = true;
            }
        }
        if (m_inWater != flag) {
            if (m_inWater) {
                OnExitWater();
            } else {
                OnEnterWater();
            }
        }
        m_inWater = flag;
    }

    public bool ShouldThrow() {
        OnReceiveDamage onReceiveDamage = (OnReceiveDamage)Controller.StateMachine.CurrentTrigger;
        return onReceiveDamage.Damage.Type == DamageType.Bash;
    }

    public void OnBashed() {
        OnReceiveDamage onReceiveDamage = (OnReceiveDamage)Controller.StateMachine.CurrentTrigger;
        FlyMovement.Velocity = onReceiveDamage.Damage.Force.normalized * Settings.BashSpeed;
    }

    public bool WasBashed() {
        OnReceiveDamage onReceiveDamage = (OnReceiveDamage)Controller.StateMachine.CurrentTrigger;
        return onReceiveDamage.Damage.Type == DamageType.Bash;
    }

    public bool HitGround() {
        OnCollisionStay onCollisionStay = (OnCollisionStay)Controller.StateMachine.CurrentTrigger;
        ContactPoint[] contacts = onCollisionStay.Collision.contacts;
        foreach (ContactPoint contactPoint in contacts) {
            if (Vector3.Dot(contactPoint.normal, Vector3.up) > Mathf.Cos((float)Math.PI / 3f)) {
                return true;
            }
        }
        return false;
    }

    public new void Start() {
        base.Start();
        State.Idle = new FishIdleState(this);
        State.Swim = new FishSwimState(this);
        State.Attack = new FishAttackState(this);
        State.Bashed = new FishBashedState(this);
        State.Bounce = new FishBounceState(this);
        State.Fall = new FishFallState(this);
        State.Flop = new FishFlopState(this);
        Controller.StateMachine.RegisterStates(State.Idle, State.Swim, State.Attack, State.Bashed, State.Bounce, State.Fall);
        Controller.StateMachine.Configure(State.Idle).AddTransition<OnFixedUpdate>(State.Swim, () => InSwimDistance() && !PlayerIsTooFarFromStartPosition() && PlayerInWater()).AddTransition<OnReceiveDamage>(State.Bashed, WasBashed, OnBashed)
            .AddTransition<OnFixedUpdate>(State.Fall, OutOfWater);
        Controller.StateMachine.Configure(State.Swim).AddTransition<OnFixedUpdate>(State.Attack, () => InAttackDistance() && !PlayerIsTooFarFromStartPosition() && AfterTime(0.5f)).AddTransition<OnFixedUpdate>(State.Idle, () => OutOfSwimDistance() || PlayerIsTooFarFromStartPosition() || !PlayerInWater())
            .AddTransition<OnReceiveDamage>(State.Bashed, WasBashed, OnBashed)
            .AddTransition<OnFixedUpdate>(State.Fall, OutOfWater);
        Controller.StateMachine.Configure(State.Attack).AddTransition<OnFixedUpdate>(State.Idle, () => AfterTime(Settings.AttackDuration) && OutOfSwimDistance()).AddTransition<OnFixedUpdate>(State.Swim, () => AfterTime(Settings.AttackDuration))
            .AddTransition<OnReceiveDamage>(State.Bashed, WasBashed, OnBashed);
        Controller.StateMachine.Configure(State.Bashed).AddTransition<OnFixedUpdate>(State.Idle, () => AfterTime(Settings.BashDuration) && InWater()).AddTransition<OnFixedUpdate>(State.Fall, () => AfterTime(Settings.BashDuration - 0.4f) && OutOfWater())
            .AddTransition<OnReceiveDamage>(State.Bashed, WasBashed, OnBashed);
        Controller.StateMachine.Configure(State.Bounce).AddTransition<OnFixedUpdate>(State.Idle, () => AfterTime(Settings.BounceDuration)).AddTransition<OnReceiveDamage>(State.Bashed, WasBashed, OnBashed);
        Controller.StateMachine.Configure(State.Fall).AddTransition<OnFixedUpdate>(State.Idle, InWater).AddTransition<OnReceiveDamage>(State.Bashed, WasBashed, OnBashed)
            .AddTransition<OnCollisionStay>(State.Flop, HitGround);
        Controller.StateMachine.Configure(State.Flop).AddTransition<OnFixedUpdate>(State.Idle, InWater).AddTransition<OnReceiveDamage>(State.Bashed, WasBashed, OnBashed);
        Controller.StateMachine.ChangeState(State.Idle);
    }

    public void ApplyGravity() {
        FlyMovement.VelocityY -= Settings.Gravity * Time.deltaTime;
    }

    public bool PlayerIsTooFarFromStartPosition() {
        return base.StartPositionToPlayerPosition.magnitude > Settings.MaxSwimDistance;
    }

    public bool InSwimDistance() {
        return base.PositionToPlayerPosition.magnitude < Settings.EnterSwimRange;
    }

    public bool InAttackDistance() {
        return base.PositionToPlayerPosition.magnitude < Settings.AttackRange;
    }

    public bool OutOfSwimDistance() {
        return base.PositionToPlayerPosition.magnitude > Settings.MaxSwimDistance;
    }

    public bool PlayerInWater() {
        return Characters.Sein.Controller.IsSwimming;
    }

    public bool OutOfWater() {
        return !m_inWater;
    }

    public bool InWater() {
        return m_inWater;
    }

    public void UpdateSpriteRotation() {
        float z = Angle + (float)(base.FaceLeft ? 180 : 0);
        Vector3 eulerAngles = Rotation.transform.eulerAngles;
        eulerAngles.z = z;
        Rotation.transform.eulerAngles = eulerAngles;
    }

    public void ApplySoftSpeed(Vector2 speed) {
        FlyMovement.Velocity = Vector3.Lerp(FlyMovement.Velocity, speed, 0.2f);
    }

    public void OnExitWater() {
        if ((bool)Sounds.ExitWater) {
            Sounds.ExitWater.Play();
        }
    }

    public void OnEnterWater() {
        if ((bool)Sounds.EnterWater) {
            Sounds.EnterWater.Play();
        }
    }
}

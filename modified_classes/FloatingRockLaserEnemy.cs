using System;
using fsm;
using fsm.triggers;
using Game;
using UnityEngine;

public class FloatingRockLaserEnemy : Enemy {
    public void PlayAnimationOnce(CharacterAnimationSystem animationSystem, TextureAnimationWithTransitions anim, int layer = 0) {
        if (anim && animationSystem) {
            animationSystem.Play(anim, layer);
        }
    }

    public void RestartAnimationLoop(CharacterAnimationSystem animationSystem, TextureAnimationWithTransitions anim, int layer = 0) {
        if (anim && animationSystem) {
            animationSystem.RestartLoop(anim, layer);
        }
    }

    public void PlayAnimationLoop(CharacterAnimationSystem animationSystem, TextureAnimationWithTransitions anim, int layer = 0) {
        if (anim && animationSystem) {
            animationSystem.PlayLoop(anim, layer);
        }
    }

    public new void Awake() {
        base.Awake();
        EntityDamageReciever damageReciever = DamageReciever;
        damageReciever.OnModifyDamage = (EntityDamageReciever.ModifyDamageDelegate)Delegate.Combine(damageReciever.OnModifyDamage, new EntityDamageReciever.ModifyDamageDelegate(OnModifyDamage));
    }

    public new void OnDestroy() {
        base.OnDestroy();
        EntityDamageReciever damageReciever = DamageReciever;
        damageReciever.OnModifyDamage = (EntityDamageReciever.ModifyDamageDelegate)Delegate.Remove(damageReciever.OnModifyDamage, new EntityDamageReciever.ModifyDamageDelegate(OnModifyDamage));
    }

    public virtual void OnModifyDamage(Damage damage) {
        if (damage.Type == DamageType.SpiritFlame) {
            damage.SetAmount(0f);
        }
    }

    public void OnEnterIdle() {
        RestartAnimationLoop(Animation, Animations.Idle);
        RestartAnimationLoop(AnimationB, AnimationsB.Idle);
        RestartAnimationLoop(AnimationC, AnimationsC.Idle);
        if (IdleSound) {
            IdleSound.Play();
        }
    }

    public void OnExitIdle() {
        if (IdleSound) {
            IdleSound.StopAndFadeOut(0.2f);
        }
    }

    public void OnEnterCharge() {
        RestartAnimationLoop(Animation, Animations.Charging);
        RestartAnimationLoop(AnimationB, AnimationsB.Charging);
        RestartAnimationLoop(AnimationC, AnimationsC.Charging);
        PlaySound(ChargingSound);
        SpawnPrefab(ChargingEffect);
    }

    public void OnEnterLaser() {
        RestartAnimationLoop(Animation, Animations.Laser);
        RestartAnimationLoop(AnimationB, AnimationsB.Laser);
        RestartAnimationLoop(AnimationC, AnimationsC.Laser);
        AimLaserAtPlayer();
        ActivateLaser();
    }

    public void OnExitLaser() {
        DeactivateLaser();
    }

    public void OnEnterShooting() {
        RestartAnimationLoop(Animation, Animations.Shooting);
        RestartAnimationLoop(AnimationB, AnimationsB.Shooting);
        RestartAnimationLoop(AnimationC, AnimationsC.Shooting);
        PlaySound(ShootingSound);
        SpawnPrefab(ShootingEffect);
        ProjectileSpawner.AimAt(Characters.Sein.Controller.Transform);
        Projectile projectile = ProjectileSpawner.SpawnProjectile();
        projectile.GetComponent<DamageDealer>().Damage = Settings.ProjectileDamage;
        Movement.ApplyImpulseForce(Settings.ShootingForce * ProjectileSpawner.Direction * -1f);
    }

    public void UpdateLaserState() {
        Movement.ApplyForce(-Settings.LaserForce * m_laserDirection);
        UpdateLaserDirection();
        UpdateLaser();
    }

    public new void Start() {
        base.Start();
        State.Idle = new State();
        State.Charge = new State();
        State.Laser = new State();
        State.Shooting = new State();
        State idle = State.Idle;
        idle.OnEnterEvent = (Action)Delegate.Combine(idle.OnEnterEvent, new Action(OnEnterIdle));
        State idle2 = State.Idle;
        idle2.OnExitEvent = (Action)Delegate.Combine(idle2.OnExitEvent, new Action(OnExitIdle));
        State charge = State.Charge;
        charge.OnEnterEvent = (Action)Delegate.Combine(charge.OnEnterEvent, new Action(OnEnterCharge));
        State laser = State.Laser;
        laser.OnEnterEvent = (Action)Delegate.Combine(laser.OnEnterEvent, new Action(OnEnterLaser));
        State laser2 = State.Laser;
        laser2.OnExitEvent = (Action)Delegate.Combine(laser2.OnExitEvent, new Action(OnExitLaser));
        State shooting = State.Shooting;
        shooting.OnEnterEvent = (Action)Delegate.Combine(shooting.OnEnterEvent, new Action(OnEnterShooting));
        State laser3 = State.Laser;
        laser3.UpdateStateEvent = (Action)Delegate.Combine(laser3.UpdateStateEvent, new Action(UpdateLaserState));
        Controller.StateMachine.RegisterStates(
            State.Idle,
            State.Charge,
            State.Shooting
        );
        Controller.StateMachine.Configure(State.Idle).AddTransition<OnFixedUpdate>(State.Charge, ShouldCharge);
        Controller.StateMachine.Configure(State.Charge).AddTransition<OnFixedUpdate>(State.Laser, () => AfterTime(Settings.ChargeDuration)).AddTransition<OnFixedUpdate>(State.Idle, () => InCloseDistance());
        Controller.StateMachine.Configure(State.Laser).AddTransition<OnFixedUpdate>(State.Shooting, () => AfterTime(Settings.LaserDuration));
        Controller.StateMachine.Configure(State.Shooting).AddTransition<OnFixedUpdate>(State.Idle, () => AfterTime(Settings.ShootingDuration)).AddTransition<OnFixedUpdate>(State.Idle, () => InCloseDistance());
        Controller.StateMachine.ChangeState(State.Idle);
        ProjectileSpawner.Projectile = Settings.Projectile;
        ProjectileSpawner.Speed = Settings.ProjectileSpeed;
        Laser.Activated = false;
        Laser.gameObject.SetActive(false);
    }

    public void UpdateLaser() {
    }

    public float DesiredLaserRotationDirection {
        get {
            bool flag = Vector3.Dot(PositionToPlayerPosition.normalized, Vector3.Cross(m_laserDirection, Vector3.back)) > 0f;
            return (!flag) ? -1 : 1;
        }
    }

    public void UpdateLaserDirection() {
        if (Controller.NearSein) {
            m_laserRotationSpeed = Mathf.MoveTowards(m_laserRotationSpeed, DesiredLaserRotationDirection, Time.deltaTime * 4f);
        }

        float num = LaserAngleOverTimeCurve.Evaluate(Controller.StateMachine.CurrentStateTime / Settings.LaserDuration);
        float num2 = Laser.CurrentLaserLength / Settings.LaserChaseSpeedDistance;
        float num3 = (!Mathf.Approximately(num2, 0f)) ? (num * Settings.LaserChaseSpeed / num2) : 0f;
        float num4 = MoonMath.Angle.AngleFromVector(m_laserDirection) + m_laserRotationSpeed * RandomizerBonusSkill.TimeScale(Time.deltaTime) * num3;
        m_laserDirection = MoonMath.Angle.VectorFromAngle(num4);
        Laser.transform.eulerAngles = new Vector3(0f, 0f, num4 - 90f);
    }

    public void ActivateLaser() {
        UpdateLaserDirection();
        UpdateLaser();
        Laser.gameObject.SetActive(true);
        Laser.Activated = true;
    }

    public void DeactivateLaser() {
        Laser.Activated = false;
    }

    public void AimLaserAtPlayer() {
        Vector3 vector = Characters.Sein.PlatformBehaviour.PlatformMovement.WorldSpeed;
        m_laserDirection = PositionToPlayerPosition.normalized;
        bool flag = Vector3.Dot(vector.normalized, Vector3.Cross(m_laserDirection, Vector3.forward)) > 0f;
        float num = MoonMath.Angle.AngleFromVector(m_laserDirection);
        num += ((!flag) ? -1 : 1) * Settings.LaserAngularOffset;
        m_laserDirection = MoonMath.Angle.VectorFromAngle(num);
        m_laserRotationSpeed = DesiredLaserRotationDirection;
    }

    public new void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        Movement.ApplySpringForce(Settings.SpringForce, StartPosition);
        Movement.ApplyDrag(Settings.Drag);
    }

    public bool ShouldCharge() {
        return Controller.NearSein && !InCloseDistance();
    }

    public bool InCloseDistance() {
        return PositionToPlayerPosition.magnitude < Settings.CloseDistance;
    }

    public States State = new States();

    public FloatingRockLaserEnemySettings Settings;

    public FloatingRockLaserEnemyAnimations Animations;

    public FloatingRockLaserEnemyAnimations AnimationsB;

    public FloatingRockLaserEnemyAnimations AnimationsC;

    public CharacterAnimationSystem AnimationB;

    public CharacterAnimationSystem AnimationC;

    public PrefabSpawner ChargingEffect;

    public PrefabSpawner ShootingEffect;

    public ProjectileSpawner ProjectileSpawner;

    public RigidbodyMovement Movement;

    public SoundSource ChargingSound;

    public SoundSource ShootingSound;

    public SoundSource LaserSound;

    public SoundSource IdleSound;

    public SoundSource LaserHitSound;

    public AnimationCurve LaserThicknessCurve;

    public AnimationCurve LaserAngleOverTimeCurve;

    public BlockableLaser Laser;

    public LayerMask LaserLayerMask;

    private float m_laserSpeed;

    private Vector3 m_laserDirection;

    private float m_laserRotationSpeed;

    private Vector3 m_laserStartPosition;

    public class States {
        public State Idle;

        public State Charge;

        public State Laser;

        public State Shooting;
    }
}

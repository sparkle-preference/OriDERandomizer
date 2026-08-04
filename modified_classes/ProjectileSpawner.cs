using System.Collections.Generic;
using Core;
using UnityEngine;

public class ProjectileSpawner : SaveSerialize, ISuspendable {
    public Vector3 Position => transform.position;

    public float TimeSinceLastShot { get; set; }

    public override void Awake() {
        TimeSinceLastShot = float.MaxValue;
        base.Awake();
        SuspensionManager.Register(this);
    }

    public override void OnDestroy() {
        base.OnDestroy();
        SuspensionManager.Unregister(this);
    }

    public void Start() {
        TimedTrigger = GetComponent<TimedTrigger>();
        if (TimedTrigger != null) {
            trueTimedDuration = TimedTrigger.Duration;
        }

        transform = base.transform;
    }

    private bool TimerPaused {
        get => TimedTrigger && TimedTrigger.Paused;
        set {
            if (TimedTrigger) {
                TimedTrigger.Paused = value;
            }
        }
    }

    public void OnDisable() {
        TimerPaused = false;
    }

    public void OnTimedTrigger() {
        SpawnProjectile();
    }

    public Projectile SpawnProjectile() {
        TimeSinceLastShot = 0f;
        var gameObject = InstantiateUtility.Instantiate(Projectile) as GameObject;
        gameObject.transform.SetParentMaintainingLocalTransform(transform.root);
        lastProjectile = gameObject;
        gameObject.transform.position = transform.position;
        var component = gameObject.GetComponent<Projectile>();
        component.Speed = Speed;
        component.Direction = Direction;
        if (Direction == Vector3.zero) {
            component.Direction = transform.up;
        }

        component.Gravity = Gravity;
        if (Owner) {
            component.Owner = Owner;
        }

        if (SpawnSound) {
            Sound.Play(SpawnSound, transform.position, null, SpawnSoundVolume, null);
        }

        return component;
    }

    public void AimAt(Transform target) {
        Direction = (target.position - transform.position).normalized;
    }

    public override void Serialize(Archive ar) {
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (trueTimedDuration != null) {
            TimedTrigger.Duration = trueTimedDuration.Value / RandomizerBonusSkill.TimeScale(1f);
        }

        if (InstantiateUtility.IsDestroyed(lastProjectile)) {
            lastProjectile = null;
        }

        if (WaitForProjectileToBeDestroyed && !TimerPaused && lastProjectile != null) {
            TimerPaused = true;
        }

        if (WaitForProjectileToBeDestroyed && TimerPaused && lastProjectile == null) {
            TimerPaused = false;
        }

        TimeSinceLastShot += Time.deltaTime;
    }

    public bool IsSuspended { get; set; }

    public float Speed;

    public Vector3 Direction = Vector3.zero;

    public float Gravity;

    public GameObject Projectile;

    public List<Collider> CollidersToIgnore;

    public GameObject Owner;

    public bool WaitForProjectileToBeDestroyed;

    public AudioClip SpawnSound;

    public float SpawnSoundVolume = 0.3f;

    protected TimedTrigger TimedTrigger;

    private GameObject lastProjectile;

    private new Transform transform;

    private float? trueTimedDuration;
}

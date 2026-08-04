using System;
using Game;
using UnityEngine;

public class SpiritGrenade : MonoBehaviour, IDamageReciever, IAttackable, IBashAttackable, ISuspendable {
    public bool IsInsideSpiritTorch => ignitableTorch != null;

    public void Awake() {
        var damageDealer = DamageDealer;
        damageDealer.OnDamageDealtEvent = (Action<GameObject, Damage>)Delegate.Combine(damageDealer.OnDamageDealtEvent, new Action<GameObject, Damage>(OnDamageDealt));
        var damageDealer2 = DamageDealer;
        damageDealer2.ShouldDealDamage = (Func<GameObject, bool>)Delegate.Combine(damageDealer2.ShouldDealDamage, new Func<GameObject, bool>(ShouldDealDamage));
        SuspensionManager.Register(this);
        Targets.Attackables.Add(this);
        rigidbody = GetComponent<Rigidbody>();
    }

    public void Start() {
        time = 0f;
    }

    public void OnDestroy() {
        var damageDealer = DamageDealer;
        damageDealer.OnDamageDealtEvent = (Action<GameObject, Damage>)Delegate.Remove(damageDealer.OnDamageDealtEvent, new Action<GameObject, Damage>(OnDamageDealt));
        var damageDealer2 = DamageDealer;
        damageDealer2.ShouldDealDamage = (Func<GameObject, bool>)Delegate.Remove(damageDealer2.ShouldDealDamage, new Func<GameObject, bool>(ShouldDealDamage));
        SuspensionManager.Unregister(this);
        Targets.Attackables.Remove(this);
    }

    public bool ShouldDealDamage(GameObject target) {
        if (IsInsideSpiritTorch) {
            return false;
        }

        var attackable = target.FindComponent<IAttackable>();
        return attackable as Component && attackable.CanBeGrenaded();
    }

    public void OnDamageDealt(GameObject go, Damage damage) {
        if (!IsInsideSpiritTorch && !go.GetComponent<Projectile>()) {
            Explode();
        }
    }

    public void Explode() {
        InstantiateUtility.Destroy(gameObject);
        InstantiateUtility.Instantiate(Explosion, transform.position, Quaternion.identity);
        HasExploded = true;
    }

    public void SetTrajectory(Vector2 speed) {
        var component = GetComponent<Rigidbody>();
        component.velocity = speed;
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        time += Time.deltaTime;
        if (IsInsideSpiritTorch) {
            rigidbody.velocity = (ignitableTorch.Position - rigidbody.position + new Vector3(0.2f, 0.4f)) * 8f;
            if (time > 0.8f) {
                InstantiateUtility.Destroy(gameObject);
            }
        } else {
            rigidbody.velocity += Vector3.down * Gravity * Time.deltaTime;
            var ignitableSpiritTorch = IgnitableSpiritTorch.IgniteAnyTorchesNearPosition(transform.position);
            if (ignitableSpiritTorch) {
                ignitableTorch = ignitableSpiritTorch;
                time = 0f;
                return;
            }

            if (time > Duration) {
                Explode();
            }
        }

        if (WaterZone.PositionInWater(Position)) {
            rigidbody.velocity *= 0.9f;
        }
    }

    public Vector3 Position => transform.position;

    public bool IsDead() {
        return gameObject.activeSelf;
    }

    public bool CanBeChargeFlamed() {
        return false;
    }

    public bool CanBeChargeDashed() {
        return false;
    }

    public bool CanBeGrenaded() {
        return false;
    }

    public bool CanBeStomped() {
        return false;
    }

    public bool CanBeBashed() {
        return !IsInsideSpiritTorch && Bashable;
    }

    public bool CanBeSpiritFlamed() {
        return false;
    }

    public bool IsStompBouncable() {
        return false;
    }

    public bool CanBeLevelUpBlasted() {
        return false;
    }

    public void OnEnterBash() {
    }

    public void OnBashHighlight() {
    }

    public void OnBashDehighlight() {
    }

    public int BashPriority => 100;

    public bool IsSuspended { get; set; }

    public void OnSpring(float height, Vector2 direction) {
        rigidbody.velocity = direction * MoonMath.Physics.SpeedFromHeightAndGravity(Gravity, height);
    }

    public void OnRecieveDamage(Damage damage) {
        if (damage.Type == DamageType.Spikes || damage.Type == DamageType.Lava || damage.Type == DamageType.Laser || damage.Type == DamageType.Bash) {
            Explode();
        }
    }

    public void OnCollisionEnter(Collision collision) {
        // If the collision causes it explode then the explosion happens before this collision callback.
        var plant = collision.gameObject.GetComponent<PetrifiedPlant>();
        if (plant != null && !HasExploded) {
            Explode();
        }

        var floor = collision.gameObject.GetComponent<StompableFloor>();
        if (RandomizerBonus.EnhancedGrenade && floor != null && !HasExploded) {
            Explode();
        }
    }

    public float Gravity;

    public DamageDealer DamageDealer;

    public GameObject Explosion;

    public float Duration = 4f;

    private float time;

    private Rigidbody rigidbody;

    private IgnitableSpiritTorch ignitableTorch;

    public bool Bashable = true;

    public bool HasExploded;
}

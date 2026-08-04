using System.Collections.Generic;
using Game;
using UnityEngine;

public class GrenadeBurst : MonoBehaviour, IPooled, ISuspendable {
    public void OnPoolSpawned() {
        suspended = false;
        time = 0f;
        waitDelay = 0f;
    }

    public static void IgnoreOnLastInstance(IAttackable attackable) {
        if (lastInstance) {
            lastInstance.damageAttackables.Add(attackable);
        }
    }

    public void Awake() {
        SuspensionManager.Register(this);
    }

    public void OnDestroy() {
        SuspensionManager.Unregister(this);
    }

    public void OnEnable() {
        lastInstance = this;
    }

    public void OnDisable() {
        damageAttackables.Clear();
        if (lastInstance == this) {
            lastInstance = null;
        }
    }

    public void Start() {
        DealDamage();
        time = 0f;
        waitDelay = 0f;
    }

    public void DealDamage() {
        var position = transform.position;
        foreach (var attackable in Targets.Attackables.ToArray()) {
            if (!InstantiateUtility.IsDestroyed(attackable as Component) && !damageAttackables.Contains(attackable) && attackable.CanBeGrenaded()) {
                var position2 = attackable.Position;
                var vector = position2 - position;
                if (vector.magnitude <= BurstRadius + RandomizerBonus.SpiritFlameLevel()) {
                    damageAttackables.Add(attackable);
                    var gameObject = ((Component)attackable).gameObject;
                    new Damage(DamageAmount + 3 * RandomizerBonus.SpiritFlameLevel(), vector.normalized * 3f, position, DamageType.Grenade, this.gameObject).DealToComponents(gameObject);
                    if (!attackable.IsDead()) {
                        var gameObject2 = (GameObject)InstantiateUtility.Instantiate(BurstImpactEffectPrefab, position2, Quaternion.identity);
                        gameObject2.transform.eulerAngles = new Vector3(0f, 0f, MoonMath.Angle.AngleFromVector(vector.normalized));
                        gameObject2.GetComponent<FollowPositionRotation>().SetTarget(gameObject.transform);
                    }
                }
            } else if (RandomizerBonus.EnhancedGrenade && !InstantiateUtility.IsDestroyed(attackable as Component) && !damageAttackables.Contains(attackable) && attackable.CanBeStomped()) {
                var position2 = attackable.Position;
                var vector = position2 - position;
                if (vector.magnitude <= BurstRadius + 1f + RandomizerBonus.SpiritFlameLevel()) {
                    damageAttackables.Add(attackable);
                    var gameObject = ((Component)attackable).gameObject;
                    new Damage(DamageAmount + 3 * RandomizerBonus.SpiritFlameLevel(), vector.normalized * 3f, position, DamageType.Stomp, this.gameObject).DealToComponents(gameObject);
                    if (!attackable.IsDead()) {
                        var gameObject2 = (GameObject)InstantiateUtility.Instantiate(BurstImpactEffectPrefab, position2, Quaternion.identity);
                        gameObject2.transform.eulerAngles = new Vector3(0f, 0f, MoonMath.Angle.AngleFromVector(vector.normalized));
                        gameObject2.GetComponent<FollowPositionRotation>().SetTarget(gameObject.transform);
                    }
                }
            }
        }

        waitDelay = 0.1f;
    }

    public void FixedUpdate() {
        if (suspended) {
            return;
        }

        time += Time.deltaTime;
        waitDelay -= Time.deltaTime;
        if (time < DealDamageDuration && waitDelay <= 0f) {
            DealDamage();
        }
    }

    public bool IsSuspended {
        get => suspended;
        set => suspended = value;
    }

    public float BurstRadius = 5f;

    public float DamageAmount = 10f;

    public GameObject BurstImpactEffectPrefab;

    public float DealDamageDuration = 0.5f;

    private float time;

    private float waitDelay;

    private readonly HashSet<IAttackable> damageAttackables = new HashSet<IAttackable>();

    private static GrenadeBurst lastInstance;

    private bool suspended;
}

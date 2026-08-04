using System.Collections.Generic;
using Game;
using UnityEngine;

public class GrenadeBurst : MonoBehaviour, IPooled, ISuspendable {
    public void OnPoolSpawned() {
        m_suspended = false;
        m_time = 0f;
        m_waitDelay = 0f;
    }

    public static void IgnoreOnLastInstance(IAttackable attackable) {
        if (m_lastInstance) {
            m_lastInstance.m_damageAttackables.Add(attackable);
        }
    }

    public void Awake() {
        SuspensionManager.Register(this);
    }

    public void OnDestroy() {
        SuspensionManager.Unregister(this);
    }

    public void OnEnable() {
        m_lastInstance = this;
    }

    public void OnDisable() {
        m_damageAttackables.Clear();
        if (m_lastInstance == this) {
            m_lastInstance = null;
        }
    }

    public void Start() {
        DealDamage();
        m_time = 0f;
        m_waitDelay = 0f;
    }

    public void DealDamage() {
        var position = transform.position;
        foreach (var attackable in Targets.Attackables.ToArray()) {
            if (!InstantiateUtility.IsDestroyed(attackable as Component) && !m_damageAttackables.Contains(attackable) && attackable.CanBeGrenaded()) {
                var position2 = attackable.Position;
                var vector = position2 - position;
                if (vector.magnitude <= BurstRadius + RandomizerBonus.SpiritFlameLevel()) {
                    m_damageAttackables.Add(attackable);
                    var gameObject = ((Component)attackable).gameObject;
                    new Damage(DamageAmount + 3 * RandomizerBonus.SpiritFlameLevel(), vector.normalized * 3f, position, DamageType.Grenade, this.gameObject).DealToComponents(gameObject);
                    if (!attackable.IsDead()) {
                        var gameObject2 = (GameObject)InstantiateUtility.Instantiate(BurstImpactEffectPrefab, position2, Quaternion.identity);
                        gameObject2.transform.eulerAngles = new Vector3(0f, 0f, MoonMath.Angle.AngleFromVector(vector.normalized));
                        gameObject2.GetComponent<FollowPositionRotation>().SetTarget(gameObject.transform);
                    }
                }
            } else if (RandomizerBonus.EnhancedGrenade && !InstantiateUtility.IsDestroyed(attackable as Component) && !m_damageAttackables.Contains(attackable) && attackable.CanBeStomped()) {
                var position2 = attackable.Position;
                var vector = position2 - position;
                if (vector.magnitude <= BurstRadius + 1f + RandomizerBonus.SpiritFlameLevel()) {
                    m_damageAttackables.Add(attackable);
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

        m_waitDelay = 0.1f;
    }

    public void FixedUpdate() {
        if (m_suspended) {
            return;
        }

        m_time += Time.deltaTime;
        m_waitDelay -= Time.deltaTime;
        if (m_time < DealDamageDuration && m_waitDelay <= 0f) {
            DealDamage();
        }
    }

    public bool IsSuspended {
        get => m_suspended;
        set => m_suspended = value;
    }

    public float BurstRadius = 5f;

    public float DamageAmount = 10f;

    public GameObject BurstImpactEffectPrefab;

    public float DealDamageDuration = 0.5f;

    private float m_time;

    private float m_waitDelay;

    private readonly HashSet<IAttackable> m_damageAttackables = new HashSet<IAttackable>();

    private static GrenadeBurst m_lastInstance;

    private bool m_suspended;
}

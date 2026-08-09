using System.Collections.Generic;
using Game;
using UnityEngine;

public class ChargeFlameBurst : MonoBehaviour, IPooled, ISuspendable {
    public void OnPoolSpawned() {
        m_suspended = false;
        m_simultaneousEnemies = 0;
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
        m_simultaneousEnemies = 0;
        m_waitDelay = 0f;
    }

    public void DealDamage() {
        var position = transform.position;
        var array = Targets.Attackables.ToArray();
        for (var i = 0; i < array.Length; i++) {
            var attackable = array[i];
            if (!InstantiateUtility.IsDestroyed(attackable as Component) && !m_damageAttackables.Contains(attackable) && attackable.CanBeChargeFlamed()) {
                var position2 = attackable.Position;
                var vector = position2 - position;
                if (Characters.Sein.Abilities.ChargeFlame.CapturedProjectiles.ContainsKey(attackable)) {
                    vector = Characters.Sein.Abilities.ChargeFlame.CapturedProjectiles[attackable].Direction;
                    (attackable as Projectile).Speed = Characters.Sein.Abilities.ChargeFlame.CapturedProjectiles[attackable].CapturedVelocity;
                    Characters.Sein.Abilities.ChargeFlame.CapturedProjectiles.Remove(attackable);
                    (attackable as Projectile).GetComponent<Collider>().enabled = true;
                }

                if (vector.magnitude <= BurstRadius) {
                    m_damageAttackables.Add(attackable);
                    var gameObject = ((Component)attackable).gameObject;
                    new Damage(DamageAmount + 6 * RandomizerBonus.SpiritFlameLevel(), vector.normalized * 3f, position, DamageType.ChargeFlame, this.gameObject).DealToComponents(gameObject);
                    var expr_D8 = attackable.IsDead();
                    if (!expr_D8) {
                        var expr_F2 = (GameObject)InstantiateUtility.Instantiate(BurstImpactEffectPrefab, position2, Quaternion.identity);
                        expr_F2.transform.eulerAngles = new Vector3(0f, 0f, MoonMath.Angle.AngleFromVector(vector.normalized));
                        expr_F2.GetComponent<FollowPositionRotation>().SetTarget(gameObject.transform);
                    }

                    if (expr_D8 && attackable is IChargeFlameAttackable && ((IChargeFlameAttackable)attackable).CountsTowardsPowerOfLightAchievement()) {
                        m_simultaneousEnemies++;
                    }
                }
            }
        }

        if (m_simultaneousEnemies >= 4) {
            AchievementsController.AwardAchievement(Characters.Sein.Abilities.ChargeFlame.KillEnemiesSimultaneouslyAchievement);
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

    private int m_simultaneousEnemies;

    private static ChargeFlameBurst m_lastInstance;

    private bool m_suspended;
}

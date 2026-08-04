using System.Collections.Generic;
using Game;
using UnityEngine;

public class ChargeFlameBurst : MonoBehaviour, IPooled, ISuspendable {
    public void OnPoolSpawned() {
        suspended = false;
        simultaneousEnemies = 0;
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
        simultaneousEnemies = 0;
        waitDelay = 0f;
    }

    public void DealDamage() {
        var position = transform.position;
        var array = Targets.Attackables.ToArray();
        for (var i = 0; i < array.Length; i++) {
            var attackable = array[i];
            if (!InstantiateUtility.IsDestroyed(attackable as Component) && !damageAttackables.Contains(attackable) && attackable.CanBeChargeFlamed()) {
                var position2 = attackable.Position;
                var vector = position2 - position;
                if (Characters.Sein.Abilities.ChargeFlame.CapturedProjectiles.ContainsKey(attackable)) {
                    vector = Characters.Sein.Abilities.ChargeFlame.CapturedProjectiles[attackable].Direction;
                    (attackable as Projectile).Speed = Characters.Sein.Abilities.ChargeFlame.CapturedProjectiles[attackable].CapturedVelocity;
                    Characters.Sein.Abilities.ChargeFlame.CapturedProjectiles.Remove(attackable);
                    (attackable as Projectile).GetComponent<Collider>().enabled = true;
                }

                if (vector.magnitude <= BurstRadius) {
                    damageAttackables.Add(attackable);
                    var gameObject = ((Component)attackable).gameObject;
                    new Damage(DamageAmount + 6 * RandomizerBonus.SpiritFlameLevel(), vector.normalized * 3f, position, DamageType.ChargeFlame, this.gameObject).DealToComponents(gameObject);
                    var exprD8 = attackable.IsDead();
                    if (!exprD8) {
                        var exprF2 = (GameObject)InstantiateUtility.Instantiate(BurstImpactEffectPrefab, position2, Quaternion.identity);
                        exprF2.transform.eulerAngles = new Vector3(0f, 0f, MoonMath.Angle.AngleFromVector(vector.normalized));
                        exprF2.GetComponent<FollowPositionRotation>().SetTarget(gameObject.transform);
                    }

                    if (exprD8 && attackable is IChargeFlameAttackable && ((IChargeFlameAttackable)attackable).CountsTowardsPowerOfLightAchievement()) {
                        simultaneousEnemies++;
                    }
                }
            }
        }

        if (simultaneousEnemies >= 4) {
            AchievementsController.AwardAchievement(Characters.Sein.Abilities.ChargeFlame.KillEnemiesSimultaneouslyAchievement);
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

    private int simultaneousEnemies;

    private static ChargeFlameBurst lastInstance;

    private bool suspended;
}

using System;
using Game;
using UnityEngine;

public class EntityDamageReciever : DamageReciever, IDynamicGraphicHierarchy, IProjectileDetonatable {
    public new void OnValidate() {
        Entity = transform.FindComponentUpwards<Entity>();
        Entity.DamageReciever = this;
        base.OnValidate();
    }

    public new void Awake() {
        base.Awake();
        if (Entity == null) {
            OnValidate();
        }
    }

    public override GameObject DisableTarget => Entity.gameObject;

    public override void OnPoolSpawned() {
        OnModifyDamage = delegate { };
        OnEntityDeathEvent = delegate { };
        base.OnPoolSpawned();
    }

    public void OnTriggerEnter(Collider collider) {
        if (CanBeCrushed && collider.GetComponent<CrushPlayer>()) {
            var damage = new Damage(10000f, Vector2.zero, Entity.Position, DamageType.Crush, gameObject);
            damage.DealToComponents(gameObject);
        }
    }

    public override void OnRecieveDamage(Damage damage) {
        var terrain = damage.Type == DamageType.Crush || damage.Type == DamageType.Spikes || damage.Type == DamageType.Lava || damage.Type == DamageType.Laser;
        if (Entity is Enemy && !(terrain || damage.Type == DamageType.Projectile || damage.Type == DamageType.Enemy)) {
            RandomizerBonus.DamageDealt(damage.Amount);
        }

        OnModifyDamage(damage);
        if (damage.Type == DamageType.Enemy) {
            return;
        }

        if (damage.Type == DamageType.Projectile) {
            damage.SetAmount(damage.Amount * 4f);
        }

        if (damage.Type == DamageType.Spikes || damage.Type == DamageType.Lava) {
            damage.SetAmount(1000f);
        }

        if (Entity.gameObject != gameObject) {
            damage.DealToComponents(Entity.gameObject);
        }

        base.OnRecieveDamage(damage);
        if (NoHealthLeft) {
            OnEntityDeathEvent(Entity);
            if (damage.Type == DamageType.Projectile && Entity is Enemy) {
                var component = damage.Sender.GetComponent<Projectile>();
                if (component != null && component.HasBeenBashedByOri) {
                    AchievementsLogic.Instance.OnProjectileKilledEnemy();
                }

                if (component != null && !component.HasBeenBashedByOri) {
                    AchievementsLogic.Instance.OnEnemyKilledAnotherEnemy();
                }
            }

            if (terrain) {
                var type = Entity.GetType();
                if (type != typeof(DropSlugEnemy) && type != typeof(KamikazeSootEnemy) && !gameObject.name.ToLower().Contains("wall")) {
                    AchievementsLogic.Instance.OnEnemyKilledItself();
                }
            }

            BingoController.OnDestroyEntity(Entity, damage);
            if (Entity is Enemy) {
                RandomizerStatsManager.OnKill(damage.Type);
                if (damage.Type == DamageType.ChargeFlame) {
                    if (Characters.Sein && Characters.Sein.Abilities.Dash) {
                        if (Characters.Sein.Abilities.Dash.CurrentState == SeinDashAttack.State.ChargeDashing) {
                            AchievementsLogic.Instance.OnChargeDashKilledEnemy();
                        } else {
                            AchievementsLogic.Instance.OnChargeFlameKilledEnemy();
                        }
                    } else {
                        AchievementsLogic.Instance.OnChargeFlameKilledEnemy();
                    }
                } else if ((damage.Type == DamageType.Stomp && damage.Force.y < 0f) || damage.Type == DamageType.StompBlast) {
                    AchievementsLogic.Instance.OnStompKilledEnemy();
                } else if (damage.Type == DamageType.SpiritFlameSplatter || damage.Type == DamageType.SpiritFlame) {
                    AchievementsLogic.Instance.OnSpiritFlameKilledEnemy();
                } else if (damage.Type == DamageType.Grenade) {
                    AchievementsLogic.Instance.OnGrenaedKilledEnemy();
                }
            }

            if (Entity is PetrifiedPlant) {
                RandomizerLocationManager.GivePickup(Entity.MoonGuid);
            }
        }
    }

    public bool CanDetonateProjectiles() {
        return IgnoreDamageCondition == null || !IgnoreDamageCondition(null);
    }

    public Entity Entity;

    public ModifyDamageDelegate OnModifyDamage = delegate { };

    public static Action<Entity> OnEntityDeathEvent = delegate { };

    public bool CanBeCrushed = true;

    public delegate void ModifyDamageDelegate(Damage d);
}

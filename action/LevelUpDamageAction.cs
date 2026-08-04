using System.Collections.Generic;
using Game;
using UnityEngine;

public class LevelUpDamageAction : ActionMethod, ISuspendable {
    public override void Perform(IContext context) {
        active = true;
    }

    public override void Awake() {
        base.Awake();
        SuspensionManager.Register(this);
    }

    public override void OnDestroy() {
        base.OnDestroy();
        SuspensionManager.Unregister(this);
    }

    public void FixedUpdate() {
        if (!active) {
            return;
        }

        time += Time.deltaTime;
        delayTime -= Time.deltaTime;
        if (delayTime < 0f) {
            delayTime = 0.1f;
            var num = DistanceOverTime.Evaluate(time);
            var attackables = Targets.Attackables;
            for (var i = 0; i < attackables.Count; i++) {
                var attackable = attackables[i];
                if (!InstantiateUtility.IsDestroyed(attackable as Component) && !TeleporterController.IsTeleporting) {
                    if (attackable.CanBeLevelUpBlasted()) {
                        if (!this.attackables.Contains(attackable)) {
                            if (Vector3.Distance(transform.position, attackable.Position) <= num) {
                                this.attackables.Add(attackable);
                                var damage = new Damage(Damage, (attackable.Position - transform.position).normalized, attackable.Position, DamageType.LevelUp, gameObject);
                                damage.DealToComponents((attackable as Component).gameObject);
                            }
                        }
                    }
                }
            }
        }

        if (time > Duration) {
            active = false;
            time = 0f;
            attackables.Clear();
        }
    }

    public bool IsSuspended { get; set; }

    private readonly HashSet<IAttackable> attackables = new HashSet<IAttackable>();

    private bool active;

    private float time;

    public AnimationCurve DistanceOverTime;

    public float Duration;

    public int Damage;

    private float delayTime;
}

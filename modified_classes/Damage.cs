using UnityEngine;

public class Damage {
    public Damage(float amount, Vector2 force, Vector3 position, DamageType type, GameObject sender) {
        this.amount = amount;
        this.force = force;
        this.position = position;
        this.type = type;
        this.sender = sender;
        if (type == DamageType.SpiritFlame) {
            this.amount += RandomizerBonus.SpiritFlameLevel();
        }
    }

    public float Amount => amount;

    public Vector2 Force => force;

    public Vector3 Position => position;

    public DamageType Type => type;

    public GameObject Sender => sender;

    public void SetAmount(float amount) {
        this.amount = amount;
    }

    public void DealToComponents(GameObject target) {
        if (target != null) {
            target.SendMessage("OnRecieveDamage", this, SendMessageOptions.DontRequireReceiver);
        }
    }

    private float amount;

    private Vector2 force;

    private Vector3 position;

    private DamageType type;

    private GameObject sender;
}

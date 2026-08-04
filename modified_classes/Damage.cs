using UnityEngine;

public class Damage {
    public Damage(float amount, Vector2 force, Vector3 position, DamageType type, GameObject sender) {
        m_amount = amount;
        m_force = force;
        m_position = position;
        m_type = type;
        m_sender = sender;
        if (type == DamageType.SpiritFlame) {
            m_amount += RandomizerBonus.SpiritFlameLevel();
        }
    }

    public float Amount => m_amount;

    public Vector2 Force => m_force;

    public Vector3 Position => m_position;

    public DamageType Type => m_type;

    public GameObject Sender => m_sender;

    public void SetAmount(float amount) {
        m_amount = amount;
    }

    public void DealToComponents(GameObject target) {
        if (target != null) {
            target.SendMessage("OnRecieveDamage", this, SendMessageOptions.DontRequireReceiver);
        }
    }

    private float m_amount;

    private Vector2 m_force;

    private Vector3 m_position;

    private DamageType m_type;

    private GameObject m_sender;
}

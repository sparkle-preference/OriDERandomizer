using System;
using System.Collections.Generic;
using UnityEngine;

public class DamageDealer : MonoBehaviour {
    public virtual float AmountOfDamage(GameObject target) {
        return Damage;
    }

    public void Start() {
    }

    public void OnTriggerStay(Collider collider) {
        if (GameController.FreezeFixedUpdate) {
            return;
        }

        var attachedRigidbody = collider.attachedRigidbody;
        if (attachedRigidbody) {
            OnCollision(attachedRigidbody.gameObject);
        }
    }

    public void OnCollisionStay(Collision collision) {
        if (GameController.FreezeFixedUpdate) {
            return;
        }

        var attachedRigidbody = collision.collider.attachedRigidbody;
        if (attachedRigidbody) {
            OnCollision(attachedRigidbody.gameObject);
        }
    }

    public void OnCollision(GameObject collided) {
        if (!Activated) {
            return;
        }

        if (!collided.activeInHierarchy) {
            return;
        }

        if (!gameObject.activeInHierarchy) {
            return;
        }

        if (PlayerOnly) {
            if (s_oriMask == -1) {
                s_oriMask = LayerMask.NameToLayer("character");
            }

            if (collided.layer != s_oriMask || (!collided.GetComponent<SeinDamageReciever>() && !collided.GetComponent<SpiritGrenadeDamageDealer>())) {
                return;
            }
        }

        DealDamage(collided);
    }

    public virtual void DealDamage(GameObject target) {
        if (GetComponent<Collider>() && !GetComponent<Collider>().enabled) {
            return;
        }

        if (InstantiateUtility.IsDestroyed(target)) {
            return;
        }

        if (Condition && !Condition.Validate(null)) {
            return;
        }

        if (ShouldDealDamage != null && !ShouldDealDamage(target)) {
            return;
        }

        Vector2 vector = target.transform.position - transform.position;
        var damage = new Damage(AmountOfDamage(target), vector.normalized, transform.position, DamageType, gameObject);
        var flag = false;
        var damageReciever = target.FindComponent<IDamageReciever>();
        if (damageReciever != null) {
            damageReciever.OnRecieveDamage(damage);
            flag = true;
        }

        if (flag) {
            OnDamageDealtEvent(target, damage);
        }
    }

    public float Damage;

    public DamageType DamageType;

    public bool Activated = true;

    public bool PlayerOnly;

    public Action<GameObject, Damage> OnDamageDealtEvent = delegate {
    };

    public Func<GameObject, bool> ShouldDealDamage;

    public Condition Condition;

    private static int s_oriMask = -1;

    private List<Collider> m_colliders = new List<Collider>();
}

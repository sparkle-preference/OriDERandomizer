using System;
using UnityEngine;

public class RollingMovement : SaveSerialize, ISuspendable {
    public event Action<Vector3, float, Collider> OnCollisionGroundEvent = delegate { };

    public event Action<Vector3, float, Collider> OnCollisionWallLeftEvent = delegate { };

    public event Action<Vector3, float, Collider> OnCollisionWallRightEvent = delegate { };

    public float SpeedY {
        get { return Speed.y; }
        set { Speed.y = value; }
    }

    public float SpeedX {
        get { return Speed.x; }
        set { Speed.x = value; }
    }

    public float GroundAngle {
        get { return 57.29578f * Mathf.Atan2(-GroundNormal.x, GroundNormal.y); }
    }

    public Vector2 WorldToGround(Vector2 world) {
        return MoonMath.Angle.Unrotate(world, GroundAngle);
    }

    public Vector2 GroundToWorld(Vector2 local) {
        return MoonMath.Angle.Rotate(local, GroundAngle);
    }

    public bool IsSuspended { get; set; }

    public new void Awake() {
        base.Awake();
        m_rigidbody = GetComponent<Rigidbody>();
        m_rigidbody.sleepThreshold = 0f;
        SuspensionManager.Register(this);
    }

    public override void OnDestroy() {
        SuspensionManager.Unregister(this);
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref Speed);
    }

    public void OnCollisionEnter(Collision collision) {
        OnCollision(collision);
    }

    public void OnCollisionStay(Collision collision) {
        OnCollision(collision);
    }

    public void OnCollision(Collision collision) {
        foreach (ContactPoint contactPoint in collision.contacts) {
            Speed -= Vector3.Dot(Speed.normalized, contactPoint.normal) * contactPoint.normal;
            if (Vector3.Dot(contactPoint.normal, Vector3.up) > Mathf.Cos(0.7853982f)) {
                m_groundNormal += contactPoint.normal;
                Ground.FutureOn = true;
                OnCollisionGroundEvent(contactPoint.normal, Vector3.Dot(collision.relativeVelocity, contactPoint.normal), collision.collider);
            }

            if (Vector3.Dot(contactPoint.normal, Vector3.right) > Mathf.Cos(0.34906584f)) {
                WallLeft.FutureOn = true;
                OnCollisionWallLeftEvent(contactPoint.normal, Vector3.Dot(collision.relativeVelocity, contactPoint.normal), collision.collider);
            }

            if (Vector3.Dot(contactPoint.normal, Vector3.left) > Mathf.Cos(0.34906584f)) {
                WallRight.FutureOn = true;
                OnCollisionWallRightEvent(contactPoint.normal, Vector3.Dot(collision.relativeVelocity, contactPoint.normal), collision.collider);
            }
        }
    }

    public Vector3 GroundBinormal {
        get { return Vector3.Cross(GroundNormal, Vector3.forward); }
    }

    public void FixedUpdate() {
        Ground.Update();
        WallLeft.Update();
        WallRight.Update();
        GroundNormal = ((m_groundNormal.magnitude != 0f) ? m_groundNormal.normalized : Vector3.up);
        IsOnGround = (m_groundNormal.magnitude != 0f);
        m_groundNormal = Vector3.zero;
        Speed.z = 0f;
        m_rigidbody.velocity = ((!IsSuspended) ? RandomizerBonusSkill.TimeScale(Speed) : Vector3.zero);
        m_rigidbody.detectCollisions = true;
    }

    private Rigidbody m_rigidbody;

    public Vector3 Speed;

    private Vector3 m_groundNormal;

    public Vector3 GroundNormal;

    public bool IsOnGround;

    public IsOnCollisionState WallLeft = new IsOnCollisionState();

    public IsOnCollisionState WallRight = new IsOnCollisionState();

    public IsOnCollisionState Ground = new IsOnCollisionState();
}

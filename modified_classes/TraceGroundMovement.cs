using UnityEngine;

public class TraceGroundMovement : SaveSerialize, IDamageReciever, ISuspendable {
    public float Speed { get; set; }

    public override void Awake() {
        rigidbody = GetComponent<Rigidbody>();
        SuspensionManager.Register(this);
        base.Awake();
    }

    public override void OnDestroy() {
        base.OnDestroy();
        SuspensionManager.Unregister(this);
    }

    public Vector3 Right => Vector3.Cross(Vector3.back, floorNormal);

    public Vector3 Left => -Right;

    public Vector3 Up => floorNormal;

    public Vector3 Down => -Up;

    public void OnCollisionEnter(Collision collision) {
        OnCollision(collision);
    }

    public void OnCollisionStay(Collision collision) {
        OnCollision(collision);
    }

    public void OnCollision(Collision collision) {
        floorNormal = PhysicsHelper.CalculateAverageNormalFromContactPoints(collision.contacts);
        movingGround.SetGround(collision.transform);
        Surface = SurfaceToSoundProviderMap.ColliderMaterialToSurfaceMaterialType(collision.collider);
    }

    public void FixedUpdate() {
        movingGround.Update();
        Kickback.AdvanceTime();
        if (IsSuspended) {
            rigidbody.velocity = Vector3.zero;
            return;
        }

        var num = Speed;
        num += Kickback.CurrentKickbackSpeed;
        rigidbody.velocity = RandomizerBonusSkill.TimeScale(Right * num);
        var eulerAngles = transform.eulerAngles;
        eulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(eulerAngles.z, MoonMath.Angle.AngleFromVector(Right), 0.2f));
        transform.eulerAngles = eulerAngles;
        var vector = transform.position;
        var vector2 = movingGround.CalculateDelta(transform);
        vector.x += RandomizerBonusSkill.TimeScale(vector2.x);
        vector.y += RandomizerBonusSkill.TimeScale(vector2.y);
        var z = eulerAngles.z;
        var b = Mathf.DeltaAngle(z, lastAngle) / Time.deltaTime;
        lastAngle = z;
        CurrentAngularVelocity = Mathf.Lerp(CurrentAngularVelocity, b, 0.5f);
        if (Vector3.Distance(lastPosition, vector) > 0.03f) {
            lastPosition = vector;
            vector -= Down * 0.05f;
            transform.position = vector;
            if (rigidbody.SweepTest(Down, out var raycastHit, 1f)) {
                vector += RandomizerBonusSkill.TimeScale(Down * raycastHit.distance);
            }
        }

        transform.position = vector;
    }

    public void ApplyKickback(float kickbackMultiplier) {
        Kickback.ApplyKickback(kickbackMultiplier);
    }

    public void OnRecieveDamage(Damage damage) {
        if (damage.Type == DamageType.Acid) {
            return;
        }

        if (Vector3.Dot(Right, damage.Force) > 0f) {
            Kickback.ApplyKickback(damage.Force.magnitude);
            return;
        }

        Kickback.ApplyKickback(-damage.Force.magnitude);
    }

    public override void Serialize(Archive ar) {
        transform.position = ar.Serialize(transform.position);
        Speed = ar.Serialize(Speed);
        ar.Serialize(ref floorNormal);
    }

    public bool IsSuspended { get; set; }

    public Kickback Kickback = new Kickback();

    private Vector3 floorNormal = Vector3.up;

    private Rigidbody rigidbody;

    private readonly MovingGroundHelper movingGround = new MovingGroundHelper();

    public SurfaceMaterialType Surface;

    private Vector3 lastPosition;

    private float lastAngle;

    public float CurrentAngularVelocity;
}

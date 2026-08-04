using UnityEngine;

public class PlatformingMovement : PlatformMovement {
    public override bool IsSuspended { get; set; }

    public new void Awake() {
        base.Awake();
        rigidbody = GetComponent<Rigidbody>();
        rigidbody.sleepThreshold = 0f;
    }

    public void OnCollisionEnter(Collision collision) {
        OnCollision(collision);
    }

    public void OnCollisionStay(Collision collision) {
        OnCollision(collision);
    }

    public void OnCollision(Collision collision) {
        for (var i = 0; i < collision.contacts.Length; i++) {
            var contactPoint = collision.contacts[i];
            var vector = WorldToLocal(contactPoint.normal);
            if (IsWallLeft(vector, contactPoint.otherCollider, 30f)) {
                OnCollisionWallLeft(vector, contactPoint.otherCollider);
            }

            if (IsWallRight(vector, contactPoint.otherCollider, 30f)) {
                OnCollisionWallRight(vector, contactPoint.otherCollider);
            }

            if (IsGround(vector, contactPoint.otherCollider, 60f)) {
                groundContactNormal += vector;
                OnCollisionGround(vector, contactPoint.otherCollider);
            }

            if (IsCeiling(vector, contactPoint.otherCollider, 60f)) {
                OnCollisionCeiling(vector, contactPoint.otherCollider);
            }
        }
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            rigidbody.velocity = Vector3.zero;
            if (rigidbody.detectCollisions) {
                rigidbody.detectCollisions = false;
            }
        } else {
            if (!rigidbody.detectCollisions) {
                rigidbody.detectCollisions = true;
            }

            PreFixedUpdate();
            if (groundContactNormal.magnitude == 0f) {
                GroundNormal = Vector3.up;
            } else {
                GroundNormal = groundContactNormal.normalized;
            }

            groundContactNormal = Vector3.zero;
            if (IsOnGround && !Physics.Raycast(new Ray(Position + WorldOffsetToBottomSphereOfCapsuleCollider, GravityDirection), CapsuleCollider.radius * transform.lossyScale.y + 0.5f)) {
                Ground.IsOn = false;
            }

            if (IsOnGround) {
                LocalSpeedY = 0f;
                var position = transform.position;
                transform.position += GroundBinormal * LocalSpeedX * Time.deltaTime;
                transform.position += GroundNormal * 0.02f;
                var vector = (0.04f + Mathf.Abs(LocalSpeedX) * Time.deltaTime) * -GroundNormal;
                if (rigidbody.SweepTest(vector.normalized, out var raycastHit, vector.magnitude)) {
                    transform.position += vector.normalized * (raycastHit.distance + 0.02f);
                } else {
                    transform.position -= GroundNormal * 0.02f;
                }

                if (Time.deltaTime == 0f) {
                    rigidbody.velocity = Vector3.zero;
                } else {
                    rigidbody.velocity = (transform.position - position) / Time.deltaTime;
                }

                rigidbody.position = position;
            } else {
                rigidbody.velocity = RandomizerBonusSkill.TimeScale(WorldSpeed);
            }

            PostFixedUpdate();
        }
    }

    public override void PlaceOnGround(float lift = 0.5f, float distance = 0f) {
        Position += (Vector3)LocalToWorld(Vector3.up * lift);
        if (distance == 0f) {
            distance = 50f;
        } else {
            distance += lift;
        }

        Vector3 vector = LocalToWorld(Vector3.down * distance);
        if (rigidbody.SweepTest(vector.normalized, out var raycastHit, vector.magnitude)) {
            Position += raycastHit.distance * vector.normalized;
        } else {
            Position += (Vector3)LocalToWorld(Vector3.down * 0.5f);
        }
    }

    private Rigidbody rigidbody;

    private Vector2 groundContactNormal;
}

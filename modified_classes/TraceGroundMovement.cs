using UnityEngine;

public class TraceGroundMovement : SaveSerialize, IDamageReciever, ISuspendable
{
	public float Speed { get; set; }

	public override void Awake()
	{
		m_rigidbody = GetComponent<Rigidbody>();
		SuspensionManager.Register(this);
		base.Awake();
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		SuspensionManager.Unregister(this);
	}

	public Vector3 Right => Vector3.Cross(Vector3.back, m_floorNormal);

	public Vector3 Left => -Right;

	public Vector3 Up => m_floorNormal;

	public Vector3 Down => -Up;

	public void OnCollisionEnter(Collision collision)
	{
		OnCollision(collision);
	}

	public void OnCollisionStay(Collision collision)
	{
		OnCollision(collision);
	}

	public void OnCollision(Collision collision)
	{
		m_floorNormal = PhysicsHelper.CalculateAverageNormalFromContactPoints(collision.contacts);
		m_movingGround.SetGround(collision.transform);
		Surface = SurfaceToSoundProviderMap.ColliderMaterialToSurfaceMaterialType(collision.collider);
	}

	public void FixedUpdate()
	{
		m_movingGround.Update();
		Kickback.AdvanceTime();
		if (IsSuspended)
		{
			m_rigidbody.velocity = Vector3.zero;
			return;
		}
		float num = Speed;
		num += Kickback.CurrentKickbackSpeed;
		m_rigidbody.velocity = RandomizerBonusSkill.TimeScale(Right * num);
		Vector3 eulerAngles = transform.eulerAngles;
		eulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(eulerAngles.z, MoonMath.Angle.AngleFromVector(Right), 0.2f));
		transform.eulerAngles = eulerAngles;
		Vector3 vector = transform.position;
		Vector2 vector2 = m_movingGround.CalculateDelta(transform);
		vector.x += RandomizerBonusSkill.TimeScale(vector2.x);
		vector.y += RandomizerBonusSkill.TimeScale(vector2.y);
		float z = eulerAngles.z;
		float b = Mathf.DeltaAngle(z, m_lastAngle) / Time.deltaTime;
		m_lastAngle = z;
		CurrentAngularVelocity = Mathf.Lerp(CurrentAngularVelocity, b, 0.5f);
		if (Vector3.Distance(m_lastPosition, vector) > 0.03f)
		{
			m_lastPosition = vector;
			vector -= Down * 0.05f;
			transform.position = vector;
			RaycastHit raycastHit;
			if (m_rigidbody.SweepTest(Down, out raycastHit, 1f))
			{
				vector += RandomizerBonusSkill.TimeScale(Down * raycastHit.distance);
			}
		}
		transform.position = vector;
	}

	public void ApplyKickback(float kickbackMultiplier)
	{
		Kickback.ApplyKickback(kickbackMultiplier);
	}

	public void OnRecieveDamage(Damage damage)
	{
		if (damage.Type == DamageType.Acid)
		{
			return;
		}
		if (Vector3.Dot(Right, damage.Force) > 0f)
		{
			Kickback.ApplyKickback(damage.Force.magnitude);
			return;
		}
		Kickback.ApplyKickback(-damage.Force.magnitude);
	}

	public override void Serialize(Archive ar)
	{
		transform.position = ar.Serialize(transform.position);
		Speed = ar.Serialize(Speed);
		ar.Serialize(ref m_floorNormal);
	}

	public bool IsSuspended { get; set; }

	public Kickback Kickback = new Kickback();

	private Vector3 m_floorNormal = Vector3.up;

	private Rigidbody m_rigidbody;

	private readonly MovingGroundHelper m_movingGround = new MovingGroundHelper();

	public SurfaceMaterialType Surface;

	private Vector3 m_lastPosition;

	private float m_lastAngle;

	public float CurrentAngularVelocity;
}

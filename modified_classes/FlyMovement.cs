using UnityEngine;

public class FlyMovement : SaveSerialize, IDamageReciever, ISuspendable
{
	public float Speed
	{
		get => Velocity.magnitude;
		set => Velocity = Velocity.normalized * Speed;
	}

	public float Angle
	{
		get => MoonMath.Angle.AngleFromVector(Velocity);
		set => Velocity = Velocity.magnitude * MoonMath.Angle.VectorFromAngle(value);
	}

	public Vector2 VelocityAsDelta
	{
		get => Velocity * Time.deltaTime;
		set => Velocity = Time.deltaTime != 0f ? value / Time.deltaTime : Vector2.zero;
	}

	public Rigidbody Rigidbody => m_rigidbody;

	public override void Awake()
	{
		base.Awake();
		m_rigidbody = GetComponent<Rigidbody>();
		SuspensionManager.Register(this);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		SuspensionManager.Unregister(this);
	}

	public void Start()
	{
	}

	public void FixedUpdate()
	{
		if (IsSuspended)
		{
			m_rigidbody.velocity = Vector3.zero;
			return;
		}
		Kickback.AdvanceTime();
		m_rigidbody.velocity = RandomizerBonusSkill.TimeScale(Velocity + (!HasKickback ? Vector2.zero : Kickback.KickbackVector));
	}

	public void OnRecieveDamage(Damage damage)
	{
		if (HasKickback)
		{
			Kickback.ApplyKickback(damage.Force.magnitude, damage.Force);
		}
	}

	public float VelocityX
	{
		get => Velocity.x;
		set
		{
			Vector2 velocity = Velocity;
			velocity.x = value;
			Velocity = velocity;
		}
	}

	public float VelocityY
	{
		get => Velocity.y;
		set
		{
			Vector2 velocity = Velocity;
			velocity.y = value;
			Velocity = velocity;
		}
	}

	public override void Serialize(Archive ar)
	{
		Velocity = ar.Serialize(Velocity);
		m_rigidbody.velocity = ar.Serialize(m_rigidbody.velocity);
		transform.position = ar.Serialize(transform.position);
	}

	public bool IsSuspended { get; set; }

	public Kickback Kickback;

	public bool HasKickback = true;

	public Vector2 Velocity;

	private Rigidbody m_rigidbody;
}

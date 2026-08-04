using System;
using Core;
using Game;
using UnityEngine;

public class Projectile : MonoBehaviour, IDamageReciever, IAttackable, IChargeFlameAttackable, IStompAttackable, IBashAttackable, IPooled, ISuspendable, IPortalVisitor, IReflectable
{
	Vector3 IPortalVisitor.Speed
	{
		get => Direction;
		set => Direction = value;
	}

	public Vector3 Direction { get; set; }

	public float Speed { get; set; }

	public GameObject LastReflector { get; set; }

	public Vector3 Displacement { get; set; }

	public bool IsSuspended { get; set; }

	public void OnValidate()
	{
		m_onKillRecievers = GetComponentsInChildren(typeof(IKillReciever));
	}

	public void OnPoolSpawned()
	{
		HasBeenBashedByOri = false;
		CurrentTime = 0f;
		Gravity = m_originalGravity;
		Direction = Vector3.left;
		Speed = 0f;
		m_collider.enabled = m_colliderEnabledAtStart;
		m_explode = false;
		m_explodeLater = false;
		m_lastLoop = null;
		LastReflector = null;
		Displacement = Vector3.zero;
		IsSuspended = false;
		Owner = null;
	}

	public void Start()
	{
		if (RotateSpriteToDirection)
		{
			transform.eulerAngles = new Vector3(0f, 0f, MoonMath.Angle.AngleFromVector(Direction));
		}
		if (EnableCollisionGracePeriod)
		{
			m_collider.enabled = false;
		}
	}

	public void Awake()
	{
		m_nullify = delegate
		{
			m_lastLoop = null;
		};
		SuspensionManager.Register(this);
		Direction = Vector3.left;
		Speed = 0f;
		m_collider = GetComponent<Collider>();
		m_colliderEnabledAtStart = m_collider.enabled;
		Rigidbody = GetComponent<Rigidbody>();
		DamageDealer component = GetComponent<DamageDealer>();
		if (component)
		{
			DamageDealer damageDealer = component;
			damageDealer.OnDamageDealtEvent = (Action<GameObject, Damage>)Delegate.Combine(damageDealer.OnDamageDealtEvent, new Action<GameObject, Damage>(OnDamageDealt));
		}
		if (ProjectileLoop)
		{
			m_lastLoop = Sound.Play(ProjectileLoop.GetSound(null), transform.position, m_nullify);
			if (m_lastLoop)
			{
				m_lastLoop.AttachTo = transform;
			}
		}
		m_originalGravity = Gravity;
	}

	public void OnEnable()
	{
		Targets.Attackables.Add(this);
		PortalVistor.All.Add(this);
		CurrentTime = 0f;
		Rigidbody.velocity = Vector3.zero;
	}

	public void OnDisable()
	{
		Targets.Attackables.Remove(this);
		PortalVistor.All.Remove(this);
	}

	public void OnDestroy()
	{
		SuspensionManager.Unregister(this);
	}

	public virtual bool CanBeBashed()
	{
		return CanProjectileBeBashed;
	}

	public bool CanBeSpiritFlamed()
	{
		return false;
	}

	public bool IsStompBouncable()
	{
		return false;
	}

	public bool CanBeLevelUpBlasted()
	{
		return false;
	}

	public void OnEnterBash()
	{
		if (m_lastLoop)
		{
			m_lastLoop.FadeOut(0.3f, true);
		}
	}

	public void OnBashHighlight()
	{
	}

	public void OnBashDehighlight()
	{
	}

	public int BashPriority => 40;

	public void OnRecieveDamage(Damage damage)
	{
		DamageType type = damage.Type;
		switch (type)
		{
		case DamageType.Bash:
			HasBeenBashedByOri = true;
			Direction = damage.Force.normalized;
			if (UseBashSpeed)
			{
				Speed = BashSpeed;
			}
			if (CancelGravityOnBash)
			{
				Gravity = 0f;
			}
			Owner = null;
			return;
		case DamageType.Grenade:
			break;
		default:
			if (type != DamageType.ChargeFlame)
			{
				return;
			}
			break;
		case DamageType.StompBlast:
			Direction = damage.Force.normalized;
			Owner = null;
			return;
		}
		Direction = damage.Force.normalized;
		Owner = null;
	}

	public Vector3 Position
	{
		get => transform.position;
		set => transform.position = value;
	}

	public bool CanBeStomped()
	{
		return true;
	}

	public bool CountsTowardsSuperJumpAchievement()
	{
		return false;
	}

	public bool CanBeChargeFlamed()
	{
		return true;
	}

	public bool CanBeChargeDashed()
	{
		return false;
	}

	public bool CanBeGrenaded()
	{
		return true;
	}

	public bool CountsTowardsPowerOfLightAchievement()
	{
		return false;
	}

	public bool IsDead()
	{
		return false;
	}

	public void OnGoThroughPortal()
	{
	}

	public void OnPortalOverlapEnter()
	{
	}

	public void OnPortalOverlapExit()
	{
	}

	public bool CanBeReflected(float maximumReflectableDamage)
	{
		return true;
	}

	public void OnGrabbed()
	{
	}

	public void OnReleased(float speed, Vector3 direction)
	{
	}

	public void OnDamageDealt(GameObject go, Damage damage)
	{
		if (go == Owner)
		{
			return;
		}
		IProjectileDetonatable projectileDetonatable = go.FindComponent<IProjectileDetonatable>();
		if (projectileDetonatable != null && projectileDetonatable.CanDetonateProjectiles())
		{
			ExplodeProjectile();
		}
	}

	public virtual void FixedUpdate()
	{
		if (IsSuspended)
		{
			Rigidbody.velocity = Vector3.zero;
			return;
		}
		if (EnableCollisionGracePeriod && CurrentTime > CollisionGracePeriod)
		{
			m_collider.enabled = true;
		}
		if (m_lastLoop == null && ProjectileLoop != null)
		{
			m_lastLoop = Sound.Play(ProjectileLoop.GetSound(null), transform.position, m_nullify);
			if (m_lastLoop)
			{
				m_lastLoop.AttachTo = transform;
			}
		}
		CurrentTime += Time.deltaTime;
		if (CurrentTime > MaximumLiveTime)
		{
			m_explode = true;
		}
		if (WaterZone.PositionInWater(Position))
		{
			m_explode = true;
		}
		if (Gravity > 0f)
		{
			SpeedVector += RandomizerBonusSkill.TimeScale(Vector3.down * Gravity * Time.fixedDeltaTime);
		}
		UpdateVelocity();
		if (RotateSpriteToDirection)
		{
			float num = transform.eulerAngles.z;
			num = Mathf.MoveTowardsAngle(num, MoonMath.Angle.AngleFromDirection(Direction), SpriteTurnSpeed * Time.deltaTime);
			transform.eulerAngles = new Vector3(0f, 0f, num);
		}
		if (m_explode)
		{
			ExplodeProjectile();
		}
		if (m_explodeLater)
		{
			m_explode = true;
			m_explodeLater = false;
		}
	}

	public void ExplodeProjectile()
	{
		if (m_lastLoop)
		{
			m_lastLoop.FadeOut(0.3f, true);
		}
		for (int i = 0; i < m_onKillRecievers.Length; i++)
		{
			if (m_onKillRecievers[i])
			{
				((IKillReciever)m_onKillRecievers[i]).OnKill();
			}
		}
		InstantiateUtility.Destroy(gameObject);
	}

	public void OnCollisionEnter(Collision collision)
	{
		m_explodeLater = true;
	}

	public void OnCollisionStay(Collision collision)
	{
		m_explode = true;
	}

	public void UpdateVelocity()
	{
		Vector3 vector = -Vector3.ClampMagnitude(Displacement / Time.deltaTime, 10f);
		Displacement += vector * Time.deltaTime;
		Rigidbody.velocity = RandomizerBonusSkill.TimeScale(Direction * Speed + vector);
	}

	public Vector3 SpeedVector
	{
		get => Speed * Direction;
		set
		{
			Speed = value.magnitude;
			Direction = value.normalized;
		}
	}

	public void UpdateSpeedAndDirection()
	{
		Direction = Rigidbody.velocity.normalized;
		Speed = RandomizerBonusSkill.TimeScale(Rigidbody.velocity.magnitude);
	}

	public GameObject Owner;

	public bool CanProjectileBeBashed = true;

	public float CollisionGracePeriod = 0.5f;

	public bool EnableCollisionGracePeriod;

	public float Gravity;

	public float MaximumLiveTime = 5f;

	public SoundProvider ProjectileLoop;

	public float BashSpeed = 20f;

	public bool UseBashSpeed;

	public bool CancelGravityOnBash;

	public bool RotateSpriteToDirection;

	public float SpriteTurnSpeed = 360f;

	[NonSerialized]
	public bool HasBeenBashedByOri;

	protected float CurrentTime;

	private float m_originalGravity;

	private SoundPlayer m_lastLoop;

	private bool m_explode;

	private bool m_explodeLater;

	private Action m_nullify;

	protected Rigidbody Rigidbody;

	private Collider m_collider;

	private bool m_colliderEnabledAtStart;

	[SerializeField]
	[HideInInspector]
	private Component[] m_onKillRecievers;
}

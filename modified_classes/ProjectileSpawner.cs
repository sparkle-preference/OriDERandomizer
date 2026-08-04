using System.Collections.Generic;
using Core;
using UnityEngine;

public class ProjectileSpawner : SaveSerialize, ISuspendable
{
	public Vector3 Position
	{
		get
		{
			return m_transform.position;
		}
	}

	public float TimeSinceLastShot { get; set; }

	public override void Awake()
	{
		TimeSinceLastShot = float.MaxValue;
		base.Awake();
		SuspensionManager.Register(this);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		SuspensionManager.Unregister(this);
	}

	public void Start()
	{
		m_timedTrigger = GetComponent<TimedTrigger>();
		if (m_timedTrigger != null)
		{
			trueTimedDuration = m_timedTrigger.Duration;
		}
		m_transform = transform;
	}

	private bool TimerPaused
	{
		get
		{
			return m_timedTrigger && m_timedTrigger.Paused;
		}
		set
		{
			if (m_timedTrigger)
			{
				m_timedTrigger.Paused = value;
			}
		}
	}

	public void OnDisable()
	{
		TimerPaused = false;
	}

	public void OnTimedTrigger()
	{
		SpawnProjectile();
	}

	public Projectile SpawnProjectile()
	{
		TimeSinceLastShot = 0f;
		GameObject gameObject = InstantiateUtility.Instantiate(Projectile) as GameObject;
		gameObject.transform.SetParentMaintainingLocalTransform(transform.root);
		m_lastProjectile = gameObject;
		gameObject.transform.position = transform.position;
		Projectile component = gameObject.GetComponent<Projectile>();
		component.Speed = Speed;
		component.Direction = Direction;
		if (Direction == Vector3.zero)
		{
			component.Direction = transform.up;
		}
		component.Gravity = Gravity;
		if (Owner)
		{
			component.Owner = Owner;
		}
		if (SpawnSound)
		{
			Sound.Play(SpawnSound, transform.position, null, SpawnSoundVolume, null);
		}
		return component;
	}

	public void AimAt(Transform target)
	{
		Direction = (target.position - m_transform.position).normalized;
	}

	public override void Serialize(Archive ar)
	{
	}

	public void FixedUpdate()
	{
		if (IsSuspended)
		{
			return;
		}
		if (trueTimedDuration != null)
		{
			m_timedTrigger.Duration = trueTimedDuration.Value / RandomizerBonusSkill.TimeScale(1f);
		}
		if (InstantiateUtility.IsDestroyed(m_lastProjectile))
		{
			m_lastProjectile = null;
		}
		if (WaitForProjectileToBeDestroyed && !TimerPaused && m_lastProjectile != null)
		{
			TimerPaused = true;
		}
		if (WaitForProjectileToBeDestroyed && TimerPaused && m_lastProjectile == null)
		{
			TimerPaused = false;
		}
		TimeSinceLastShot += Time.deltaTime;
	}

	public bool IsSuspended { get; set; }

	public float Speed;

	public Vector3 Direction = Vector3.zero;

	public float Gravity;

	public GameObject Projectile;

	public List<Collider> CollidersToIgnore;

	public GameObject Owner;

	public bool WaitForProjectileToBeDestroyed;

	public AudioClip SpawnSound;

	public float SpawnSoundVolume = 0.3f;

	protected TimedTrigger m_timedTrigger;

	private GameObject m_lastProjectile;

	private Transform m_transform;

	private float? trueTimedDuration;
}

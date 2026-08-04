using System.Collections.Generic;
using Game;
using UnityEngine;

public class LevelUpDamageAction : ActionMethod, ISuspendable
{
	public override void Perform(IContext context)
	{
		m_active = true;
	}

	public override void Awake()
	{
		base.Awake();
		SuspensionManager.Register(this);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		SuspensionManager.Unregister(this);
	}

	public void FixedUpdate()
	{
		if (!m_active)
		{
			return;
		}
		m_time += Time.deltaTime;
		m_delayTime -= Time.deltaTime;
		if (m_delayTime < 0f)
		{
			m_delayTime = 0.1f;
			var num = DistanceOverTime.Evaluate(m_time);
			var attackables = Targets.Attackables;
			for (var i = 0; i < attackables.Count; i++)
			{
				var attackable = attackables[i];
				if (!InstantiateUtility.IsDestroyed(attackable as Component) && !TeleporterController.IsTeleporting)
				{
					if (attackable.CanBeLevelUpBlasted())
					{
						if (!m_attackables.Contains(attackable))
						{
							if (Vector3.Distance(transform.position, attackable.Position) <= num)
							{
								m_attackables.Add(attackable);
								var damage = new Damage(Damage, (attackable.Position - transform.position).normalized, attackable.Position, DamageType.LevelUp, gameObject);
								damage.DealToComponents((attackable as Component).gameObject); 
							}
						}
					}
				}
			}
		}
		if (m_time > Duration)
		{
			m_active = false;
			m_time = 0f;
			m_attackables.Clear();
		}
	}

	public bool IsSuspended { get; set; }

	private readonly HashSet<IAttackable> m_attackables = new HashSet<IAttackable>();

	private bool m_active;

	private float m_time;

	public AnimationCurve DistanceOverTime;

	public float Duration;

	public int Damage;

	private float m_delayTime;
}

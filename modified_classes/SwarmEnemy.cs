using System;
using fsm;
using fsm.triggers;
using UnityEngine;

public class SwarmEnemy : GroundEnemy
{
	public override bool CanBeOptimized()
	{
		return Controller.StateMachine.CurrentState == State.Idle;
	}

	public override void Awake()
	{
		base.Awake();
		DamageReciever.OnDeathEvent.Add(new Action<Damage>(OnDeath));
		EntityDamageReciever damageReciever = DamageReciever;
		damageReciever.OnModifyDamage = (EntityDamageReciever.ModifyDamageDelegate)Delegate.Combine(damageReciever.OnModifyDamage, new EntityDamageReciever.ModifyDamageDelegate(OnPreProcessDamage));
	}

	public void OnPreProcessDamage(Damage damage)
	{
		EntityDamageDealer component = damage.Sender.GetComponent<EntityDamageDealer>();
		if (component != null)
		{
			Entity entity = component.Entity;
			if (entity is SwarmEnemy && GetInstanceID() > entity.GetInstanceID())
			{
				PlatformMovement.LocalSpeedX *= 0.7f;
			}
		}
	}

	public new void Start()
	{
		base.Start();
		State.Idle = new State
		{
			OnEnterEvent = OnEnterIdle,
			UpdateStateEvent = UpdateIdle,
			OnExitEvent = OnExitIdle
		};
		State.Run = new State
		{
			OnEnterEvent = OnEnterRun,
			UpdateStateEvent = UpdateRun,
			OnExitEvent = OnExitRun
		};
		State.Spawned = new State
		{
			OnEnterEvent = OnEnterSpawned,
			UpdateStateEvent = UpdateSpawned
		};
		Controller.StateMachine.RegisterStates(
			State.Idle,
		State.Run,
		State.Spawned
		);
		Controller.StateMachine.Configure(State.Idle).AddTransition<OnFixedUpdate>(State.Run, ShouldRun);
		Controller.StateMachine.Configure(State.Run).AddTransition<OnFixedUpdate>(State.Idle, () => !ShouldRun());
		Controller.StateMachine.Configure(State.Spawned).AddTransition<OnFixedUpdate>(State.Run, () => AfterTime(0.5f));
		Controller.StateMachine.ChangeState((!m_wasSpawned) ? State.Idle : State.Spawned);
	}

	public bool ShouldRun()
	{
		float num = Math.Sign(PositionToPlayerPosition.x);
		bool flag = Size != 0f && Physics.Linecast(transform.position + new Vector3(num * (Size - 1f), 0f), transform.position + new Vector3(num * Size, 0f));
		bool flag2;
		if (EnemyStopper.InsideEnemyStopper(Position, (!PlayerIsToLeft) ? Vector3.right : Vector3.left, out flag2))
		{
			return false;
		}
		return Controller.IsNearSein() && Mathf.Abs(PositionToPlayerPosition.x) > 0.5f && !flag;
	}

	public void SetModeToSpawned()
	{
		m_wasSpawned = true;
	}

	public new void FixedUpdate()
	{
		base.FixedUpdate();
		if (!IsSuspended)
		{
			PlatformMovement.LocalSpeedY -= Settings.Gravity * Time.deltaTime;
			if (PlatformMovement.LocalSpeedY < -Settings.MaxFallSpeed)
			{
				PlatformMovement.LocalSpeedY = -Settings.MaxFallSpeed;
			}
			UpdateRotation();
			if (IsInWater)
			{
				Drown();
			}
		}
	}

	public void UpdateRotation()
	{
		float num = SpeedXToRotation.Evaluate(PlatformMovement.LocalSpeedX) * SpeedYToRotation.Evaluate(PlatformMovement.LocalSpeedX) * AirTiltAngle;
		float b = (!PlatformMovement.IsOnGround) ? num : PlatformMovement.GroundAngle;
		FeetTransform.eulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(FeetTransform.eulerAngles.z, b, 0.1f));
	}

	public void OnEnterIdle()
	{
		if (Idle)
		{
			Idle.Play();
		}
	}

	public void OnEnterRun()
	{
		if (Walking)
		{
			Walking.Play();
		}
	}

	public void OnExitIdle()
	{
		if (Idle)
		{
			Idle.Stop();
		}
	}

	public void OnExitRun()
	{
		if (Walking)
		{
			Walking.Stop();
		}
	}

	public void OnEnterSpawned()
	{
		RestartAnimationLoop(Animations.Spawned);
	}

	public void UpdateIdle()
	{
		if (PlatformMovement.IsOnGround)
		{
			PlayAnimationLoop(Animations.Idle);
		}
		else
		{
			PlayAnimationLoop((!CanFall) ? Animations.Idle : Animations.Fall);
		}
		PlatformMovement.LocalSpeedX = MoonMath.Movement.DecelerateSpeed(PlatformMovement.LocalSpeedX, Settings.Decceleration);
	}

	public void UpdateRun()
	{
		if (PlatformMovement.IsOnGround)
		{
			PlayAnimationLoop((!PlayerIsToLeft) ? Animations.RunRight : Animations.RunLeft);
		}
		else
		{
			PlayAnimationLoop(CanFall ? Animations.Fall : ((!PlayerIsToLeft) ? Animations.RunRight : Animations.RunLeft));
		}
		PlatformMovement.LocalSpeedX = RandomizerBonusSkill.TimeScale(Settings.Speed * Settings.MoveCurve.Evaluate(SpriteAnimator.CurrentAnimationTime) * ((!PlayerIsToLeft) ? 1 : (-1)));
		if (Settings.JumpDelay > 0f)
		{
			if (m_jumpDelay < 0f && PlatformMovement.IsOnGround)
			{
				m_jumpDelay = Settings.JumpDelay;
				PlatformMovement.LocalSpeedY = Settings.JumpStrength;
				PlayAnimationOnce(Animations.Jump, 1);
			}
			m_jumpDelay -= Time.deltaTime;
		}
	}

	public void UpdateSpawned()
	{
	}

	public void OnDeath(Damage damage)
	{
		if (Settings.Child)
		{
			for (int i = 0; i < 2; i++)
			{
				Vector3 velocity = (((i != 0) ? Vector3.right : Vector3.left) + Vector3.up * 3f) * 7f;
				SwarmEnemyManager.Instance.QueueSpawn(transform.position, velocity, (int)(Loot.LootAmount * Loot.LootMultiplier), OrbSpawner, DamageDealer.Damage, Settings.Child, SceneRootGUID, Owner);
			}
		}
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		Owner.OnChildComponentDestroy(this);
	}

	public SwarmEnemyAnimations Animations;

	public SwarmEnemySettings Settings;

	public SwarmEnemyLootSettings Loot;

	public OrbSpawner OrbSpawner;

	public SoundSource Idle;

	public SoundSource Walking;

	public bool CanFall = true;

	public float Size;

	public States State = new States();

	private bool m_wasSpawned;

	public AnimationCurve SpeedXToRotation;

	public AnimationCurve SpeedYToRotation;

	public float AirTiltAngle;

	private float m_jumpDelay;

	public SwarmEnemyPlaceholder Owner;

	public class States
	{
		public State Idle;

		public State Run;

		public State Spawned;

		public State Thrown;

		public State Frozen;
	}
}

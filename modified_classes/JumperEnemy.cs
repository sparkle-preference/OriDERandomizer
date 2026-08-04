using fsm;
using fsm.triggers;
using Game;
using UnityEngine;

public class JumperEnemy : GroundEnemy
{
	public override bool CanBeOptimized()
	{
		return Controller.StateMachine.CurrentState == State.Idle;
	}

	public void ForceAttackPlayer()
	{
		Controller.StateMachine.ChangeState(State.JumpCharge);
	}

	public new void Start()
	{
		base.Start();
		State.Idle = new JumperEnemyIdleState(this);
		State.JumpCharge = new JumperEnemyChargingState(this);
		State.Fall = new JumperEnemyFallState(this);
		State.Thrown = new JumperEnemyThrownState(this);
		State.Stomped = new JumperEnemyStompedState(this);
		State.Stunned = new JumperEnemyStunnedState(this);
		State.Respawn = new State();
		State.Respawn.OnEnterEvent = delegate
		{
			PlayAnimationOnce(Animations.Respawn);
			FacePlayer();
			SpawnPrefab(Settings.RespawnEffect);
		};
		Controller.StateMachine.Configure(State.Idle).AddTransition<OnFixedUpdate>(State.JumpCharge, PlayerInRange).AddTransition<OnFixedUpdate>(State.JumpCharge, OutOfJumpingZone).AddTransition<AttackTriggered>(State.JumpCharge).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, OnStomped).AddTransition<OnReceiveDamage>(State.JumpCharge);
		Controller.StateMachine.Configure(State.JumpCharge).AddTransition<OnFixedUpdate>(State.Fall, () => AfterTime(Settings.ChargingDuration), DoJump).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, OnStomped);
		Controller.StateMachine.Configure(State.Fall).AddTransition<OnFixedUpdate>(State.JumpCharge, () => LandedOnGround() && PlayerInRange(), OnLanded).AddTransition<OnFixedUpdate>(State.Idle, () => LandedOnGround() && !PlayerInRange(), OnLanded).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, OnStomped);
		Controller.StateMachine.Configure(State.Thrown).AddTransition<OnFixedUpdate>(State.Stunned, IsOnGround).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, OnStomped);
		Controller.StateMachine.Configure(State.Stomped).AddTransition<OnFixedUpdate>(State.Stunned, IsOnGround).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, OnStomped);
		Controller.StateMachine.Configure(State.Stunned).AddTransition<OnFixedUpdate>(State.Idle, () => AfterTime(Settings.StunnedDuration)).AddTransition<OnReceiveDamage>(State.Thrown, ShouldThrow, OnThrow).AddTransition<OnReceiveDamage>(State.Stomped, ShouldStomped, OnStomped);
		Controller.StateMachine.Configure(State.Respawn).AddTransition<OnAnimationEnded>(State.Idle);
		Controller.StateMachine.RegisterStates(
			State.Idle,
		State.JumpCharge,
		State.Fall,
		State.Stunned,
		State.Thrown,
		State.Stomped,
		State.Respawn
		);
		if (m_timedRespawn)
		{
			Controller.StateMachine.ChangeState(State.Respawn);
			m_timedRespawn = false;
		}
		else
		{
			Controller.StateMachine.ChangeState(State.Idle);
		}
	}

	public new void OnTimedRespawn()
	{
		m_timedRespawn = true;
	}

	public bool OutOfJumpingZone()
	{
		return !(JumpingZone == null) && !new Rect
		{
			width = JumpingZone.lossyScale.x,
			height = JumpingZone.lossyScale.y,
			center = JumpingZone.position
		}.Contains(Position);
	}

	public bool IsOnGround()
	{
		return PlatformMovement.IsOnGround;
	}

	public bool ShouldThrow()
	{
		var onReceiveDamage = (OnReceiveDamage)Controller.StateMachine.CurrentTrigger;
		return onReceiveDamage.Damage.Type == DamageType.Bash;
	}

	public bool ShouldStomped()
	{
		var onReceiveDamage = (OnReceiveDamage)Controller.StateMachine.CurrentTrigger;
		return onReceiveDamage.Damage.Type == DamageType.StompBlast;
	}

	public void OnThrow()
	{
		var onReceiveDamage = (OnReceiveDamage)Controller.StateMachine.CurrentTrigger;
		PlatformMovement.WorldSpeed = onReceiveDamage.Damage.Force * 10f;
		if (PlatformMovement.IsOnGround && PlatformMovement.LocalSpeedY < 0f)
		{
			PlatformMovement.LocalSpeedY *= -0.5f;
		}
		m_thrownDirection = onReceiveDamage.Damage.Force.normalized;
		FaceLeft = PlatformMovement.LocalSpeedX < 0f;
	}

	public void OnStomped()
	{
		var onReceiveDamage = (OnReceiveDamage)Controller.StateMachine.CurrentTrigger;
		PlatformMovement.WorldSpeed = onReceiveDamage.Damage.Force * 8f;
		if (PlatformMovement.IsOnGround && PlatformMovement.LocalSpeedY < 0f)
		{
			PlatformMovement.LocalSpeedY *= -0.5f;
		}
		m_thrownDirection = onReceiveDamage.Damage.Force.normalized;
		FaceLeft = PlatformMovement.LocalSpeedX < 0f;
	}

	public void DoJump()
	{
		Vector2 localSpeed;
		localSpeed.y = PhysicsHelper.CalculateSpeedFromHeight(Settings.JumpHeight, Settings.Gravity);
		var num = 2f * localSpeed.y / Settings.Gravity;
		localSpeed.x = Settings.JumpDistance / num;
		if (OutOfJumpingZone())
		{
			localSpeed.x = Mathf.Clamp((JumpingZone.position.x - Position.x) / num, -localSpeed.x, localSpeed.x);
			m_shouldStomp = false;
		}
		else
		{
			localSpeed.x = Mathf.Clamp(PositionToPlayerPosition.x / num, -localSpeed.x, localSpeed.x);
			m_shouldStomp = Mathf.Abs((PlayerPosition + m_playerSmoothSpeed - Position).x) < Settings.StompAttackDistance;
		}
		var vector = new Vector3(localSpeed.x * num * 0.5f, Settings.JumpHeight);
		var flag = Mathf.Sign(localSpeed.x) != FaceLeftSign;
		if (Physics.Raycast(new Ray(Position, vector.normalized), vector.magnitude, RaycastLayerMask) || !m_shouldStomp)
		{
			localSpeed.y = PhysicsHelper.CalculateSpeedFromHeight(Settings.ShortJumpHeight, Settings.Gravity);
			Animation.Play(!flag ? Animations.ShortJump : Animations.JumpFlip, 1, () => !PlatformMovement.IsOnGround);
			m_shouldStomp = false;
		}
		else
		{
			Animation.Play(!flag ? Animations.Jump : Animations.JumpFlip, 1, () => !PlatformMovement.IsOnGround);
		}
		PlaySound(Sounds.Jump);
		if (flag)
		{
			FaceLeft = !FaceLeft;
		}
		PlatformMovement.LocalSpeed = localSpeed;
	}

	public bool PlayerInRange()
	{
		return PositionToPlayerPosition.magnitude < Settings.ChargeRange && Controller.NearSein;
	}

	public new void FixedUpdate()
	{
		base.FixedUpdate();
		if (IsSuspended)
		{
			return;
		}
		PlatformMovement.LocalSpeedY -= RandomizerBonusSkill.TimeScale(Time.deltaTime) * Settings.Gravity;
		if (PlatformMovement.LocalSpeedY < -Settings.MaxFallSpeed)
		{
			PlatformMovement.LocalSpeedY = -Settings.MaxFallSpeed;
		}
		if (PlatformMovement.IsOnCeiling)
		{
			PlatformMovement.LocalSpeedY = Mathf.Min(0f, PlatformMovement.LocalSpeedY);
		}
		UpdateRotation();
		if (Characters.Sein)
		{
			m_playerSmoothSpeed = Vector3.Lerp(m_playerSmoothSpeed, Characters.Sein.Speed, 0.1f);
		}
		if (IsInWater)
		{
			Drown();
		}
	}

	public void UpdateRotation()
	{
		var currentState = Controller.StateMachine.CurrentState;
		var currentStateTime = Controller.StateMachine.CurrentStateTime;
		if (currentState == State.Thrown)
		{
			var num = 1f - Mathf.InverseLerp(0.3f, 0.6f, currentStateTime);
			FeetTransform.eulerAngles = new Vector3(0f, 0f, (MoonMath.Angle.AngleFromVector(m_thrownDirection) - 90f) * num);
		}
		else
		{
			var b = !PlatformMovement.IsOnGround ? 0f : PlatformMovement.GroundAngle;
			FeetTransform.eulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(FeetTransform.eulerAngles.z, b, 0.2f));
		}
	}

	public bool LandedOnGround()
	{
		return PlatformMovement.IsOnGround && PlatformMovement.LocalSpeedY <= 0f;
	}

	public void OnLanded()
	{
		if (m_shouldStomp && Settings.HasStompExplosion)
		{
			if (StompEffect)
			{
				var gameObject = (GameObject)InstantiateUtility.Instantiate(StompEffect, Position, Quaternion.identity);
				gameObject.GetComponentInChildren<DamageDealer>().Damage = Settings.ExplosionDamage;
			}
			PlaySound(Sounds.Impact);
		}
		else
		{
			if (LandEffect)
			{
				InstantiateUtility.Instantiate(LandEffect, Position, Quaternion.identity);
			}
			PlaySound(Sounds.Impact);
		}
		var groundCollider = PlatformMovementListOfColliders.GroundCollider;
		var damage = new Damage(Settings.GroundStompDamage, Vector3.down * 3f, transform.position, DamageType.Stomp, this.gameObject);
		if (groundCollider)
		{
			damage.DealToComponents(groundCollider.gameObject);
		}
		PlatformMovement.LocalSpeed = Vector3.zero;
	}

	public JumpingSootEnemyAnimations Animations;

	public JumpingSootEnemySettings Settings;

	public JumpingSootEnemySounds Sounds;

	public States State = new States();

	public Transform JumpingZone;

	public LayerMask RaycastLayerMask;

	private Vector3 m_playerSmoothSpeed;

	private bool m_shouldStomp;

	private Vector3 m_thrownDirection;

	private bool m_timedRespawn;

	public GameObject StompEffect;

	public GameObject LandEffect;

	public class States
	{
		public State Respawn;

		public JumperEnemyIdleState Idle;

		public JumperEnemyChargingState JumpCharge;

		public JumperEnemyFallState Fall;

		public JumperEnemyThrownState Thrown;

		public JumperEnemyStompedState Stomped;

		public JumperEnemyStunnedState Stunned;
	}
}

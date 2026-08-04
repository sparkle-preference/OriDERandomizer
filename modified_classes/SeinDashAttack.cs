using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinDashAttack : CharacterState, ISeinReceiver
{
	static SeinDashAttack()
	{
		OnDashEvent = delegate
		{
		};
		OnWallDashEvent = delegate
		{
		};
	}

	public static event Action OnDashEvent;

	public static event Action OnWallDashEvent;

	public bool HasEnoughEnergy => m_sein.Energy.CanAfford(AdjustedEnergyCost);

	public override void Serialize(Archive ar)
	{
		if (ar.Reading)
		{
			ReturnToNormal();
		}
	}

	public override void OnExit()
	{
		ReturnToNormal();
		base.OnExit();
	}

	public void OnDisable()
	{
		Exit();
	}

	public void ReturnToNormal()
	{
		if (CurrentState != State.Normal)
		{
			if (CurrentState == State.Dashing)
			{
				m_sein.PlatformBehaviour.PlatformMovement.LocalSpeedX = (!m_faceLeft ? 1 : -1) * DashSpeedOverTime.Evaluate(DashSpeedOverTime.length);
			}
			if (CurrentState == State.ChargeDashing)
			{
				m_sein.PlatformBehaviour.PlatformMovement.LocalSpeedX = (!m_faceLeft ? 1 : -1) * ChargeDashSpeedOverTime.Evaluate(ChargeDashSpeedOverTime.length);
			}
			UI.Cameras.Current.ChaseTarget.CameraSpeedMultiplier.x = 1f;
			if (CurrentState == State.ChargeDashing)
			{
				RestoreEnergy();
			}
			ChangeState(State.Normal);
		}
	}

	public void SpendEnergy()
	{
		m_sein.Energy.Spend(AdjustedEnergyCost);
	}

	public void RestoreEnergy()
	{
		m_sein.Energy.Gain(AdjustedEnergyCost);
	}

	public void SetReferenceToSein(SeinCharacter sein)
	{
		m_sein = sein;
		sein.Abilities.Dash = this;
	}

	public override void UpdateCharacterState()
	{
		UpdateState();
	}

	public bool IsDashingOrChangeDashing
	{
		get
		{
			if (CurrentState == State.Dashing)
			{
				return m_stateCurrentTime < DashTime;
			}
			return CurrentState == State.ChargeDashing && m_stateCurrentTime < ChargeDashTime;
		}
	}

	public void ChangeState(State state)
	{
		CurrentState = state;
		m_stateCurrentTime = 0f;
		m_attackablesIgnore.Clear();
	}

	public IChargeDashAttackable FindClosestAttackable
	{
		get
		{
			IChargeDashAttackable result = null;
			var num = float.MaxValue;
			foreach (var attackable in Targets.Attackables)
			{
				if (attackable as Component && attackable.CanBeChargeDashed() && attackable is IChargeDashAttackable)
				{
					var chargeDashAttackable = (IChargeDashAttackable)attackable;
					if (UI.Cameras.Current.IsOnScreen(attackable.Position))
					{
						var magnitude = (attackable.Position - m_sein.Position).magnitude;
						if (magnitude < num && magnitude < ChargeDashTargetMaxDistance)
						{
							result = chargeDashAttackable;
							num = magnitude;
						}
					}
				}
			}
			return result;
		}
	}

	public void AttackNearbyEnemies()
	{
		var i = 0;
		while (i < Targets.Attackables.Count)
		{
			var attackable = Targets.Attackables[i];
			if (!InstantiateUtility.IsDestroyed(attackable as Component) && !m_attackablesIgnore.Contains(attackable) && attackable.CanBeChargeFlamed() && (attackable.Position - m_sein.PlatformBehaviour.PlatformMovement.HeadPosition).magnitude <= 3f)
			{
				m_attackablesIgnore.Add(attackable);
				var v = !m_chargeDashAtTarget ? (!m_faceLeft ? Vector3.right : Vector3.left) * 3f : m_chargeDashDirection * 3f;
				if (RandomizerBonus.EnhancedDash)
				{
					v = m_enhancedDashDirection * 3f;
				}
				new Damage(Damage, v, m_sein.Position, DamageType.ChargeFlame, gameObject).DealToComponents(((Component)attackable).gameObject);
				m_hasHitAttackable = true;
				if (ExplosionEffect && Time.time - m_timeOfLastExplosionEffect > 0.1f)
				{
					m_timeOfLastExplosionEffect = Time.time;
					InstantiateUtility.Instantiate(ExplosionEffect, Vector3.Lerp(transform.position, attackable.Position, 0.5f), Quaternion.identity);
				}
				break;
			}

			i++;
		}
	}

	private void PerformDash(TextureAnimationWithTransitions dashAnimation, SoundProvider dashSound)
	{
		m_sein.Mortality.DamageReciever.ResetInviciblity();
		m_hasDashed = true;
		if (RandomizerBonus.DoubleAirDash() && !RandomizerBonus.DoubleAirDashUsed)
		{
			m_hasDashed = false;
			RandomizerBonus.DoubleAirDashUsed = true;
		}
		m_isOnGround = m_sein.IsOnGround;
		m_lastDashTime = Time.time;
		m_lastPressTime = 0f;
		SpriteRotation = m_sein.PlatformBehaviour.PlatformMovement.GroundAngle;
		m_allowNoDecelerationForThisDash = true;
		if (m_chargeDashAtTarget)
		{
			m_faceLeft = m_chargeDashDirection.x < 0f;
		}
		else if (m_sein.PlatformBehaviour.PlatformMovement.HasWallLeft)
		{
			m_faceLeft = false;
		}
		else if (m_sein.PlatformBehaviour.PlatformMovement.HasWallRight)
		{
			m_faceLeft = true;
		}
		else if (m_sein.Input.NormalizedHorizontal != 0)
		{
			m_faceLeft = m_sein.Input.NormalizedHorizontal < 0;
		}
		else if (!Mathf.Approximately(m_sein.Speed.x, 0f))
		{
			m_faceLeft = m_sein.Speed.x < 0f;
		}
		else
		{
			m_faceLeft = m_sein.FaceLeft;
			m_allowNoDecelerationForThisDash = false;
		}
		m_sein.FaceLeft = m_faceLeft;
		m_stopAnimation = false;
		if (!m_chargeDashAtTarget && RandomizerBonus.EnhancedDash)
		{
			m_enhancedDashDirection = m_faceLeft ? Vector3.left : Vector3.right;

			if (Input.Axis.magnitude > 0f)
			{
				if (!m_sein.IsOnGround)
				{
					m_enhancedDashDirection = Input.Axis.normalized;
				}
				else if (Input.Axis.y > 0f)
				{
					var dot = Vector3.Dot(Input.Axis.normalized, Vector3.left);
					if (dot < 0.94f && dot > -0.94f)
					{
						m_enhancedDashDirection = Input.Axis.normalized;
					}
				}
			}

			SpriteRotation = Mathf.Atan2(m_enhancedDashDirection.y, m_enhancedDashDirection.x) * 57.29578f;
			if (m_faceLeft)
			{
				SpriteRotation = Mathf.Repeat(SpriteRotation, 360f) - 180f;
			}
		}
		if (dashSound)
		{
			Sound.Play(dashSound.GetSound(null), m_sein.Position, null);
		}
		m_sein.Animation.Play(dashAnimation, 154, KeepDashAnimationPlaying);
		if (RainbowDashActivated)
		{
			((GameObject)InstantiateUtility.Instantiate(DashFollowRainbowEffect, m_sein.Position, Quaternion.identity)).transform.parent = m_sein.Transform;
		}
		m_sein.PlatformBehaviour.PlatformMovement.LocalSpeedY = -DashDownwardSpeed;
	}

	public void PerformDash()
	{
		m_chargeDashAtTarget = false;
		var dashSound = !RainbowDashActivated ? DashSound : RainbowDashSound;
		var isGliding = m_sein.Controller.IsGliding;
		PerformDash(!isGliding ? DashAnimation : GlideDashAnimation, dashSound);
		ChangeState(State.Dashing);
		UpdateDashing();
		OnDashEvent();
	}

	public void PerformWallDash()
	{
		m_chargeDashAtTarget = false;
		var dashSound = !RainbowDashActivated ? DashSound : RainbowDashSound;
		PerformDash(DashAnimation, dashSound);
		ChangeState(State.Dashing);
		UpdateDashing();
		OnWallDashEvent();
	}

	public void PerformDashIntoWall()
	{
		m_lastPressTime = 0f;
		m_lastDashTime = Time.time;
		m_sein.Animation.Play(DashIntoWallAnimation, 154, KeepDashIntoWallAnimationPlaying);
		Sound.Play(DashIntoWallSound.GetSound(null), m_sein.Position, null);
	}

	public bool KeepDashIntoWallAnimationPlaying()
	{
		return AgainstWall() && m_sein.IsOnGround;
	}

	public void PerformChargeDash()
	{
		m_hasHitAttackable = false;
		m_chargeJumpWasReleased = false;
		m_chargeDashAttackTarget = FindClosestAttackable as IAttackable;
		if (m_chargeDashAttackTarget != null)
		{
			m_chargeDashAtTarget = true;
			m_chargeDashDirection = (m_chargeDashAttackTarget.Position - m_sein.Position).normalized;
			m_chargeDashAtTargetPosition = m_chargeDashAttackTarget.Position;
		}
		else
		{
			m_chargeDashAtTarget = false;
		}
		var dashSound = !RainbowDashActivated ? ChargeDashSound : RainbowDashSound;
		PerformDash(ChargeDashAnimation, dashSound);
		if (m_chargeDashAtTarget)
		{
			SpriteRotation = Mathf.Atan2(m_chargeDashDirection.y, m_chargeDashDirection.x) * 57.29578f - (!m_faceLeft ? 0 : 180);
		}
		ChangeState(State.ChargeDashing);
		CompleteChargeEffect();
		UpdateChargeDashing();
	}

	private bool HasChargeDashSkill()
	{
		return m_sein.PlayerAbilities.ChargeDash.HasAbility;
	}

	private bool HasAirDashSkill()
	{
		return m_sein.PlayerAbilities.AirDash.HasAbility;
	}

	private bool CanChargeDash()
	{
		return HasChargeDashSkill() && Input.ChargeJump.Pressed && m_chargeJumpWasReleased && !Characters.Sein.Abilities.Swimming.IsSwimming;
	}

	public void CompleteChargeEffect()
	{
		if (m_sein.Abilities.ChargeJumpCharging)
		{
			m_sein.Abilities.ChargeJumpCharging.EndCharge();
		}
	}

	private void UpdateTargetHighlight(IChargeDashAttackable target)
	{
		if (m_lastTarget == target)
		{
			return;
		}
		if (!InstantiateUtility.IsDestroyed(m_lastTarget as Component))
		{
			m_lastTarget.OnChargeDashDehighlight();
		}
		m_lastTarget = target;
		if (!InstantiateUtility.IsDestroyed(m_lastTarget as Component))
		{
			m_lastTarget.OnChargeDashHighlight();
		}
	}

	public bool KeepDashAnimationPlaying()
	{
		return !m_stopAnimation && !m_sein.Abilities.WallSlide.IsOnWall && Active;
	}

	public bool KeepChargeDashAnimationPlaying()
	{
		return KeepDashAnimationPlaying();
	}

	public bool AgainstWall()
	{
		var platformMovement = m_sein.PlatformBehaviour.PlatformMovement;
		return (platformMovement.HasWallLeft && m_sein.FaceLeft) || (platformMovement.HasWallRight && !m_sein.FaceLeft);
	}

	public bool CanPerformNormalDash()
	{
		return	(HasAirDashSkill() || m_sein.IsOnGround || (RandomizerBonus.GravitySuit() && Characters.Sein.Abilities.Swimming.IsSwimming)) && !AgainstWall() && DashHasCooledDown && !m_hasDashed;
	}

	private bool DashHasCooledDown => Time.time - m_lastDashTime > 0.4f;

	public bool CanPerformDashIntoWall()
	{
		return m_sein.IsOnGround && AgainstWall() && DashHasCooledDown;
	}

	public bool CanWallDash()
	{
		var platformMovement = m_sein.PlatformBehaviour.PlatformMovement;
		return ((platformMovement.HasWallLeft && m_sein.Input.Horizontal >= 0f) || (platformMovement.HasWallRight && m_sein.Input.Horizontal <= 0f)) && !m_sein.IsOnGround && m_sein.PlayerAbilities.AirDash.HasAbility;
	}

	public void UpdateNormal()
	{
		var num = Time.time - m_lastPressTime;
		if (m_sein.IsOnGround || (RandomizerBonus.GravitySuit() && Characters.Sein.Abilities.Swimming.IsSwimming))
		{
			m_hasDashed = false;
			RandomizerBonus.DoubleAirDashUsed = false;
		}
		if (Input.Glide.Pressed && m_timeWhenDashJumpHappened + 5f > Time.time)
		{
			m_timeWhenDashJumpHappened = 0f;
			var platformMovement = m_sein.PlatformBehaviour.PlatformMovement;
			var num2 = OffGroundSpeed - 2f;
			if (Mathf.Abs(platformMovement.LocalSpeedX) > num2)
			{
				platformMovement.LocalSpeedX = Mathf.Sign(platformMovement.LocalSpeedX) * num2;
			}
		}
		IChargeDashAttackable target;
		if (CanChargeDash())
		{
			target = FindClosestAttackable;
		}
		else
		{
			target = null;
		}
		UpdateTargetHighlight(target);
		if (Input.RightShoulder.Pressed && num < 0.15f)
		{
			if (CanChargeDash())
			{
				if (HasEnoughEnergy)
				{
					SpendEnergy();
					PerformChargeDash();
					return;
				}
				ShowNotEnoughEnergy();
				m_lastPressTime = 0f;
			}
			else
			{
				if (CanPerformNormalDash())
				{
					PerformDash();
					return;
				}
				if (CanWallDash())
				{
					PerformWallDash();
					return;
				}
				if (CanPerformDashIntoWall())
				{
					PerformDashIntoWall();
				}
			}
		}
	}

	private void ShowNotEnoughEnergy()
	{
		UI.SeinUI.ShakeEnergyOrbBar();
		if (NotEnoughEnergySound)
		{
			Sound.Play(NotEnoughEnergySound.GetSound(null), transform.position, null);
		}
	}

	public void UpdateDashing()
	{
		var platformMovement = m_sein.PlatformBehaviour.PlatformMovement;
		UI.Cameras.Current.ChaseTarget.CameraSpeedMultiplier.x = Mathf.Clamp01(m_stateCurrentTime / DashTime);
		var velocity = DashSpeedOverTime.Evaluate(m_stateCurrentTime);
		velocity *= 1.0f + .2f*RandomizerBonus.Velocity();
		if (RandomizerBonus.GravitySuit() && Characters.Sein.Abilities.Swimming.IsSwimming)
		{
			var newSpeed = new Vector2(velocity, 0f);
			platformMovement.LocalSpeed = newSpeed.Rotate(m_sein.Abilities.Swimming.SwimAngle);
		}
		else if (RandomizerBonus.EnhancedDash && m_enhancedDashDirection.y != 0f)
		{
			platformMovement.LocalSpeed = m_enhancedDashDirection * velocity;
		}
		else
		{
			platformMovement.LocalSpeedX = (!m_faceLeft ? 1 : -1) * velocity;
		}
		m_sein.FaceLeft = m_faceLeft;
		if (AgainstWall())
		{
			platformMovement.LocalSpeed = Vector2.zero;
		}
		SpriteRotation = Mathf.Lerp(SpriteRotation, m_sein.PlatformBehaviour.PlatformMovement.GroundAngle, 0.2f);
		if (m_sein.IsOnGround)
		{
			if (Input.Horizontal > 0f && m_faceLeft)
			{
				StopDashing();
			}
			if (Input.Horizontal < 0f && !m_faceLeft)
			{
				StopDashing();
			}

			if (!m_isOnGround && RandomizerBonus.EnhancedDash)
			{
				m_isOnGround = true;
				m_enhancedDashDirection = m_faceLeft ? Vector3.left : Vector3.right;
				SpriteRotation = m_sein.PlatformBehaviour.PlatformMovement.GroundAngle;
			}
		}
		if (m_stateCurrentTime > DashTime)
		{
			if (platformMovement.IsOnGround && Input.Horizontal == 0f)
			{
				platformMovement.LocalSpeedX = 0f;
			}
			ChangeState(State.Normal);
		}
		if (Input.Jump.OnPressed || Input.Glide.OnPressed)
		{
			platformMovement.LocalSpeedX = !m_faceLeft ? OffGroundSpeed : -OffGroundSpeed;
			m_sein.PlatformBehaviour.AirNoDeceleration.NoDeceleration = m_allowNoDecelerationForThisDash;
			m_stopAnimation = true;
			ChangeState(State.Normal);
			m_timeWhenDashJumpHappened = Time.time;
		}
		if (RaycastTest() && m_isOnGround)
		{
			StickOntoGround();
			return;
		}
		m_isOnGround = false;
	}

	private void StickOntoGround()
	{
		var platformMovement = m_sein.PlatformBehaviour.PlatformMovement;
		var vector = platformMovement.Position;
		platformMovement.PlaceOnGround(0f, 8f);
		var vector2 = vector;
		platformMovement.PlaceOnGround(0.5f, 8f);
		var vector3 = vector;
		vector = vector2;
		if (vector3.y > vector2.y)
		{
			vector = vector3;
		}
		platformMovement.Position = vector;
	}

	public void UpdateChargeDashing()
	{
		var platformMovement = m_sein.PlatformBehaviour.PlatformMovement;
		AttackNearbyEnemies();
		m_sein.Mortality.DamageReciever.MakeInvincibleToEnemies(1f);
		var velocity = ChargeDashSpeedOverTime.Evaluate(m_stateCurrentTime);
		velocity *= 1.0f + .2f*RandomizerBonus.Velocity();
		if (m_chargeDashAtTarget)
		{
			platformMovement.LocalSpeed = m_chargeDashDirection * velocity;
		}
		else if (RandomizerBonus.EnhancedDash && m_enhancedDashDirection.y != 0f)
		{
			platformMovement.LocalSpeed = m_enhancedDashDirection * velocity;
		}
		else
		{
			platformMovement.LocalSpeedX = (!m_faceLeft ? 1 : -1) * velocity;
		}
		if (m_hasHitAttackable)
		{
			platformMovement.LocalSpeed *= 0.33f;
		}
		m_sein.FaceLeft = m_faceLeft;
		SpriteRotation = Mathf.Lerp(SpriteRotation, m_sein.PlatformBehaviour.PlatformMovement.GroundAngle, 0.3f);
		if (AgainstWall())
		{
			platformMovement.LocalSpeed = Vector2.zero;
		}
		if (m_sein.IsOnGround)
		{
			if (Input.Horizontal > 0f && m_faceLeft)
			{
				StopDashing();
			}
			if (Input.Horizontal < 0f && !m_faceLeft)
			{
				StopDashing();
			}

			if (!m_isOnGround && RandomizerBonus.EnhancedDash)
			{
				m_isOnGround = true;
				m_enhancedDashDirection = m_faceLeft ? Vector3.left : Vector3.right;
				SpriteRotation = m_sein.PlatformBehaviour.PlatformMovement.GroundAngle;
			}
		}
		if (m_stateCurrentTime > ChargeDashTime)
		{
			ChangeState(State.Normal);
		}
		if (Input.Jump.OnPressed || Input.Glide.OnPressed)
		{
			platformMovement.LocalSpeedX = !m_faceLeft ? OffGroundSpeed : -OffGroundSpeed;
			m_sein.PlatformBehaviour.AirNoDeceleration.NoDeceleration = true;
			m_stopAnimation = true;
			ChangeState(State.Normal);
		}
		if (RaycastTest() && m_isOnGround && !m_chargeDashAtTarget)
		{
			StickOntoGround();
			return;
		}
		m_isOnGround = false;
	}

	public void UpdateState()
	{
		UI.Cameras.Current.ChaseTarget.CameraSpeedMultiplier.x = 1f;
		if (Input.RightShoulder.OnPressed)
		{
			m_lastPressTime = Time.time;
		}
		if (Input.ChargeJump.Released)
		{
			m_chargeJumpWasReleased = true;
		}
		switch (CurrentState)
		{
		case State.Normal:
			UpdateNormal();
			break;
		case State.Dashing:
			UpdateDashing();
			break;
		case State.ChargeDashing:
			UpdateChargeDashing();
			break;
		}
		m_stateCurrentTime += Time.deltaTime;
	}

	public void StopDashing()
	{
		m_sein.PlatformBehaviour.PlatformMovement.LocalSpeed = Vector2.zero;
		ChangeState(State.Normal);
		m_stopAnimation = true;
		m_chargeDashAtTarget = false;
	}

	private bool RaycastTest()
	{
		var a = Vector3.Cross(m_sein.PlatformBehaviour.PlatformMovement.GroundRayNormal, Vector3.forward);
		var num = m_sein.Speed.x * Time.deltaTime;
		var vector = m_sein.Position + a * num + Vector3.up;
		var vector2 = Vector3.down * (1.8f + Mathf.Abs(num));
		Debug.DrawRay(vector, vector2, Color.yellow, 0.5f);
		RaycastHit raycastHit;
		return m_sein.Controller.RayTest(vector, vector2, out raycastHit);
	}

	public void ResetDashLimit()
	{
		m_hasDashed = false;
		RandomizerBonus.DoubleAirDashUsed = false;
	}

	public float AdjustedEnergyCost
	{
		get
		{
			var efficiencyDiscount = RandomizerBonus.ChargeDashEfficiency() ? 0.5f : 0f;
			var enhancedDiscount = RandomizerBonus.EnhancedDash ? 0.5f : 0f;
			return EnergyCost - efficiencyDiscount - enhancedDiscount;
		}
	}

	public AnimationCurve DashSpeedOverTime;

	public AnimationCurve ChargeDashSpeedOverTime;

	public float DashTime = 0.5f;

	public float ChargeDashTime = 0.5f;

	public float ChargeTime = 0.2f;

	public SoundProvider ChargeSound;

	public SoundProvider DoneChargingSound;

	public SoundSource ChargedSound;

	public SoundProvider UnChargeSound;

	public SoundProvider DashSound;

	public SoundProvider ChargeDashSound;

	public SoundProvider RainbowDashSound;

	public SoundProvider DashIntoWallSound;

	public GameObject ExplosionEffect;

	public State CurrentState;

	public float DashDownwardSpeed = 10f;

	public float OffGroundSpeed = 15f;

	public int Damage = 50;

	public float EnergyCost = 1f;

	public SoundProvider NotEnoughEnergySound;

	public TextureAnimationWithTransitions DashAnimation;

	public TextureAnimationWithTransitions ChargeDashAnimation;

	public TextureAnimationWithTransitions GlideDashAnimation;

	public TextureAnimationWithTransitions DashIntoWallAnimation;

	public GameObject DashStartEffect;

	public GameObject DashFollowEffect;

	public GameObject DashFollowRainbowEffect;

	private SeinCharacter m_sein;

	private bool m_faceLeft;

	private float m_stateCurrentTime;

	private HashSet<IAttackable> m_attackablesIgnore = new HashSet<IAttackable>();

	private bool m_stopAnimation;

	private float m_lastPressTime;

	private float m_lastDashTime;

	private bool m_isOnGround;

	public static bool RainbowDashActivated;

	private bool m_hasDashed;

	public float ChargeDashTargetMaxDistance = 20f;

	private float m_timeOfLastExplosionEffect;

	private float m_timeWhenDashJumpHappened;

	private bool m_allowNoDecelerationForThisDash;

	private IAttackable m_chargeDashAttackTarget;

	private bool m_hasHitAttackable;

	private bool m_chargeJumpWasReleased = true;

	private IChargeDashAttackable m_lastTarget;

	public float SpriteRotation;

	private Vector3 m_chargeDashDirection;

	private bool m_chargeDashAtTarget;

	private Vector3 m_chargeDashAtTargetPosition;

	private Vector3 m_enhancedDashDirection;

	public enum State
	{
		Normal,
		Dashing,
		ChargeDashing
	}
}

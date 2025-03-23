using System;
using Core;
using UnityEngine;

public class SeinGlide : CharacterState, ISeinReceiver
{
	public CharacterGravity CharacterGravity
	{
		get
		{
			return this.Sein.PlatformBehaviour.Gravity;
		}
	}

	public CharacterLeftRightMovement CharacterLeftRightMovement
	{
		get
		{
			return this.Sein.PlatformBehaviour.LeftRightMovement;
		}
	}

	public PlatformMovement PlatformMovement
	{
		get
		{
			return this.Sein.PlatformBehaviour.PlatformMovement;
		}
	}

	public void Start()
	{
		this.CharacterGravity.ModifyGravityPlatformMovementSettingsEvent += this.ModifyGravityPlatformMovementSettings;
		this.CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent += this.ModifyHorizontalPlatformMovementSettings;
	}

	public new void OnDestroy()
	{
		base.OnDestroy();
		this.CharacterGravity.ModifyGravityPlatformMovementSettingsEvent -= this.ModifyGravityPlatformMovementSettings;
		this.CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent -= this.ModifyHorizontalPlatformMovementSettings;
		this.IsGliding = false;
	}

	public override void OnExit()
	{
		this.IsGliding = false;
	}

	public void ModifyGravityPlatformMovementSettings(GravityPlatformMovementSettings settings)
	{
		if (this.IsGliding && this.PlatformMovement.LocalSpeedY < 0f)
		{
			settings.GravityStrength *= this.GravityMultiplier;
		}
	}

	public void ModifyHorizontalPlatformMovementSettings(HorizontalPlatformMovementSettings settings)
	{
		if (this.IsGliding)
		{
			settings.Air.ApplySpeedMultiplier(this.MoveSpeed);
		}
	}

	public bool IsGliding
	{
		get
		{
			return this.m_isGliding;
		}
		set
		{
			if (this.m_isGliding != value)
			{
				this.m_isGliding = value;
				if (this.m_isGliding)
				{
					this.OnEnterGlide();
				}
				else
				{
					this.OnExitGlide();
				}
			}
		}
	}

	public void OnEnterGlide()
	{
		this.UpdateAnimations();
	}

	public void OnExitGlide()
	{
		if (this.m_parachuteLoopLastSound)
		{
			this.m_parachuteLoopLastSound.FadeOut(1f, true);
		}
		base.OnExit();
		if (this.RunningTime > 0.3f)
		{
			Sound.Play(this.CloseParachuteSound.GetSound(null), this.PlatformMovement.Position, null);
		}
		this.RunningTime = 0f;
		this.m_playedOpenSound = false;
	}

	public bool CanGlide
	{
		get
		{
			return !this.PlatformMovement.IsOnGround && !this.PlatformMovement.IsOnWall && !this.Sein.Controller.InputLocked && !SeinAbilityRestrictZone.IsInside(SeinAbilityRestrictZoneMode.AllAbilities);
		}
	}

	public bool WantsToGlide
	{
		get
		{
			return Core.Input.Glide.Pressed && !this.NeedsRightTriggerReleased && this.m_lockGlidingRemainingTime <= 0f;
		}
	}

	public void LockGliding(float time)
	{
		this.m_lockGlidingRemainingTime = time;
	}

	public void UpdateGliding()
	{
		if (!this.CanGlide || !this.WantsToGlide)
		{
			this.IsGliding = false;
		}
		this.m_pressedMoveHorizontally = false;
		this.RunningTime += Time.deltaTime;
		if (!this.m_playedOpenSound && this.RunningTime > 0.15f && this.RunningTime < 0.2f)
		{
			Sound.Play(this.OpenParachuteSound.GetSound(null), this.PlatformMovement.Position, null);
			this.m_playedOpenSound = true;
		}
		if (!this.IsGliding)
		{
			this.Exit();
			return;
		}
		if (this.PlatformMovement.LocalSpeedY < -this.GlideSpeed)
		{
			this.PlatformMovement.LocalSpeedY = -this.GlideSpeed;
		}
		this.UpdateAnimations();
		if (this.m_pressedMoveHorizontally && !this.m_wasMovingHorizantally)
		{
			Sound.Play(this.TurnLeftRightSound.GetSound(null), this.PlatformMovement.Position, null);
		}
		else if (this.m_parachuteLoopLastSound == null)
		{
			this.m_parachuteLoopLastSound = Sound.Play(this.ParachuteLoopSound.GetSound(null), this.PlatformMovement.Position, delegate()
			{
				this.m_parachuteLoopLastSound = null;
			});
			if (this.m_parachuteLoopLastSound)
			{
				this.m_parachuteLoopLastSound.AttachTo = this.PlatformMovement.transform;
			}
		}
		this.m_wasMovingHorizantally = this.m_pressedMoveHorizontally;
		this.HandleFloatZones();
	}

	private void UpdateAnimations()
	{
		if (this.ShouldGlideMovingAnimationPlay)
		{
			this.m_pressedMoveHorizontally = true;
			this.Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(this.MovingAnimation, 110, new Func<bool>(this.ShouldGlideMovingAnimationKeepPlaying), false);
		}
		else if (this.ShouldGlideIdleAnimationPlay)
		{
			this.Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(this.IdleAnimation, 110, new Func<bool>(this.ShouldGlideIdleAnimationKeepPlaying), false);
		}
	}

	public void HandleFloatZones()
	{
		for (int i = 0; i < FloatZone.All.Count; i++)
		{
			FloatZone floatZone = FloatZone.All[i];
			if (floatZone.BoundingRect.Contains(this.Sein.Position))
			{
				PlatformMovement platformMovement = this.Sein.PlatformBehaviour.PlatformMovement;
				Vector2 b = Vector2.up * this.Sein.PlatformBehaviour.Gravity.CurrentSettings.GravityStrength * Time.deltaTime;
				platformMovement.LocalSpeed += b;
				Vector2 localSpeed = platformMovement.LocalSpeed;
				if (localSpeed.y < 0f)
				{
					localSpeed.y = MoonMath.Float.ClampedAdd(localSpeed.y, floatZone.Deceleration * Time.deltaTime, 0f, 0f);
				}
				if (localSpeed.y >= 0f)
				{
					localSpeed.y = MoonMath.Float.ClampedAdd(localSpeed.y, floatZone.Acceleration * Time.deltaTime, 0f, floatZone.DesiredSpeed);
					localSpeed.y = MoonMath.Float.ClampedSubtract(localSpeed.y, floatZone.TooFastDeceleration * Time.deltaTime, 0f, floatZone.DesiredSpeed);
				}
				platformMovement.LocalSpeed = localSpeed;
				this.Sein.ResetAirLimits();
				return;
			}
		}
	}

	public override void UpdateCharacterState()
	{
		if (this.CharacterLeftRightMovement.HorizontalInput != 0f)
		{
			this.m_isMoveAnimation = 3;
		}
		else if (this.m_isMoveAnimation > 0)
		{
			this.m_isMoveAnimation--;
		}
		if (this.m_lockGlidingRemainingTime > 0f)
		{
			this.m_lockGlidingRemainingTime -= Time.deltaTime;
			if (this.m_lockGlidingRemainingTime < 0f)
			{
				this.m_lockGlidingRemainingTime = 0f;
			}
		}
		if (this.NeedsRightTriggerReleased && Core.Input.Glide.Released)
		{
			this.NeedsRightTriggerReleased = false;
		}
		if (this.IsGliding)
		{
			this.UpdateGliding();
		}
		else if (this.CanGlide && this.WantsToGlide && this.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY < 0f)
		{
			this.IsGliding = true;
		}
	}

	public bool CanEnter
	{
		get
		{
			bool isGliding = this.IsGliding;
			if (isGliding)
			{
			}
			return isGliding;
		}
	}

	public float GlideOpeningTime
	{
		get
		{
			return 0.5f;
		}
	}

	public bool ShouldGlideIdleAnimationPlay
	{
		get
		{
			return this.ShouldGlideIdleAnimationKeepPlaying();
		}
	}

	public bool ShouldGlideMovingAnimationPlay
	{
		get
		{
			return this.ShouldGlideMovingAnimationKeepPlaying();
		}
	}

	public bool ShouldGlideIdleAnimationKeepPlaying()
	{
		return this.IsGliding;
	}

	public bool ShouldGlideMovingAnimationKeepPlaying()
	{
		return this.IsGliding && this.m_isMoveAnimation > 0;
	}

	public void SetReferenceToSein(SeinCharacter sein)
	{
		this.Sein = sein;
		this.Sein.Abilities.Glide = this;
	}

	public SeinCharacter Sein;

	public TextureAnimationWithTransitions IdleAnimation;

	public TextureAnimationWithTransitions MovingAnimation;

	public SoundProvider OpenParachuteSound;

	public SoundProvider CloseParachuteSound;

	public SoundProvider ParachuteLoopSound;

	public SoundProvider TurnLeftRightSound;

	private SoundPlayer m_parachuteLoopLastSound;

	private bool m_playedOpenSound;

	private bool m_pressedMoveHorizontally;

	private bool m_wasMovingHorizantally;

	private bool m_isGliding;

	public bool NeedsRightTriggerReleased;

	private float m_lockGlidingRemainingTime;

	private int m_isMoveAnimation;

	public float RunningTime;

	public int Level;

	public float MinHeightToGlide = 2f;

	public float GlideSpeed;

	public float GravityMultiplier = 0.5f;

	public HorizontalPlatformMovementSettings.SpeedMultiplierSet MoveSpeed;
}

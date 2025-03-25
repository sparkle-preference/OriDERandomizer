using System;
using Core;
using UnityEngine;

public class SeinWallSlide : CharacterState, ISeinReceiver
{
	public CharacterSpriteMirror SpriteMirror
	{
		get
		{
			return this.Sein.PlatformBehaviour.Visuals.SpriteMirror;
		}
	}

	public CharacterLeftRightMovement LeftRightMovement
	{
		get
		{
			return this.Sein.PlatformBehaviour.LeftRightMovement;
		}
	}

	public SeinDoubleJump DoubleJump
	{
		get
		{
			return this.Sein.Abilities.DoubleJump;
		}
	}

	public SeinJump Jump
	{
		get
		{
			return this.Sein.Abilities.Jump;
		}
	}

	public CharacterGravity Gravity
	{
		get
		{
			return this.Sein.PlatformBehaviour.Gravity;
		}
	}

	public PlatformMovement PlatformMovement
	{
		get
		{
			return this.Sein.PlatformBehaviour.PlatformMovement;
		}
	}

	public bool IsOnWall
	{
		get
		{
			float num = Mathf.Cos(0.27925268f);
			PlatformMovementListOfColliders platformMovementListOfColliders = this.Sein.PlatformBehaviour.PlatformMovementListOfColliders;
			Collider wallLeftCollider = platformMovementListOfColliders.WallLeftCollider;
			Collider wallRightCollider = platformMovementListOfColliders.WallRightCollider;
			PlatformMovement platformMovement = this.PlatformMovement;
			if (platformMovement.HasWallLeft)
			{
				if (wallLeftCollider && wallLeftCollider.CompareTag("NoWallSlide"))
				{
					return false;
				}
				if (Vector3.Dot(platformMovement.WallLeftNormal, platformMovement.LocalToWorld(Vector3.up)) > 0f || Vector3.Dot(platformMovement.WallLeftNormal, platformMovement.LocalToWorld(Vector3.right)) > num || (wallLeftCollider && wallLeftCollider.GetComponent<SteepWall>()))
				{
					return true;
				}
			}
			if (platformMovement.HasWallRight)
			{
				if (wallRightCollider && wallRightCollider.CompareTag("NoWallSlide"))
				{
					return false;
				}
				if (Vector3.Dot(platformMovement.WallRightNormal, platformMovement.LocalToWorld(Vector3.up)) > 0f || Vector3.Dot(platformMovement.WallRightNormal, platformMovement.LocalToWorld(Vector3.left)) > num || (wallRightCollider && wallRightCollider.GetComponent<SteepWall>()))
				{
					return true;
				}
			}
			return false;
		}
	}

	public bool CanWallSlide
	{
		get
		{
			return base.Active && !this.PlatformMovement.IsOnGround && !this.PlatformMovement.IsOnCeiling && this.IsOnWall && this.PlatformMovement.HeadAndFeetAgainstWall;
		}
	}

	public bool IsWallSliding
	{
		get
		{
			return base.Active && (this.CurrentState == SeinWallSlide.State.SlidingLeft || this.CurrentState == SeinWallSlide.State.SlidingRight);
		}
	}

	public bool ShouldWallSlideUpAnimationPlay
	{
		get
		{
			return this.ShouldWallSlideUpAnimationKeepPlaying() && this.PlatformMovement.LocalSpeedY > this.WallSlideUpAnimationMinimiumSpeed;
		}
	}

	public bool ShouldWallSlideDownAnimationPlay
	{
		get
		{
			return this.ShouldWallSlideDownAnimationKeepPlaying();
		}
	}

	public void SetReferenceToSein(SeinCharacter sein)
	{
		this.Sein = sein;
		this.Sein.Abilities.WallSlide = this;
	}

	public void Start()
	{
		base.Active = true;
		this.LeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent += this.ModifyHorizontalPlatformMovementSettings;
		this.Gravity.ModifyGravityPlatformMovementSettingsEvent += this.ModifyGravityPlatformMovementSettings;
	}

	public new void OnDestroy()
	{
		base.OnDestroy();
		this.LeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent -= this.ModifyHorizontalPlatformMovementSettings;
		this.Gravity.ModifyGravityPlatformMovementSettingsEvent -= this.ModifyGravityPlatformMovementSettings;
	}

	public void ModifyGravityPlatformMovementSettings(GravityPlatformMovementSettings settings)
	{
		if (this.IsWallSliding && this.Sein.PlayerAbilities.WallJump.HasAbility)
		{
			settings.GravityStrength *= this.GravityMultiplier;
			settings.MaxFallSpeed = Mathf.Min(this.MaxFallSpeed, settings.MaxFallSpeed);
		}
	}

	public void ModifyHorizontalPlatformMovementSettings(HorizontalPlatformMovementSettings settings)
	{
		if (this.IsWallSliding && this.m_inputLockTimeRemaining > 0f)
		{
			settings.LockInput = true;
		}
	}

	public override void UpdateCharacterState()
	{
		this.m_inputLockTimeRemaining -= Time.deltaTime;
		this.UpdateState();
		if (this.IsWallSliding)
		{
			if (this.ShouldWallSlideUpAnimationPlay)
			{
				this.Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(this.SlideUpAnimation, 23, new Func<bool>(this.ShouldWallSlideUpAnimationKeepPlaying), false);
			}
			else if (this.ShouldWallSlideDownAnimationPlay)
			{
				this.Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(this.SlideDownAnimation, 23, new Func<bool>(this.ShouldWallSlideDownAnimationKeepPlaying), false);
			}

			if (this.Sein.PlayerAbilities.WallJump.HasAbility && this.ShouldStopSliding)
			{
				this.PlatformMovement.LocalSpeedY = 0f;
			}
		}
		this.HandleSounds();
	}

	public override void OnExit()
	{
		this.HandleSounds();
	}

	public SurfaceMaterialType WallSurfaceMaterialType
	{
		get
		{
			return this.Sein.PlatformBehaviour.WallSurfaceMaterialType;
		}
	}

	public void HandleSounds()
	{
		if (this.WallEnterSounds != null && (this.PlatformMovement.WallLeft.OnThisFrame || this.PlatformMovement.WallRight.OnThisFrame))
		{
			Sound.Play(this.WallEnterSounds.GetSoundForMaterial(this.WallSurfaceMaterialType, null), this.Sein.Position, null);
		}
		if (this.WallExitSounds != null && (this.PlatformMovement.WallLeft.OffThisFrame || this.PlatformMovement.WallRight.OffThisFrame))
		{
			Sound.Play(this.WallExitSounds.GetSoundForMaterial(this.WallSurfaceMaterialType, null), this.Sein.Position, null);
		}
		if (this.m_wallSlideUpSound == null && this.ShouldWallSlideUpAnimationPlay && !this.Sein.Controller.IsGrabbingWall)
		{
			this.m_wallSlideUpSound = Sound.Play(this.WallSlideUpSound.GetSoundForMaterial(this.WallSurfaceMaterialType, null), this.Sein.Position, delegate()
			{
				this.m_wallSlideUpSound = null;
			});
		}
		if ((!this.ShouldWallSlideUpAnimationPlay || this.Sein.Controller.IsGrabbingWall) && this.m_wallSlideUpSound)
		{
			this.m_wallSlideUpSound.FadeOut(0.25f, true);
			UberPoolManager.Instance.RemoveOnDestroyed(this.m_wallSlideUpSound.gameObject);
			this.m_wallSlideUpSound = null;
		}
		if (this.m_wallSlideDownSound == null && this.ShouldWallSlideDownAnimationPlay && !this.Sein.Controller.IsGrabbingWall && GameController.Instance.GameTime > this.m_lastWallSlideDownSoundTime + this.m_minimumSoundDelay)
		{
			this.m_lastWallSlideDownSoundTime = GameController.Instance.GameTime;
			this.m_wallSlideDownSound = Sound.Play(this.WallSlideDownSound.GetSoundForMaterial(this.WallSurfaceMaterialType, null), this.Sein.Position, delegate()
			{
				this.m_wallSlideDownSound = null;
			});
		}
		if ((!this.ShouldWallSlideDownAnimationPlay || this.Sein.Controller.IsGrabbingWall) && this.m_wallSlideDownSound)
		{
			this.m_wallSlideDownSound.FadeOut(0.25f, true);
			UberPoolManager.Instance.RemoveOnDestroyed(this.m_wallSlideDownSound.gameObject);
			this.m_wallSlideDownSound = null;
		}
	}

	public void ChangeState(SeinWallSlide.State state)
	{
		this.CurrentState = state;
		switch (this.CurrentState)
		{
		case SeinWallSlide.State.SlidingLeft:
			this.ResetMovingOffWallLockTimer();
			this.Sein.ResetAirLimits();
			if (this.Jump)
			{
				this.Jump.ResetRunningJumpCount();
			}
			break;
		case SeinWallSlide.State.SlidingRight:
			this.ResetMovingOffWallLockTimer();
			this.Sein.ResetAirLimits();
			if (this.Jump)
			{
				this.Jump.ResetRunningJumpCount();
			}
			break;
		}
	}

	public void ResetMovingOffWallLockTimer()
	{
		if (this.Sein.Abilities.WallJump && this.Sein.Abilities.WallJump.Active)
		{
			this.m_inputLockTimeRemaining = this.InputLockDuration;

			if (this.ShouldStopSliding)
			{
				this.PlatformMovement.LocalSpeedY = 0f;
			}
		}
		else
		{
			this.m_inputLockTimeRemaining = 0f;
		}
	}

	public void UpdateState()
	{
		switch (this.CurrentState)
		{
		case SeinWallSlide.State.Normal:
			if (!this.PlatformMovement.IsOnGround)
			{
				if (this.CanWallSlide && this.PlatformMovement.HasWallLeft)
				{
					this.ChangeState(SeinWallSlide.State.SlidingLeft);
				}
				else if (this.CanWallSlide && this.PlatformMovement.HasWallRight)
				{
					this.ChangeState(SeinWallSlide.State.SlidingRight);
				}
			}
			break;
		case SeinWallSlide.State.SlidingLeft:
			if (!this.CanWallSlide)
			{
				this.ChangeState(SeinWallSlide.State.Normal);
				return;
			}
			if (this.LeftRightMovement.BaseHorizontalInput <= 0f)
			{
				this.ResetMovingOffWallLockTimer();
			}
			if (this.PlatformMovement.HeadAndFeetAgainstWall)
			{
				this.SpriteMirror.FaceLeft = true;
			}
			break;
		case SeinWallSlide.State.SlidingRight:
			if (!this.CanWallSlide)
			{
				this.ChangeState(SeinWallSlide.State.Normal);
				return;
			}
			if (this.LeftRightMovement.BaseHorizontalInput >= 0f)
			{
				this.ResetMovingOffWallLockTimer();
			}
			if (this.PlatformMovement.HeadAndFeetAgainstWall)
			{
				this.SpriteMirror.FaceLeft = false;
			}
			break;
		}
	}

	public bool ShouldWallSlideUpAnimationKeepPlaying()
	{
		return base.Active && this.IsOnWall && this.PlatformMovement.IsInAir && this.PlatformMovement.Jumping && this.PlatformMovement.HeadAgainstWall && this.PlatformMovement.FeetAgainstWall;
	}

	public bool ShouldWallSlideDownAnimationKeepPlaying()
	{
		return base.Active && this.IsOnWall && this.PlatformMovement.IsInAir && this.PlatformMovement.FeetAgainstWall && this.PlatformMovement.HeadAgainstWall;
	}

	public override void Serialize(Archive ar)
	{
		ar.Serialize(ref this.m_inputLockTimeRemaining);
		base.Serialize(ar);
	}

	public bool ShouldStopSliding
	{
		get
		{
			return RandomizerBonus.EnhancedWallJump && this.PlatformMovement.LocalSpeedY < 0f && this.Sein.Input.Vertical >= 0f;
		}
	}

	public SeinWallSlide.State CurrentState;

	public float GravityMultiplier;

	public float InputLockDuration = 0.2f;

	public float MaxFallSpeed = 10f;

	public SeinCharacter Sein;

	public TextureAnimationWithTransitions SlideDownAnimation;

	public TextureAnimationWithTransitions SlideUpAnimation;

	public SurfaceToSoundProviderMap WallEnterSounds;

	public SurfaceToSoundProviderMap WallExitSounds;

	public SurfaceToSoundProviderMap WallSlideDownSound;

	public float WallSlideUpAnimationMinimiumSpeed = 5f;

	public SurfaceToSoundProviderMap WallSlideUpSound;

	private float m_inputLockTimeRemaining;

	private SoundPlayer m_wallSlideDownSound;

	private SoundPlayer m_wallSlideUpSound;

	private float m_lastWallSlideDownSoundTime;

	private float m_minimumSoundDelay = 0.4f;

	public enum State
	{
		Normal,
		SlidingLeft,
		SlidingRight
	}
}

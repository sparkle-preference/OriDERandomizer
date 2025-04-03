using System;
using UnityEngine;

public class SeinSpriteRotationController : CharacterState, ISeinReceiver
{
	public void BeginTiltLeftRightInAir(float duration)
	{
		this.m_tiltLeftRightTimer = duration;
	}

	public void BeginTiltUpDownInAir(float duration)
	{
		this.m_tiltUpDownTimer = duration;
	}

	public PlatformMovement PlatformMovement
	{
		get
		{
			return this.Sein.PlatformBehaviour.PlatformMovement;
		}
	}

	public SeinCrouch Crouch
	{
		get
		{
			return this.Sein.Abilities.Crouch;
		}
	}

	public SeinStomp Stomp
	{
		get
		{
			return this.Sein.Abilities.Stomp;
		}
	}

	public SeinBashAttack BashAttack
	{
		get
		{
			return this.Sein.Abilities.Bash;
		}
	}

	public bool IsStomping
	{
		get
		{
			return this.Stomp && this.Stomp.Active;
		}
	}

	public void SetReferenceToSein(SeinCharacter sein)
	{
		this.Sein = sein;
	}

	private void UpdateUnderwaterRotation()
	{
		this.HeadAngle = 0f;
		this.FeetAngle = 0f;
		this.CenterAngle = this.Sein.Abilities.Swimming.SwimAngle + (float)((!this.Sein.Controller.FaceLeft) ? 0 : 180);
	}

	private void UpdateCinematicRotation()
	{
		if (this.PlatformMovement.IsOnGround)
		{
			this.m_groundAngle = Mathf.LerpAngle(this.m_groundAngle, this.PlatformMovement.GroundAngle, 0.1f);
		}
		else
		{
			this.m_groundAngle = Mathf.LerpAngle(this.m_groundAngle, 0f, 0.1f);
		}
		this.FeetAngle = this.m_groundAngle;
		this.HeadAngle = 0f;
		this.CenterAngle = 0f;
	}

	private void UpdateRegularRotation()
	{
		if (this.m_tiltLeftRightTimer > 0f)
		{
			this.m_tiltLeftRightTimer = Mathf.Max(this.m_tiltLeftRightTimer - Time.deltaTime, 0f);
		}
		if (this.m_tiltUpDownTimer > 0f)
		{
			this.m_tiltUpDownTimer = Mathf.Max(this.m_tiltUpDownTimer - Time.deltaTime, 0f);
		}
		this.CenterAngle = 0f;
		this.HeadAngle = 0f;
		this.FeetAngle = 0f;
		if (this.PlatformMovement.HasWallLeft)
		{
			if (!this.PlatformMovement.WallLeft.WasOn)
			{
				this.m_wallLeftAngle = ((!this.PlatformMovement.WallLeftRayHit) ? this.PlatformMovement.GravityAngle : this.PlatformMovement.WallLeftAngle);
			}
			else if (this.PlatformMovement.WallLeftRayHit)
			{
				this.m_wallLeftAngle = Mathf.LerpAngle(this.m_wallLeftAngle, this.PlatformMovement.WallLeftAngle, 0.2f);
			}
			if (this.Sein.Abilities.Swimming && this.Sein.Abilities.Swimming.IsSwimming)
			{
				this.FeetAngle = this.PlatformMovement.GravityAngle;
			}
			else if (this.PlatformMovement.IsOnGround && this.Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft)
			{
				this.FeetAngle = this.PlatformMovement.GravityAngle;
			}
			else
			{
				this.FeetAngle = Mathf.Max(0f, this.m_wallLeftAngle);
				this.HeadAngle = Mathf.Min(0f, this.m_wallLeftAngle);
			}
		}
		else if (this.PlatformMovement.HasWallRight)
		{
			if (!this.PlatformMovement.WallRight.WasOn)
			{
				this.m_wallRightAngle = ((!this.PlatformMovement.WallRightRayHit) ? this.PlatformMovement.GravityAngle : this.PlatformMovement.WallRightAngle);
			}
			else if (this.PlatformMovement.WallRightRayHit)
			{
				this.m_wallRightAngle = Mathf.LerpAngle(this.m_wallRightAngle, this.PlatformMovement.WallRightAngle, 0.2f);
			}
			if (this.Sein.Abilities.Swimming && this.Sein.Abilities.Swimming.IsSwimming)
			{
				this.FeetAngle = this.PlatformMovement.GravityAngle;
			}
			else if (this.PlatformMovement.IsOnGround && !this.Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft)
			{
				this.FeetAngle = this.PlatformMovement.GravityAngle;
			}
			else
			{
				this.HeadAngle = Mathf.Max(0f, this.m_wallRightAngle);
				this.FeetAngle = Mathf.Min(0f, this.m_wallRightAngle);
			}
		}
		else if (this.PlatformMovement.IsOnGround)
		{
			if (this.Sein.Controller.IsAimingGrenade)
			{
				this.m_groundAngle = this.PlatformMovement.GroundAngle;
			}
			else if (!this.PlatformMovement.Ground.WasOn)
			{
				this.m_groundAngle = ((!this.PlatformMovement.GroundRayHit) ? this.PlatformMovement.GravityAngle : this.PlatformMovement.GroundAngle);
			}
			else if (this.PlatformMovement.GroundRayHit)
			{
				this.m_groundAngle = Mathf.LerpAngle(this.m_groundAngle, this.PlatformMovement.GroundAngle, 0.2f);
			}
			if (this.Sein.Abilities.Swimming && this.Sein.Abilities.Swimming.IsSwimming)
			{
				this.FeetAngle = this.PlatformMovement.GravityAngle;
			}
			else if (this.PlatformMovement.IsOnCeiling && this.Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft == this.PlatformMovement.CeilingNormal.x > 0f)
			{
				this.FeetAngle = this.PlatformMovement.GravityAngle;
			}
			else
			{
				this.FeetAngle = this.m_groundAngle;
			}
		}
		else
		{
			this.FeetAngle = this.PlatformMovement.GravityAngle;
			if (this.m_tiltLeftRightTimer > 0f)
			{
				this.CenterAngle -= Mathf.Atan2(this.PlatformMovement.LocalSpeedX, 12f) * 57.29578f * 0.5f * Mathf.Clamp01(this.m_tiltLeftRightTimer);
			}
			if (this.m_tiltUpDownTimer > 0f)
			{
				this.CenterAngle += (float)((!this.Sein.FaceLeft) ? 1 : -1) * Mathf.Atan2(this.PlatformMovement.LocalSpeedY, 12f) * 57.29578f * 0.5f * Mathf.Clamp01(this.m_tiltUpDownTimer);
			}
		}
		if (this.Sein.Abilities.StandingOnEdge && this.Sein.Abilities.StandingOnEdge.StandingOnEdge)
		{
			this.FeetAngle = this.PlatformMovement.GravityAngle;
		}
	}

	public override void UpdateCharacterState()
	{
		if (this.CinematicRotation)
		{
			this.UpdateCinematicRotation();
		}
		else if (this.Sein.Controller.IsDashing)
		{
			this.UpdateDashingRotation();
		}
		else if (this.Sein.Controller.IsSwimming && this.Sein.Abilities.Swimming.IsUnderwater && !this.Sein.Controller.IsBashing && !this.Sein.Controller.IsStomping)
		{
			this.UpdateUnderwaterRotation();
		}
		else
		{
			this.UpdateRegularRotation();
		}
		this.UpdateRotation();
	}

	public void UpdateDashingRotation()
	{
		this.FeetAngle = (this.HeadAngle = (this.CenterAngle = 0f));
		if (this.Sein.IsOnGround)
		{
			this.FeetAngle = this.Sein.Abilities.Dash.SpriteRotation;
		}
		else
		{
			this.CenterAngle = this.Sein.Abilities.Dash.SpriteRotation;
		}
	}

	public void UpdateRotation()
	{
		if (this.FeetTransform)
		{
			this.FeetTransform.eulerAngles = new Vector3(0f, 0f, this.FeetAngle);
		}
		if (this.HeadTransform)
		{
			this.HeadTransform.localEulerAngles = new Vector3(0f, 0f, this.HeadAngle);
		}
		if (this.CenterTransform)
		{
			this.CenterTransform.localEulerAngles = new Vector3(0f, 0f, this.CenterAngle);
		}
	}

	public override void Serialize(Archive ar)
	{
		ar.Serialize(ref this.FeetAngle);
		ar.Serialize(ref this.CenterAngle);
		ar.Serialize(ref this.m_ceilingAngle);
		ar.Serialize(ref this.m_groundAngle);
		ar.Serialize(ref this.m_localPosition);
		ar.Serialize(ref this.m_wallLeftAngle);
		ar.Serialize(ref this.m_wallRightAngle);
		if (ar.Reading)
		{
			this.UpdateRotation();
		}
	}

	public Transform FeetTransform;

	public Transform HeadTransform;

	public Transform CenterTransform;

	public bool CinematicRotation;

	public float FeetAngle;

	public float HeadAngle;

	public float CenterAngle;

	public SeinCharacter Sein;

	private float m_ceilingAngle;

	private float m_groundAngle;

	private Vector2 m_localPosition;

	private float m_wallLeftAngle;

	private float m_wallRightAngle;

	private float m_tiltLeftRightTimer;

	private float m_tiltUpDownTimer;
}

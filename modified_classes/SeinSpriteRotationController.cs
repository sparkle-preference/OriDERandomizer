using UnityEngine;

public class SeinSpriteRotationController : CharacterState, ISeinReceiver
{
	public void BeginTiltLeftRightInAir(float duration)
	{
		m_tiltLeftRightTimer = duration;
	}

	public void BeginTiltUpDownInAir(float duration)
	{
		m_tiltUpDownTimer = duration;
	}

	public PlatformMovement PlatformMovement
	{
		get
		{
			return Sein.PlatformBehaviour.PlatformMovement;
		}
	}

	public SeinCrouch Crouch
	{
		get
		{
			return Sein.Abilities.Crouch;
		}
	}

	public SeinStomp Stomp
	{
		get
		{
			return Sein.Abilities.Stomp;
		}
	}

	public SeinBashAttack BashAttack
	{
		get
		{
			return Sein.Abilities.Bash;
		}
	}

	public bool IsStomping
	{
		get
		{
			return Stomp && Stomp.Active;
		}
	}

	public void SetReferenceToSein(SeinCharacter sein)
	{
		Sein = sein;
	}

	private void UpdateUnderwaterRotation()
	{
		HeadAngle = 0f;
		FeetAngle = 0f;
		CenterAngle = Sein.Abilities.Swimming.SwimAngle + ((!Sein.Controller.FaceLeft) ? 0 : 180);
	}

	private void UpdateCinematicRotation()
	{
		if (PlatformMovement.IsOnGround)
		{
			m_groundAngle = Mathf.LerpAngle(m_groundAngle, PlatformMovement.GroundAngle, 0.1f);
		}
		else
		{
			m_groundAngle = Mathf.LerpAngle(m_groundAngle, 0f, 0.1f);
		}
		FeetAngle = m_groundAngle;
		HeadAngle = 0f;
		CenterAngle = 0f;
	}

	private void UpdateRegularRotation()
	{
		if (m_tiltLeftRightTimer > 0f)
		{
			m_tiltLeftRightTimer = Mathf.Max(m_tiltLeftRightTimer - Time.deltaTime, 0f);
		}
		if (m_tiltUpDownTimer > 0f)
		{
			m_tiltUpDownTimer = Mathf.Max(m_tiltUpDownTimer - Time.deltaTime, 0f);
		}
		CenterAngle = 0f;
		HeadAngle = 0f;
		FeetAngle = 0f;
		if (PlatformMovement.HasWallLeft)
		{
			if (!PlatformMovement.WallLeft.WasOn)
			{
				m_wallLeftAngle = ((!PlatformMovement.WallLeftRayHit) ? PlatformMovement.GravityAngle : PlatformMovement.WallLeftAngle);
			}
			else if (PlatformMovement.WallLeftRayHit)
			{
				m_wallLeftAngle = Mathf.LerpAngle(m_wallLeftAngle, PlatformMovement.WallLeftAngle, 0.2f);
			}
			if (Sein.Abilities.Swimming && Sein.Abilities.Swimming.IsSwimming)
			{
				FeetAngle = PlatformMovement.GravityAngle;
			}
			else if (PlatformMovement.IsOnGround && Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft)
			{
				FeetAngle = PlatformMovement.GravityAngle;
			}
			else
			{
				FeetAngle = Mathf.Max(0f, m_wallLeftAngle);
				HeadAngle = Mathf.Min(0f, m_wallLeftAngle);
			}
		}
		else if (PlatformMovement.HasWallRight)
		{
			if (!PlatformMovement.WallRight.WasOn)
			{
				m_wallRightAngle = ((!PlatformMovement.WallRightRayHit) ? PlatformMovement.GravityAngle : PlatformMovement.WallRightAngle);
			}
			else if (PlatformMovement.WallRightRayHit)
			{
				m_wallRightAngle = Mathf.LerpAngle(m_wallRightAngle, PlatformMovement.WallRightAngle, 0.2f);
			}
			if (Sein.Abilities.Swimming && Sein.Abilities.Swimming.IsSwimming)
			{
				FeetAngle = PlatformMovement.GravityAngle;
			}
			else if (PlatformMovement.IsOnGround && !Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft)
			{
				FeetAngle = PlatformMovement.GravityAngle;
			}
			else
			{
				HeadAngle = Mathf.Max(0f, m_wallRightAngle);
				FeetAngle = Mathf.Min(0f, m_wallRightAngle);
			}
		}
		else if (PlatformMovement.IsOnGround)
		{
			if (Sein.Controller.IsAimingGrenade)
			{
				m_groundAngle = PlatformMovement.GroundAngle;
			}
			else if (!PlatformMovement.Ground.WasOn)
			{
				m_groundAngle = ((!PlatformMovement.GroundRayHit) ? PlatformMovement.GravityAngle : PlatformMovement.GroundAngle);
			}
			else if (PlatformMovement.GroundRayHit)
			{
				m_groundAngle = Mathf.LerpAngle(m_groundAngle, PlatformMovement.GroundAngle, 0.2f);
			}
			if (Sein.Abilities.Swimming && Sein.Abilities.Swimming.IsSwimming)
			{
				FeetAngle = PlatformMovement.GravityAngle;
			}
			else if (PlatformMovement.IsOnCeiling && Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft == PlatformMovement.CeilingNormal.x > 0f)
			{
				FeetAngle = PlatformMovement.GravityAngle;
			}
			else
			{
				FeetAngle = m_groundAngle;
			}
		}
		else
		{
			FeetAngle = PlatformMovement.GravityAngle;
			if (m_tiltLeftRightTimer > 0f)
			{
				CenterAngle -= Mathf.Atan2(PlatformMovement.LocalSpeedX, 12f) * 57.29578f * 0.5f * Mathf.Clamp01(m_tiltLeftRightTimer);
			}
			if (m_tiltUpDownTimer > 0f)
			{
				CenterAngle += ((!Sein.FaceLeft) ? 1 : -1) * Mathf.Atan2(PlatformMovement.LocalSpeedY, 12f) * 57.29578f * 0.5f * Mathf.Clamp01(m_tiltUpDownTimer);
			}
		}
		if (Sein.Abilities.StandingOnEdge && Sein.Abilities.StandingOnEdge.StandingOnEdge)
		{
			FeetAngle = PlatformMovement.GravityAngle;
		}
	}

	public override void UpdateCharacterState()
	{
		if (CinematicRotation)
		{
			UpdateCinematicRotation();
		}
		else if (Sein.Controller.IsDashing)
		{
			UpdateDashingRotation();
		}
		else if (Sein.Controller.IsStomping)
		{
			UpdateStompingRotation();
		}
		else if (Sein.Controller.IsSwimming && Sein.Abilities.Swimming.IsUnderwater && !Sein.Controller.IsBashing && !Sein.Controller.IsStomping)
		{
			UpdateUnderwaterRotation();
		}
		else
		{
			UpdateRegularRotation();
		}
		UpdateRotation();
	}

	public void UpdateDashingRotation()
	{
		FeetAngle = (HeadAngle = (CenterAngle = 0f));
		if (Sein.IsOnGround)
		{
			FeetAngle = Sein.Abilities.Dash.SpriteRotation;
		}
		else
		{
			CenterAngle = Sein.Abilities.Dash.SpriteRotation;
		}
	}

	public void UpdateStompingRotation()
	{
		FeetAngle = (HeadAngle = (CenterAngle = 0f));
		CenterAngle = Sein.Abilities.Stomp.SpriteRotation;
	}

	public void UpdateRotation()
	{
		if (FeetTransform)
		{
			FeetTransform.eulerAngles = new Vector3(0f, 0f, FeetAngle);
		}
		if (HeadTransform)
		{
			HeadTransform.localEulerAngles = new Vector3(0f, 0f, HeadAngle);
		}
		if (CenterTransform)
		{
			CenterTransform.localEulerAngles = new Vector3(0f, 0f, CenterAngle);
		}
	}

	public override void Serialize(Archive ar)
	{
		ar.Serialize(ref FeetAngle);
		ar.Serialize(ref CenterAngle);
		ar.Serialize(ref m_ceilingAngle);
		ar.Serialize(ref m_groundAngle);
		ar.Serialize(ref m_localPosition);
		ar.Serialize(ref m_wallLeftAngle);
		ar.Serialize(ref m_wallRightAngle);
		if (ar.Reading)
		{
			UpdateRotation();
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

using UnityEngine;

public class SeinSpriteRotationController : CharacterState, ISeinReceiver {
    public void BeginTiltLeftRightInAir(float duration) {
        tiltLeftRightTimer = duration;
    }

    public void BeginTiltUpDownInAir(float duration) {
        tiltUpDownTimer = duration;
    }

    public PlatformMovement PlatformMovement => Sein.PlatformBehaviour.PlatformMovement;

    public SeinCrouch Crouch => Sein.Abilities.Crouch;

    public SeinStomp Stomp => Sein.Abilities.Stomp;

    public SeinBashAttack BashAttack => Sein.Abilities.Bash;

    public bool IsStomping => Stomp && Stomp.Active;

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
    }

    private void UpdateUnderwaterRotation() {
        HeadAngle = 0f;
        FeetAngle = 0f;
        CenterAngle = Sein.Abilities.Swimming.SwimAngle + (!Sein.Controller.FaceLeft ? 0 : 180);
    }

    private void UpdateCinematicRotation() {
        if (PlatformMovement.IsOnGround) {
            groundAngle = Mathf.LerpAngle(groundAngle, PlatformMovement.GroundAngle, 0.1f);
        } else {
            groundAngle = Mathf.LerpAngle(groundAngle, 0f, 0.1f);
        }

        FeetAngle = groundAngle;
        HeadAngle = 0f;
        CenterAngle = 0f;
    }

    private void UpdateRegularRotation() {
        if (tiltLeftRightTimer > 0f) {
            tiltLeftRightTimer = Mathf.Max(tiltLeftRightTimer - Time.deltaTime, 0f);
        }

        if (tiltUpDownTimer > 0f) {
            tiltUpDownTimer = Mathf.Max(tiltUpDownTimer - Time.deltaTime, 0f);
        }

        CenterAngle = 0f;
        HeadAngle = 0f;
        FeetAngle = 0f;
        if (PlatformMovement.HasWallLeft) {
            if (!PlatformMovement.WallLeft.WasOn) {
                wallLeftAngle = !PlatformMovement.WallLeftRayHit ? PlatformMovement.GravityAngle : PlatformMovement.WallLeftAngle;
            } else if (PlatformMovement.WallLeftRayHit) {
                wallLeftAngle = Mathf.LerpAngle(wallLeftAngle, PlatformMovement.WallLeftAngle, 0.2f);
            }

            if (Sein.Abilities.Swimming && Sein.Abilities.Swimming.IsSwimming) {
                FeetAngle = PlatformMovement.GravityAngle;
            } else if (PlatformMovement.IsOnGround && Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft) {
                FeetAngle = PlatformMovement.GravityAngle;
            } else {
                FeetAngle = Mathf.Max(0f, wallLeftAngle);
                HeadAngle = Mathf.Min(0f, wallLeftAngle);
            }
        } else if (PlatformMovement.HasWallRight) {
            if (!PlatformMovement.WallRight.WasOn) {
                wallRightAngle = !PlatformMovement.WallRightRayHit ? PlatformMovement.GravityAngle : PlatformMovement.WallRightAngle;
            } else if (PlatformMovement.WallRightRayHit) {
                wallRightAngle = Mathf.LerpAngle(wallRightAngle, PlatformMovement.WallRightAngle, 0.2f);
            }

            if (Sein.Abilities.Swimming && Sein.Abilities.Swimming.IsSwimming) {
                FeetAngle = PlatformMovement.GravityAngle;
            } else if (PlatformMovement.IsOnGround && !Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft) {
                FeetAngle = PlatformMovement.GravityAngle;
            } else {
                HeadAngle = Mathf.Max(0f, wallRightAngle);
                FeetAngle = Mathf.Min(0f, wallRightAngle);
            }
        } else if (PlatformMovement.IsOnGround) {
            if (Sein.Controller.IsAimingGrenade) {
                groundAngle = PlatformMovement.GroundAngle;
            } else if (!PlatformMovement.Ground.WasOn) {
                groundAngle = !PlatformMovement.GroundRayHit ? PlatformMovement.GravityAngle : PlatformMovement.GroundAngle;
            } else if (PlatformMovement.GroundRayHit) {
                groundAngle = Mathf.LerpAngle(groundAngle, PlatformMovement.GroundAngle, 0.2f);
            }

            if (Sein.Abilities.Swimming && Sein.Abilities.Swimming.IsSwimming) {
                FeetAngle = PlatformMovement.GravityAngle;
            } else if (PlatformMovement.IsOnCeiling && Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft == PlatformMovement.CeilingNormal.x > 0f) {
                FeetAngle = PlatformMovement.GravityAngle;
            } else {
                FeetAngle = groundAngle;
            }
        } else {
            FeetAngle = PlatformMovement.GravityAngle;
            if (tiltLeftRightTimer > 0f) {
                CenterAngle -= Mathf.Atan2(PlatformMovement.LocalSpeedX, 12f) * 57.29578f * 0.5f * Mathf.Clamp01(tiltLeftRightTimer);
            }

            if (tiltUpDownTimer > 0f) {
                CenterAngle += (!Sein.FaceLeft ? 1 : -1) * Mathf.Atan2(PlatformMovement.LocalSpeedY, 12f) * 57.29578f * 0.5f * Mathf.Clamp01(tiltUpDownTimer);
            }
        }

        if (Sein.Abilities.StandingOnEdge && Sein.Abilities.StandingOnEdge.StandingOnEdge) {
            FeetAngle = PlatformMovement.GravityAngle;
        }
    }

    public override void UpdateCharacterState() {
        if (CinematicRotation) {
            UpdateCinematicRotation();
        } else if (Sein.Controller.IsDashing) {
            UpdateDashingRotation();
        } else if (Sein.Controller.IsStomping) {
            UpdateStompingRotation();
        } else if (Sein.Controller.IsSwimming && Sein.Abilities.Swimming.IsUnderwater && !Sein.Controller.IsBashing && !Sein.Controller.IsStomping) {
            UpdateUnderwaterRotation();
        } else {
            UpdateRegularRotation();
        }

        UpdateRotation();
    }

    public void UpdateDashingRotation() {
        FeetAngle = HeadAngle = CenterAngle = 0f;
        if (Sein.IsOnGround) {
            FeetAngle = Sein.Abilities.Dash.SpriteRotation;
        } else {
            CenterAngle = Sein.Abilities.Dash.SpriteRotation;
        }
    }

    public void UpdateStompingRotation() {
        FeetAngle = HeadAngle = CenterAngle = 0f;
        CenterAngle = Sein.Abilities.Stomp.SpriteRotation;
    }

    public void UpdateRotation() {
        if (FeetTransform) {
            FeetTransform.eulerAngles = new Vector3(0f, 0f, FeetAngle);
        }

        if (HeadTransform) {
            HeadTransform.localEulerAngles = new Vector3(0f, 0f, HeadAngle);
        }

        if (CenterTransform) {
            CenterTransform.localEulerAngles = new Vector3(0f, 0f, CenterAngle);
        }
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref FeetAngle);
        ar.Serialize(ref CenterAngle);
        ar.Serialize(ref ceilingAngle);
        ar.Serialize(ref groundAngle);
        ar.Serialize(ref localPosition);
        ar.Serialize(ref wallLeftAngle);
        ar.Serialize(ref wallRightAngle);
        if (ar.Reading) {
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

    private float ceilingAngle;

    private float groundAngle;

    private Vector2 localPosition;

    private float wallLeftAngle;

    private float wallRightAngle;

    private float tiltLeftRightTimer;

    private float tiltUpDownTimer;
}

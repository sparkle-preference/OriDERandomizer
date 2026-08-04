using System;
using Core;
using Game;
using UnityEngine;
using Events = Sein.World.Events;
using Input = Core.Input;

public class SeinSwimming : CharacterState, ISeinReceiver {
    public void ChangeState(State state) {
        if (CurrentState == State.SwimMovingUnderwater && UnderwaterSwimmingSoundProvider) {
            UnderwaterSwimmingSoundProvider.StopAndFadeOut(0.3f);
        }

        CurrentState = state;
    }

    public bool IsUpsideDown => Vector3.Dot(MoonMath.Angle.VectorFromAngle(SwimAngle), !sein.Controller.FaceLeft ? Vector3.left : Vector3.right) > Mathf.Cos(0.87266463f);

    public float RemainingBreath { get; set; }

    public bool HasUnlimitedBreathingUnderwater => sein.PlayerAbilities.WaterBreath.HasAbility;

    public PlatformMovement PlatformMovement => sein.PlatformBehaviour.PlatformMovement;

    public CharacterLeftRightMovement LeftRightMovement => sein.PlatformBehaviour.LeftRightMovement;

    public CharacterGravity Gravity => sein.PlatformBehaviour.Gravity;

    public bool IsSwimming => CurrentState != State.OutOfWater;

    private float WaterSurfacePositionY => currentWater.Bounds.yMax;

    public Rect WaterSurfaceBound {
        get {
            var result = new Rect(currentWater.Bounds);
            result.yMin = result.yMax - 0.5f;
            result.yMax += !sein.PlatformBehaviour.PlatformMovement.IsOnGround ? 0.5f : 0f;
            return result;
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        this.sein = sein;
        this.sein.Abilities.Swimming = this;
    }

    public bool IsSuspended { get; set; }

    public bool IsUnderwater => CurrentState == State.SwimMovingUnderwater || CurrentState == State.SwimIdleUnderwater;

    public void HideBreathingUI() {
        for (var i = 0; i < breathingUIAnimators.Length; i++) {
            breathingUIAnimators[i].ContinueBackward();
        }
    }

    public void ShowBreathingUI() {
        for (var i = 0; i < breathingUIAnimators.Length; i++) {
            breathingUIAnimators[i].ContinueForward();
        }
    }

    public override void Awake() {
        base.Awake();
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
        breathingUIAnimators = BreathingUI.GetComponentsInChildren<LegacyAnimator>();
    }

    public void RestoreBreath() {
        RemainingBreath = Breath;
    }

    public void UpdateDrowning() {
        if (!Events.WaterPurified && CurrentState != State.OutOfWater) {
            RemainingBreath = 0f;
            HideBreathingUI();
        }

        if (HasUnlimitedBreathingUnderwater && Events.WaterPurified) {
            return;
        }

        if (sein.Controller.IsBashing) {
            return;
        }

        if (RemainingBreath > 0f) {
            RemainingBreath -= Time.deltaTime;
        }

        if (RemainingBreath <= 0f) {
            RemainingBreath = 0f;
            if (drowningDelay < 0f) {
                new Damage(DrownDamage, Vector2.zero, transform.position, DamageType.Drowning, gameObject).DealToComponents(Characters.Sein.Mortality.DamageReciever.gameObject);
                drowningDelay = DurationBetweenDrowningDamage;
            }
        }
    }

    public void Start() {
        LeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent += ModifyHorizontalPlatformMovementSettings;
        Gravity.ModifyGravityPlatformMovementSettingsEvent += ModifyGravityPlatformMovementSettings;
    }

    public override void OnDestroy() {
        base.OnDestroy();
        LeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent -= ModifyHorizontalPlatformMovementSettings;
        Gravity.ModifyGravityPlatformMovementSettingsEvent -= ModifyGravityPlatformMovementSettings;
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
    }

    public override void Serialize(Archive ar) {
        CurrentState = (State)ar.Serialize((int)CurrentState);
        ar.Serialize(ref drowningDelay);
        RemainingBreath = ar.Serialize(RemainingBreath);
        ar.Serialize(ref swimIdleTime);
        ar.Serialize(ref swimMovingTime);
        ar.Serialize(ref SwimAngle);
        ar.Serialize(ref SmoothAngleDelta);
    }

    public void OnRestoreCheckpoint() {
        RestoreBreath();
    }

    public void ModifyHorizontalPlatformMovementSettings(HorizontalPlatformMovementSettings settings) {
        switch (CurrentState) {
            case State.OutOfWater:
                break;
            case State.SwimmingOnSurface:
                settings.Air.ApplySpeedMultiplier(SwimmingOnSurfaceHorizontalSpeed);
                settings.Ground.ApplySpeedMultiplier(SwimmingOnSurfaceHorizontalSpeed);
                break;
            case State.SwimMovingUnderwater:
            case State.SwimIdleUnderwater:
                settings.Air.Acceleration = 0f;
                settings.Air.Decceleration = 0f;
                settings.Air.MaxSpeed = float.PositiveInfinity;
                settings.Ground.Acceleration = 0f;
                settings.Ground.Decceleration = 0f;
                settings.Ground.MaxSpeed = float.PositiveInfinity;
                break;
        }
    }

    public void ModifyGravityPlatformMovementSettings(GravityPlatformMovementSettings settings) {
        if (CurrentState == State.SwimmingOnSurface) {
            settings.GravityStrength = 0f;
            settings.MaxFallSpeed = 0f;
        }

        if (CurrentState == State.SwimMovingUnderwater || CurrentState == State.SwimIdleUnderwater) {
            settings.GravityStrength = 0f;
        }
    }

    public override void UpdateCharacterState() {
        if (drowningDelay >= 0f) {
            drowningDelay -= Time.deltaTime;
        }

        switch (CurrentState) {
            case State.OutOfWater:
                UpdateOutOfWaterState();
                return;
            case State.SwimmingOnSurface:
                UpdateSwimmingOnSurfaceState();
                return;
            case State.SwimMovingUnderwater:
                UpdateSwimMovingUnderwaterState();
                return;
            case State.SwimIdleUnderwater:
                UpdateSwimIdleUnderwaterState();
                return;
            default:
                return;
        }
    }

    public void GetOutOfWater() {
        Sound.Play(OutOfWaterSoundProvider.GetSound(null), sein.transform.position, null);
        InstantiateUtility.Instantiate(WaterSplashPrefab, sein.transform.position, Quaternion.identity);
        ChangeState(State.OutOfWater);
        RemainingBreath = Breath;
    }

    public void SwimUnderwater() {
        ChangeState(State.SwimMovingUnderwater);
        SwimAngle = 270f;
        swimIdleTime = 0f;
        swimMovingTime = 0f;
        swimAccelerationTime = 0f;
        Sound.Play(InWaterSoundProvider.GetSound(null), sein.transform.position, null);
        if (sein.Abilities.Bash != null && sein.Abilities.Bash.IsBashing) {
            Sound.Play(BashIntoWaterSoundProvider.GetSound(null), sein.transform.position, null);
        }

        if (sein.Abilities.Stomp && sein.Abilities.Stomp.IsStomping) {
            Sound.Play(StompIntoWaterSoundProvider.GetSound(null), sein.transform.position, null);
        }

        InstantiateUtility.Instantiate(WaterSplashPrefab, sein.transform.position, Quaternion.identity);
        if (!HasUnlimitedBreathingUnderwater) {
            RemainingBreath = Breath;
            ShowBreathingUI();
        }
    }

    public void RemoveUnderwaterSounds() {
        if (ambienceLayer != null) {
            Ambience.RemoveAmbienceLayer(ambienceLayer);
            ambienceLayer = null;
            UnderwaterMixerSnapshot.FadeOut();
        }
    }

    public void UpdateOutOfWaterState() {
        var headPosition = sein.PlatformBehaviour.PlatformMovement.HeadPosition;
        RemoveUnderwaterSounds();
        var i = 0;
        while (i < Zones.WaterZones.Count) {
            var waterZone = Zones.WaterZones[i];
            if (waterZone.Bounds.Contains(headPosition)) {
                currentWater = waterZone;
                sein.PlatformBehaviour.PlatformMovement.LocalSpeedX *= 0.5f;
                if (Mathf.Abs(PlatformMovement.LocalSpeedY) <= SkipSurfaceSpeedIn && WaterSurfaceBound.Contains(PlatformMovement.Position)) {
                    SwimOnSurface();
                    return;
                }

                if (PlatformMovement.LocalSpeedY < 0f) {
                    SwimUnderwater();
                    PlatformMovement.LocalSpeedY *= 0.8f;
                    return;
                }

                currentWater = null;
                return;
            }

            i++;
        }
    }

    public void SwimOnSurface() {
        PlatformMovement.PositionY = WaterSurfacePositionY;
        PlatformMovement.LocalSpeedY = 0f;
        ChangeState(State.SwimmingOnSurface);
        if (sein.Abilities.Carry && sein.Abilities.Carry.IsCarrying) {
            var damage = new Damage(1000f, (sein.transform.position - transform.position).normalized, transform.position, DamageType.Water, gameObject);
            sein.Mortality.DamageReciever.OnRecieveDamage(damage);
        }

        Sound.Play(OutOfWaterSoundProvider.GetSound(null), sein.transform.position, null);
        InstantiateUtility.Instantiate(WaterSplashPrefab, sein.transform.position, Quaternion.identity);
        RestoreBreath();
        HideBreathingUI();
    }

    public void OnDisable() {
        RemoveUnderwaterSounds();
    }

    public void UpdateSwimmingOnSurfaceState() {
        if (!Events.WaterPurified) {
            UpdateDrowning();
        }

        RemoveUnderwaterSounds();
        if (currentWater == null) {
            GetOutOfWater();
            return;
        }

        Vector2 point = sein.PlatformBehaviour.PlatformMovement.Position;
        if (WaterSurfaceBound.Contains(point)) {
            PlatformMovement.Ground.IsOn = false;
            PlatformMovement.GroundNormal = Vector3.up;
            PlatformMovement.PositionY = WaterSurfacePositionY;
            PlatformMovement.LocalSpeedY = 0f;

            sein.PlatformBehaviour.Visuals.Animation.PlayLoop(sein.Input.NormalizedHorizontal != 0 ? Animations.SwimSurface.Moving : Animations.SwimSurface.Idle, 9, ShouldSwimSurfaceAnimationPlay);
            if (SurfaceSwimmingSoundProvider && !SurfaceSwimmingSoundProvider.IsPlaying && sein.Input.NormalizedHorizontal != 0) {
                SurfaceSwimmingSoundProvider.Play();
            }

            if (sein.Controller.CanMove && !sein.Controller.IsBashing) {
                if (sein.Input.Down.Pressed) {
                    SwimUnderwater();
                    PlatformMovement.LocalSpeedY = -DiveUnderwaterSpeed;
                }

                if (Input.Jump.OnPressed) {
                    SurfaceSwimJump();
                }
            }

            return;
        }

        GetOutOfWater();
    }

    public void HorizontalFlip() {
        swimMovingTime = 0f;
        boostAnimationRemainingTime = 0f;
        SwimAngle += 180f;
        sein.Controller.FaceLeft = !sein.Controller.FaceLeft;
        sein.PlatformBehaviour.Visuals.Animation.Play(Animations.SwimFlipHorizontalAnimation, 10, ShouldSwimUnderwaterAnimationPlay);
    }

    public void VerticalFlip() {
        boostAnimationRemainingTime = 0f;
        swimMovingTime = 0f;
        sein.Controller.FaceLeft = !sein.Controller.FaceLeft;
        sein.PlatformBehaviour.Visuals.Animation.Play(Animations.SwimFlipVerticalAnimation, 10, ShouldSwimUnderwaterAnimationPlay);
    }

    public void HorizontalVerticalFlip() {
        swimMovingTime = 0f;
        boostAnimationRemainingTime = 0f;
        SwimAngle += 180f;
        sein.PlatformBehaviour.Visuals.Animation.Play(Animations.SwimFlipHorizontalVerticalAnimation, 10, ShouldSwimUnderwaterAnimationPlay);
    }

    public void OnBash(float angle) {
        if (IsUnderwater) {
            angle += 90f;
            SwimAngle = angle;
            sein.Controller.FaceLeft = MoonMath.Angle.VectorFromAngle(angle).x < 0f;
            swimAccelerationTime = -BashTime;
            ChangeState(State.SwimIdleUnderwater);
        }
    }

    public void ApplySwimmingUnderwaterStuff() {
        if (ambienceLayer == null) {
            ambienceLayer = new Ambience.Layer(SwimmingUnderwaterAmbience, 0.7f, 0.7f, 5);
            Ambience.AddAmbienceLayer(ambienceLayer);
            UnderwaterMixerSnapshot.FadeIn();
        }
    }

    public Vector2 GetAxisInput() {
        if (!sein.Controller.CanMove) {
            return Vector2.zero;
        }

        if (sein.Input.Axis.magnitude > 0.3f) {
            return sein.Input.Axis;
        }

        if (RandomizerSettings.Controls.SwimmingMouseAim) {
            Vector2 oriScreenPos = UI.Cameras.Current.Camera.WorldToScreenPoint(PlatformMovement.Position);
            Vector2 oriUIPos = UI.Cameras.System.GUICamera.Camera.ScreenToWorldPoint(oriScreenPos);
            var cursorAxis = Input.CursorPositionUI - oriUIPos;

            if (cursorAxis.magnitude > 0.5f) {
                return cursorAxis;
            }
        }

        return Vector2.zero;
    }

    public void UpdateSwimMovingUnderwaterState() {
        UpdateDrowning();
        if (UnderwaterSwimmingSoundProvider && !UnderwaterSwimmingSoundProvider.IsPlaying) {
            UnderwaterSwimmingSoundProvider.Play();
        }

        sein.PlatformBehaviour.PlatformMovement.ForceKeepInAir = true;
        var vector = GetAxisInput();
        swimAccelerationTime += 2f * Time.deltaTime;
        Vector2 vector2 = Vector3.down * MaxFallSpeed;
        if (vector != Vector2.zero) {
            swimIdleTime = 0f;
            vector.Normalize();
            var swimAngle = SwimAngle;
            var v = MoonMath.Angle.VectorFromAngle(SwimAngle);
            if (Vector3.Dot(-vector, v) > Mathf.Cos(1.04719758f)) {
                if (IsUpsideDown) {
                    HorizontalVerticalFlip();
                } else {
                    HorizontalFlip();
                }
            } else {
                var target = MoonMath.Angle.AngleFromVector(vector);
                SwimAngle = Mathf.MoveTowardsAngle(SwimAngle, target, SwimAngleDeltaLimit * Time.deltaTime);
                vector = MoonMath.Angle.VectorFromAngle(SwimAngle);
                vector2 = vector * SwimSpeed * RandomizerBonusSkill.ExtremeSpeed;
                if (sein.Controller.CanMove && RandomizerSettings.IsSwimBoosting()) {
                    isBoosting = true;
                    boostTime = Mathf.Min(boostTime, BoostPeakTime);
                }

                if (sein.Controller.CanMove && RandomizerSettings.SwimBoostPressed() && boostAnimationRemainingTime <= 0f && BoostSwimsoundProvider) {
                    Sound.Play(BoostSwimsoundProvider.GetSound(null), transform.position, null);
                    boostAnimationRemainingTime = 0.6666667f;
                }

                if (isBoosting) {
                    boostTime += Time.deltaTime / BoostDuration;
                    vector2 *= SwimSpeedBoostCurve.Evaluate(boostTime);
                }

                if (isBoosting && boostTime > BoostDuration) {
                    isBoosting = false;
                    boostTime = 0f;
                }
            }

            var b = MoonMath.Angle.AngleSubtract(SwimAngle, swimAngle) / Time.deltaTime;
            SmoothAngleDelta = Mathf.Lerp(SmoothAngleDelta, b, 0.1f);
        } else {
            if (swimAccelerationTime > 0f) {
                swimAccelerationTime = 0f;
            }

            if (isBoosting) {
                isBoosting = false;
                boostTime = 0f;
                boostAnimationRemainingTime = 0f;
            }

            if (swimIdleTime > 0.1f) {
                swimMovingTime = 0f;
                if (swimAccelerationTime > 0f) {
                    swimAccelerationTime = 0f;
                }

                if (IsUpsideDown) {
                    VerticalFlip();
                }

                var faceLeft = sein.Controller.FaceLeft;
                float target2 = !faceLeft ? 0 : 180;
                if (MoonMath.Angle.AngleSubtract(SwimAngle, target2) > 0f) {
                    sein.PlatformBehaviour.Visuals.Animation.Play(faceLeft ? Animations.SwimMiddleToIdleClockwise : Animations.SwimMiddleToIdleAntiClockwise, 10, ShouldIdleUnderwaterAnimationPlay);
                } else {
                    sein.PlatformBehaviour.Visuals.Animation.Play(!faceLeft ? Animations.SwimMiddleToIdleClockwise : Animations.SwimMiddleToIdleAntiClockwise, 10, ShouldIdleUnderwaterAnimationPlay);
                }

                ChangeState(State.SwimIdleUnderwater);
            }

            swimIdleTime += Time.deltaTime;
        }

        PlatformMovement.LocalSpeed = Vector3.Lerp(PlatformMovement.LocalSpeed, vector2, AccelerationOverTime.Evaluate(swimAccelerationTime));
        if (IsUpsideDown && Math.Abs(SmoothAngleDelta) < 10f) {
            VerticalFlip();
        }

        ApplySwimmingUnderwaterStuff();
        if (boostAnimationRemainingTime > 0f) {
            boostAnimationRemainingTime -= Time.deltaTime;
            var min = Mathf.RoundToInt(Animations.AnimationFromBend.Evaluate(SmoothAngleDelta * (!sein.Controller.FaceLeft ? -1 : 1)) * (Animations.SwimJumpLeft.Length - 1));
            var num = Mathf.Clamp(0, min, Animations.SwimJumpLeft.Length - 1);
            sein.PlatformBehaviour.Visuals.Animation.PlayLoop(Animations.SwimJumpLeft[num], 9, ShouldSwimUnderwaterAnimationPlay, true);
        } else {
            var min2 = Mathf.RoundToInt(Animations.AnimationFromBend.Evaluate(SmoothAngleDelta * (!sein.Controller.FaceLeft ? -1 : 1)) * (Animations.SwimHorizontal.Length - 1));
            var num2 = Mathf.Clamp(0, min2, Animations.SwimHorizontal.Length - 1);
            sein.PlatformBehaviour.Visuals.Animation.PlayLoop(Animations.SwimHorizontal[num2], 9, ShouldSwimUnderwaterAnimationPlay, true);
        }

        HandleLeavingWater();
    }

    public void UpdateSwimIdleUnderwaterState() {
        UpdateDrowning();
        var vector = GetAxisInput();
        swimAccelerationTime += Time.deltaTime;
        if (vector != Vector2.zero) {
            if (swimAccelerationTime > 0f) {
                swimAccelerationTime = 0f;
            }

            swimIdleTime = 0f;
            ChangeState(State.SwimMovingUnderwater);
        } else {
            float target = !sein.Controller.FaceLeft ? 0 : 180;
            SwimAngle = Mathf.MoveTowardsAngle(SwimAngle, target, SwimAngleDeltaLimit * Time.deltaTime);
            sein.PlatformBehaviour.Visuals.Animation.PlayLoop(Animations.SwimIdle, 9, ShouldIdleUnderwaterAnimationPlay, true);
        }

        PlatformMovement.LocalSpeed = Vector3.Lerp(PlatformMovement.LocalSpeed, Vector3.down * MaxFallSpeed, AccelerationOverTime.Evaluate(swimAccelerationTime));
        ApplySwimmingUnderwaterStuff();
        HandleLeavingWater();
    }

    public void HandleLeavingWater() {
        var position = sein.PlatformBehaviour.PlatformMovement.Position;
        for (var i = 0; i < Zones.WaterZones.Count; i++) {
            var waterZone = Zones.WaterZones[i];
            if (waterZone.Bounds.Contains(position)) {
                currentWater = waterZone;
                return;
            }
        }

        if (RemainingBreath / Breath > 0.5f) {
            if (EmergeHighBreathSoundProvider) {
                Sound.Play(EmergeHighBreathSoundProvider.GetSound(null), transform.position, null);
            }
        } else if (RemainingBreath / Breath > 0.15f) {
            if (EmergeMedBreathSoundProvider) {
                Sound.Play(EmergeMedBreathSoundProvider.GetSound(null), transform.position, null);
            }
        } else if (EmergeLowBreathSoundProvider) {
            Sound.Play(EmergeLowBreathSoundProvider.GetSound(null), transform.position, null);
        }

        RestoreBreath();
        HideBreathingUI();
        if (currentWater.HasTopSurface && WaterSurfaceBound.Contains(PlatformMovement.Position)) {
            SwimOnSurface();
            return;
        }

        GetOutOfWater();
    }

    public bool CanJump() {
        return CurrentState == State.SwimmingOnSurface || CurrentState == State.SwimMovingUnderwater;
    }

    public void SurfaceSwimJump() {
        PlatformMovement.LocalSpeedY = JumpOutOfWaterSpeed * RandomizerBonus.Jumpscale;
        if (sein.Input.NormalizedHorizontal == 0) {
            sein.PlatformBehaviour.Visuals.Animation.Play(Animations.JumpOutOfWater.Idle, 10, ShouldJumpOutOfWaterAnimationIdleKeepPlaying);
        } else {
            sein.PlatformBehaviour.Visuals.Animation.Play(Animations.JumpOutOfWater.Moving, 10, ShouldJumpOutOfWaterAnimationMovingKeepPlaying);
        }

        sein.ResetAirLimits();
        GetOutOfWater();
    }

    public bool ShouldSwimUnderwaterAnimationPlay() {
        return CurrentState == State.SwimMovingUnderwater;
    }

    public bool ShouldIdleUnderwaterAnimationPlay() {
        return CurrentState == State.SwimIdleUnderwater;
    }

    public bool ShouldSwimSurfaceAnimationPlay() {
        return CurrentState == State.SwimmingOnSurface;
    }

    public bool ShouldJumpOutOfWaterAnimationIdleKeepPlaying() {
        return PlatformMovement.IsInAir && (!sein.Controller.CanMove || sein.Input.NormalizedHorizontal == 0) && (!IsSwimming || !PlatformMovement.Falling);
    }

    public bool ShouldJumpOutOfWaterAnimationMovingKeepPlaying() {
        return PlatformMovement.IsInAir && (!sein.Controller.CanMove || sein.Input.NormalizedHorizontal != 0) && (!IsSwimming || !PlatformMovement.Falling);
    }

    public SoundProvider SwimmingUnderwaterAmbience;

    public MixerSnapshot UnderwaterMixerSnapshot;

    public State CurrentState;

    public SwimmingAnimations Animations;

    public float Breath = 3f;

    public GameObject BreathingUI;

    public float DiveUnderwaterSpeed = 3f;

    public float DurationBetweenDrowningDamage = 1f;

    public SoundProvider InWaterSoundProvider;

    public SoundProvider BashIntoWaterSoundProvider;

    public SoundProvider StompIntoWaterSoundProvider;

    public float JumpOutOfWaterSpeed = 20f;

    public SoundProvider OutOfWaterSoundProvider;

    public float SkipSurfaceSpeedIn = 20f;

    public SoundSource SurfaceSwimmingSoundProvider;

    public SoundSource UnderwaterSwimmingSoundProvider;

    public SoundProvider EmergeHighBreathSoundProvider;

    public SoundProvider EmergeMedBreathSoundProvider;

    public SoundProvider EmergeLowBreathSoundProvider;

    public SoundProvider BoostSwimsoundProvider;

    public float SwimSpeed = 6f;

    public AnimationCurve SwimSpeedBoostCurve;

    public float BoostPeakTime = 0.2f;

    private float boostTime;

    public float BoostDuration;

    private bool isBoosting;

    public float SwimAngle;

    public float SwimAngleDeltaLimit = 100f;

    private float swimMovingTime;

    private float swimIdleTime;

    private float swimAccelerationTime;

    public HorizontalPlatformMovementSettings.SpeedMultiplierSet SwimmingOnSurfaceHorizontalSpeed;

    public GameObject WaterSplashPrefab;

    private WaterZone currentWater;

    private float drowningDelay;

    private SeinCharacter sein;

    private LegacyAnimator[] breathingUIAnimators;

    public float DrownDamage = 5f;

    private Ambience.Layer ambienceLayer;

    public float MaxFallSpeed = 4f;

    public float BashTime = 1f;

    public float SmoothAngleDelta;

    public AnimationCurve AccelerationOverTime;

    private float boostAnimationRemainingTime;

    [Serializable]
    public class MovingAndIdleAnimationPair {
        public TextureAnimationWithTransitions Idle;

        public TextureAnimationWithTransitions Moving;
    }

    public enum State {
        OutOfWater,
        SwimmingOnSurface,
        SwimMovingUnderwater,
        SwimIdleUnderwater,
    }

    [Serializable]
    public class SwimmingAnimations {
        public MovingAndIdleAnimationPair JumpOutOfWater;

        public MovingAndIdleAnimationPair SwimSurface;

        public TextureAnimationWithTransitions[] SwimHorizontal;

        public TextureAnimationWithTransitions[] SwimJumpLeft;

        public AnimationCurve AnimationFromBend;

        public TextureAnimationWithTransitions SwimIdle;

        public TextureAnimationWithTransitions SwimMiddleToIdleClockwise;

        public TextureAnimationWithTransitions SwimMiddleToIdleAntiClockwise;

        public TextureAnimationWithTransitions SwimIdleToSwimMiddle;

        public TextureAnimationWithTransitions SwimFlipHorizontalAnimation;

        public TextureAnimationWithTransitions SwimFlipVerticalAnimation;

        public TextureAnimationWithTransitions SwimFlipHorizontalVerticalAnimation;
    }
}

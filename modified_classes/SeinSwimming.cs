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

    public bool IsUpsideDown => Vector3.Dot(MoonMath.Angle.VectorFromAngle(SwimAngle), !m_sein.Controller.FaceLeft ? Vector3.left : Vector3.right) > Mathf.Cos(0.87266463f);

    public float RemainingBreath { get; set; }

    public bool HasUnlimitedBreathingUnderwater => m_sein.PlayerAbilities.WaterBreath.HasAbility;

    public PlatformMovement PlatformMovement => m_sein.PlatformBehaviour.PlatformMovement;

    public CharacterLeftRightMovement LeftRightMovement => m_sein.PlatformBehaviour.LeftRightMovement;

    public CharacterGravity Gravity => m_sein.PlatformBehaviour.Gravity;

    public bool IsSwimming => CurrentState != State.OutOfWater;

    private float WaterSurfacePositionY => m_currentWater.Bounds.yMax;

    public Rect WaterSurfaceBound {
        get {
            var result = new Rect(m_currentWater.Bounds);
            result.yMin = result.yMax - 0.5f;
            result.yMax += !m_sein.PlatformBehaviour.PlatformMovement.IsOnGround ? 0.5f : 0f;
            return result;
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        m_sein = sein;
        m_sein.Abilities.Swimming = this;
    }

    public bool IsSuspended { get; set; }

    public bool IsUnderwater => CurrentState == State.SwimMovingUnderwater || CurrentState == State.SwimIdleUnderwater;

    public void HideBreathingUI() {
        for (var i = 0; i < m_breathingUIAnimators.Length; i++) {
            m_breathingUIAnimators[i].ContinueBackward();
        }
    }

    public void ShowBreathingUI() {
        for (var i = 0; i < m_breathingUIAnimators.Length; i++) {
            m_breathingUIAnimators[i].ContinueForward();
        }
    }

    public override void Awake() {
        base.Awake();
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
        m_breathingUIAnimators = BreathingUI.GetComponentsInChildren<LegacyAnimator>();
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

        if (m_sein.Controller.IsBashing) {
            return;
        }

        if (RemainingBreath > 0f) {
            RemainingBreath -= Time.deltaTime;
        }

        if (RemainingBreath <= 0f) {
            RemainingBreath = 0f;
            if (m_drowningDelay < 0f) {
                new Damage(DrownDamage, Vector2.zero, transform.position, DamageType.Drowning, gameObject).DealToComponents(Characters.Sein.Mortality.DamageReciever.gameObject);
                m_drowningDelay = DurationBetweenDrowningDamage;
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
        ar.Serialize(ref m_drowningDelay);
        RemainingBreath = ar.Serialize(RemainingBreath);
        ar.Serialize(ref m_swimIdleTime);
        ar.Serialize(ref m_swimMovingTime);
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
        if (m_drowningDelay >= 0f) {
            m_drowningDelay -= Time.deltaTime;
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
        Sound.Play(OutOfWaterSoundProvider.GetSound(null), m_sein.transform.position, null);
        InstantiateUtility.Instantiate(WaterSplashPrefab, m_sein.transform.position, Quaternion.identity);
        ChangeState(State.OutOfWater);
        RemainingBreath = Breath;
    }

    public void SwimUnderwater() {
        ChangeState(State.SwimMovingUnderwater);
        SwimAngle = 270f;
        m_swimIdleTime = 0f;
        m_swimMovingTime = 0f;
        m_swimAccelerationTime = 0f;
        Sound.Play(InWaterSoundProvider.GetSound(null), m_sein.transform.position, null);
        if (m_sein.Abilities.Bash != null && m_sein.Abilities.Bash.IsBashing) {
            Sound.Play(BashIntoWaterSoundProvider.GetSound(null), m_sein.transform.position, null);
        }

        if (m_sein.Abilities.Stomp && m_sein.Abilities.Stomp.IsStomping) {
            Sound.Play(StompIntoWaterSoundProvider.GetSound(null), m_sein.transform.position, null);
        }

        InstantiateUtility.Instantiate(WaterSplashPrefab, m_sein.transform.position, Quaternion.identity);
        if (!HasUnlimitedBreathingUnderwater) {
            RemainingBreath = Breath;
            ShowBreathingUI();
        }
    }

    public void RemoveUnderwaterSounds() {
        if (m_ambienceLayer != null) {
            Ambience.RemoveAmbienceLayer(m_ambienceLayer);
            m_ambienceLayer = null;
            UnderwaterMixerSnapshot.FadeOut();
        }
    }

    public void UpdateOutOfWaterState() {
        var headPosition = m_sein.PlatformBehaviour.PlatformMovement.HeadPosition;
        RemoveUnderwaterSounds();
        var i = 0;
        while (i < Zones.WaterZones.Count) {
            var waterZone = Zones.WaterZones[i];
            if (waterZone.Bounds.Contains(headPosition)) {
                m_currentWater = waterZone;
                m_sein.PlatformBehaviour.PlatformMovement.LocalSpeedX *= 0.5f;
                if (Mathf.Abs(PlatformMovement.LocalSpeedY) <= SkipSurfaceSpeedIn && WaterSurfaceBound.Contains(PlatformMovement.Position)) {
                    SwimOnSurface();
                    return;
                }

                if (PlatformMovement.LocalSpeedY < 0f) {
                    SwimUnderwater();
                    PlatformMovement.LocalSpeedY *= 0.8f;
                    return;
                }

                m_currentWater = null;
                return;
            }

            i++;
        }
    }

    public void SwimOnSurface() {
        PlatformMovement.PositionY = WaterSurfacePositionY;
        PlatformMovement.LocalSpeedY = 0f;
        ChangeState(State.SwimmingOnSurface);
        if (m_sein.Abilities.Carry && m_sein.Abilities.Carry.IsCarrying) {
            var damage = new Damage(1000f, (m_sein.transform.position - transform.position).normalized, transform.position, DamageType.Water, gameObject);
            m_sein.Mortality.DamageReciever.OnRecieveDamage(damage);
        }

        Sound.Play(OutOfWaterSoundProvider.GetSound(null), m_sein.transform.position, null);
        InstantiateUtility.Instantiate(WaterSplashPrefab, m_sein.transform.position, Quaternion.identity);
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
        if (m_currentWater == null) {
            GetOutOfWater();
            return;
        }

        Vector2 point = m_sein.PlatformBehaviour.PlatformMovement.Position;
        if (WaterSurfaceBound.Contains(point)) {
            PlatformMovement.Ground.IsOn = false;
            PlatformMovement.GroundNormal = Vector3.up;
            PlatformMovement.PositionY = WaterSurfacePositionY;
            PlatformMovement.LocalSpeedY = 0f;

            m_sein.PlatformBehaviour.Visuals.Animation.PlayLoop(m_sein.Input.NormalizedHorizontal != 0 ? Animations.SwimSurface.Moving : Animations.SwimSurface.Idle, 9, ShouldSwimSurfaceAnimationPlay);
            if (SurfaceSwimmingSoundProvider && !SurfaceSwimmingSoundProvider.IsPlaying && m_sein.Input.NormalizedHorizontal != 0) {
                SurfaceSwimmingSoundProvider.Play();
            }

            if (m_sein.Controller.CanMove && !m_sein.Controller.IsBashing) {
                if (m_sein.Input.Down.Pressed) {
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
        m_swimMovingTime = 0f;
        m_boostAnimationRemainingTime = 0f;
        SwimAngle += 180f;
        m_sein.Controller.FaceLeft = !m_sein.Controller.FaceLeft;
        m_sein.PlatformBehaviour.Visuals.Animation.Play(Animations.SwimFlipHorizontalAnimation, 10, ShouldSwimUnderwaterAnimationPlay);
    }

    public void VerticalFlip() {
        m_boostAnimationRemainingTime = 0f;
        m_swimMovingTime = 0f;
        m_sein.Controller.FaceLeft = !m_sein.Controller.FaceLeft;
        m_sein.PlatformBehaviour.Visuals.Animation.Play(Animations.SwimFlipVerticalAnimation, 10, ShouldSwimUnderwaterAnimationPlay);
    }

    public void HorizontalVerticalFlip() {
        m_swimMovingTime = 0f;
        m_boostAnimationRemainingTime = 0f;
        SwimAngle += 180f;
        m_sein.PlatformBehaviour.Visuals.Animation.Play(Animations.SwimFlipHorizontalVerticalAnimation, 10, ShouldSwimUnderwaterAnimationPlay);
    }

    public void OnBash(float angle) {
        if (IsUnderwater) {
            angle += 90f;
            SwimAngle = angle;
            m_sein.Controller.FaceLeft = MoonMath.Angle.VectorFromAngle(angle).x < 0f;
            m_swimAccelerationTime = -BashTime;
            ChangeState(State.SwimIdleUnderwater);
        }
    }

    public void ApplySwimmingUnderwaterStuff() {
        if (m_ambienceLayer == null) {
            m_ambienceLayer = new Ambience.Layer(SwimmingUnderwaterAmbience, 0.7f, 0.7f, 5);
            Ambience.AddAmbienceLayer(m_ambienceLayer);
            UnderwaterMixerSnapshot.FadeIn();
        }
    }

    public Vector2 GetAxisInput() {
        if (!m_sein.Controller.CanMove) {
            return Vector2.zero;
        }

        if (m_sein.Input.Axis.magnitude > 0.3f) {
            return m_sein.Input.Axis;
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

        m_sein.PlatformBehaviour.PlatformMovement.ForceKeepInAir = true;
        var vector = GetAxisInput();
        m_swimAccelerationTime += 2f * Time.deltaTime;
        Vector2 vector2 = Vector3.down * MaxFallSpeed;
        if (vector != Vector2.zero) {
            m_swimIdleTime = 0f;
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
                if (m_sein.Controller.CanMove && RandomizerSettings.IsSwimBoosting()) {
                    m_isBoosting = true;
                    m_boostTime = Mathf.Min(m_boostTime, BoostPeakTime);
                }

                if (m_sein.Controller.CanMove && RandomizerSettings.SwimBoostPressed() && m_boostAnimationRemainingTime <= 0f && BoostSwimsoundProvider) {
                    Sound.Play(BoostSwimsoundProvider.GetSound(null), transform.position, null);
                    m_boostAnimationRemainingTime = 0.6666667f;
                }

                if (m_isBoosting) {
                    m_boostTime += Time.deltaTime / BoostDuration;
                    vector2 *= SwimSpeedBoostCurve.Evaluate(m_boostTime);
                }

                if (m_isBoosting && m_boostTime > BoostDuration) {
                    m_isBoosting = false;
                    m_boostTime = 0f;
                }
            }

            var b = MoonMath.Angle.AngleSubtract(SwimAngle, swimAngle) / Time.deltaTime;
            SmoothAngleDelta = Mathf.Lerp(SmoothAngleDelta, b, 0.1f);
        } else {
            if (m_swimAccelerationTime > 0f) {
                m_swimAccelerationTime = 0f;
            }

            if (m_isBoosting) {
                m_isBoosting = false;
                m_boostTime = 0f;
                m_boostAnimationRemainingTime = 0f;
            }

            if (m_swimIdleTime > 0.1f) {
                m_swimMovingTime = 0f;
                if (m_swimAccelerationTime > 0f) {
                    m_swimAccelerationTime = 0f;
                }

                if (IsUpsideDown) {
                    VerticalFlip();
                }

                var faceLeft = m_sein.Controller.FaceLeft;
                float target2 = !faceLeft ? 0 : 180;
                if (MoonMath.Angle.AngleSubtract(SwimAngle, target2) > 0f) {
                    m_sein.PlatformBehaviour.Visuals.Animation.Play(faceLeft ? Animations.SwimMiddleToIdleClockwise : Animations.SwimMiddleToIdleAntiClockwise, 10, ShouldIdleUnderwaterAnimationPlay);
                } else {
                    m_sein.PlatformBehaviour.Visuals.Animation.Play(!faceLeft ? Animations.SwimMiddleToIdleClockwise : Animations.SwimMiddleToIdleAntiClockwise, 10, ShouldIdleUnderwaterAnimationPlay);
                }

                ChangeState(State.SwimIdleUnderwater);
            }

            m_swimIdleTime += Time.deltaTime;
        }

        PlatformMovement.LocalSpeed = Vector3.Lerp(PlatformMovement.LocalSpeed, vector2, AccelerationOverTime.Evaluate(m_swimAccelerationTime));
        if (IsUpsideDown && Math.Abs(SmoothAngleDelta) < 10f) {
            VerticalFlip();
        }

        ApplySwimmingUnderwaterStuff();
        if (m_boostAnimationRemainingTime > 0f) {
            m_boostAnimationRemainingTime -= Time.deltaTime;
            var min = Mathf.RoundToInt(Animations.AnimationFromBend.Evaluate(SmoothAngleDelta * (!m_sein.Controller.FaceLeft ? -1 : 1)) * (Animations.SwimJumpLeft.Length - 1));
            var num = Mathf.Clamp(0, min, Animations.SwimJumpLeft.Length - 1);
            m_sein.PlatformBehaviour.Visuals.Animation.PlayLoop(Animations.SwimJumpLeft[num], 9, ShouldSwimUnderwaterAnimationPlay, true);
        } else {
            var min2 = Mathf.RoundToInt(Animations.AnimationFromBend.Evaluate(SmoothAngleDelta * (!m_sein.Controller.FaceLeft ? -1 : 1)) * (Animations.SwimHorizontal.Length - 1));
            var num2 = Mathf.Clamp(0, min2, Animations.SwimHorizontal.Length - 1);
            m_sein.PlatformBehaviour.Visuals.Animation.PlayLoop(Animations.SwimHorizontal[num2], 9, ShouldSwimUnderwaterAnimationPlay, true);
        }

        HandleLeavingWater();
    }

    public void UpdateSwimIdleUnderwaterState() {
        UpdateDrowning();
        var vector = GetAxisInput();
        m_swimAccelerationTime += Time.deltaTime;
        if (vector != Vector2.zero) {
            if (m_swimAccelerationTime > 0f) {
                m_swimAccelerationTime = 0f;
            }

            m_swimIdleTime = 0f;
            ChangeState(State.SwimMovingUnderwater);
        } else {
            float target = !m_sein.Controller.FaceLeft ? 0 : 180;
            SwimAngle = Mathf.MoveTowardsAngle(SwimAngle, target, SwimAngleDeltaLimit * Time.deltaTime);
            m_sein.PlatformBehaviour.Visuals.Animation.PlayLoop(Animations.SwimIdle, 9, ShouldIdleUnderwaterAnimationPlay, true);
        }

        PlatformMovement.LocalSpeed = Vector3.Lerp(PlatformMovement.LocalSpeed, Vector3.down * MaxFallSpeed, AccelerationOverTime.Evaluate(m_swimAccelerationTime));
        ApplySwimmingUnderwaterStuff();
        HandleLeavingWater();
    }

    public void HandleLeavingWater() {
        var position = m_sein.PlatformBehaviour.PlatformMovement.Position;
        for (var i = 0; i < Zones.WaterZones.Count; i++) {
            var waterZone = Zones.WaterZones[i];
            if (waterZone.Bounds.Contains(position)) {
                m_currentWater = waterZone;
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
        if (m_currentWater.HasTopSurface && WaterSurfaceBound.Contains(PlatformMovement.Position)) {
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
        if (m_sein.Input.NormalizedHorizontal == 0) {
            m_sein.PlatformBehaviour.Visuals.Animation.Play(Animations.JumpOutOfWater.Idle, 10, ShouldJumpOutOfWaterAnimationIdleKeepPlaying);
        } else {
            m_sein.PlatformBehaviour.Visuals.Animation.Play(Animations.JumpOutOfWater.Moving, 10, ShouldJumpOutOfWaterAnimationMovingKeepPlaying);
        }

        m_sein.ResetAirLimits();
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
        return PlatformMovement.IsInAir && (!m_sein.Controller.CanMove || m_sein.Input.NormalizedHorizontal == 0) && (!IsSwimming || !PlatformMovement.Falling);
    }

    public bool ShouldJumpOutOfWaterAnimationMovingKeepPlaying() {
        return PlatformMovement.IsInAir && (!m_sein.Controller.CanMove || m_sein.Input.NormalizedHorizontal != 0) && (!IsSwimming || !PlatformMovement.Falling);
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

    public float SkipSurfaceSpeedOut = 10f;

    public SoundSource SurfaceSwimmingSoundProvider;

    public SoundSource UnderwaterSwimmingSoundProvider;

    public SoundProvider EmergeHighBreathSoundProvider;

    public SoundProvider EmergeMedBreathSoundProvider;

    public SoundProvider EmergeLowBreathSoundProvider;

    public SoundProvider BoostSwimsoundProvider;

    public float SwimGravity = 13f;

    public float SwimSpeed = 6f;

    public AnimationCurve SwimSpeedBoostCurve;

    public float BoostPeakTime = 0.2f;

    private float m_boostTime;

    public float BoostDuration;

    private bool m_isBoosting;

    public float SwimAngle;

    public float SwimAngleDeltaLimit = 100f;

    private float m_swimMovingTime;

    private float m_swimIdleTime;

    private float m_swimAccelerationTime;

    public float SwimUpwardsGravity = 13f;

    public HorizontalPlatformMovementSettings.SpeedMultiplierSet SwimmingOnSurfaceHorizontalSpeed;

    public GameObject WaterSplashPrefab;

    private WaterZone m_currentWater;

    private float m_drowningDelay;

    private SeinCharacter m_sein;

    private LegacyAnimator[] m_breathingUIAnimators;

    public float DrownDamage = 5f;

    private Ambience.Layer m_ambienceLayer;

    public bool CanHorizontalSwimJump;

    public float Deceleration = 10f;

    public float MaxFallSpeed = 4f;

    public float BashTime = 1f;

    public float SmoothAngleDelta;

    public AnimationCurve AccelerationOverTime;

    private float m_boostAnimationRemainingTime;

    public bool CanJumpUnderwater;

    public bool HoldAToSwimLoop;

    public float SwimJumpSpeedDelta = 100f;

    [Serializable]
    public class MovingAndIdleAnimationPair {
        public TextureAnimationWithTransitions Idle;

        public TextureAnimationWithTransitions Moving;
    }

    public enum State {
        OutOfWater,
        SwimmingOnSurface,
        SwimMovingUnderwater,
        SwimIdleUnderwater
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

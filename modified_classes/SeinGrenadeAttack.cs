using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinGrenadeAttack : CharacterState, ISeinReceiver {
    private bool IsGrabbingWall {
        get { return m_sein.Controller.IsGrabbingWall; }
    }

    private bool IsInAir {
        get { return !m_isAiming; }
    }

    private void ResetAimToDefault() {
        SetAimVelocity(new Vector2(14f, 16f));
    }

    private int PickAnimationIndex(int length) {
        return Mathf.Clamp(Mathf.FloorToInt((!IsGrabbingWall ? Mathf.InverseLerp(MinAimGroundAnimationAngle, MaxAimGroundAnimationAngle, m_animationAimAngle) : Mathf.InverseLerp(MinAimWallAnimationAngle, MaxAimWallAnimationAngle, m_animationAimAngle)) * length), 0, length - 1);
    }

    private float IndexToAnimationAngle(int index, int length) {
        float t = index / (float)length;
        if (IsGrabbingWall) {
            return Mathf.Lerp(MinAimWallAnimationAngle, MaxAimWallAnimationAngle, t);
        }

        return Mathf.Lerp(MinAimGroundAnimationAngle, MaxAimGroundAnimationAngle, t);
    }

    private TextureAnimationWithTransitions PickAnimation(TextureAnimationWithTransitions[] animations) {
        int num = PickAnimationIndex(animations.Length);
        return animations[num];
    }

    private float EnergyCostFinal {
        get { return 0f; }
    }

    private bool HasGrenadeEfficiencySkill() {
        return m_sein.PlayerAbilities.GrenadeEfficiency.HasAbility;
    }

    private bool HasEnoughEnergy {
        get { return m_sein.Energy.CanAfford(EnergyCostFinal); }
    }

    private void SpendEnergy() {
        m_sein.Energy.Spend(EnergyCostFinal);
    }

    private void RestoreEnergy() {
        m_sein.Energy.Gain(EnergyCostFinal);
    }

    public void Start() {
        CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent += ModifyHorizontalPlatformMovementSettings;
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
    }

    public override void OnDestroy() {
        base.OnDestroy();
        CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent -= ModifyHorizontalPlatformMovementSettings;
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
    }

    public void OnRestoreCheckpoint() {
        CancelAiming();
    }

    public CharacterLeftRightMovement CharacterLeftRightMovement {
        get { return m_sein.PlatformBehaviour.LeftRightMovement; }
    }

    public CharacterGravity CharacterGravity {
        get { return m_sein.PlatformBehaviour.Gravity; }
    }

    private void ModifyHorizontalPlatformMovementSettings(HorizontalPlatformMovementSettings settings) {
        if (m_isAiming) {
            settings.Ground.Acceleration = 0f;
            settings.Ground.MaxSpeed = 0f;
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        m_sein = sein;
        sein.Abilities.Grenade = this;
    }

    public override void UpdateCharacterState() {
        if (m_sein.IsSuspended) {
            return;
        }

        if (m_sein.Controller.InputLocked) {
            return;
        }

        if (SeinAbilityRestrictZone.IsInside()) {
            return;
        }

        base.UpdateCharacterState();
        if (m_isAiming) {
            UpdateAiming();
            return;
        }

        UpdateNormal();
    }

    private bool HasGrenadeUpgrade() {
        return m_sein.PlayerAbilities.GrenadeUpgrade.HasAbility;
    }

    private Vector3 GrenadeSpawnPosition {
        get { return m_sein.Position; }
    }

    private SpiritGrenade SpawnGrenade(Vector2 velocity) {
        RefreshListOfQuickSpiritGrenades();
        if (m_spiritGrenades.Count >= MaxSpamGrenades) {
            m_spiritGrenades[0].Explode();
            m_spiritGrenades.RemoveAt(0);
        }

        SpiritGrenade component = ((GameObject)InstantiateUtility.Instantiate(!HasGrenadeUpgrade() ? Grenade : GrenadeUpgraded, GrenadeSpawnPosition, Quaternion.identity)).GetComponent<SpiritGrenade>();
        component.SetTrajectory(velocity);
        m_spiritGrenades.Add(component);
        if (m_autoTarget as Component != null) {
            component.Duration = TimeToTarget(velocity, m_autoTarget) + 0.2f;
            m_autoTarget = null;
        }

        return component;
    }

    private void RefreshListOfQuickSpiritGrenades() {
        m_spiritGrenades.RemoveAll(a => a == null);
    }

    public bool IsAiming {
        get { return m_isAiming; }
    }

    public bool CanAim {
        get { return !m_sein.PlatformBehaviour.PlatformMovement.MovingHorizontally && (m_sein.IsOnGround || IsGrabbingWall); }
    }

    public void PlayAimAnimation() {
        m_sein.Animation.PlayLoop(PickAnimation(!IsGrabbingWall ? AimingAnimations : WallAimingAnimations), 154, KeepPlayingAimAnimation, true);
    }

    public void PlayThrowAnimation() {
        if (Mathf.Approximately(Mathf.Abs(m_rawAimOffset.x), QuickThrowSpeed.x) && Mathf.Approximately(m_rawAimOffset.y, QuickThrowSpeed.y)) {
            m_sein.Animation.Play(!IsGrabbingWall ? QuickThrow.IdleThrowAnimation : QuickThrow.WallThrowAnimation, 154, KeepPlayingThrowAnimation);
            return;
        }

        m_sein.Animation.Play(PickAnimation(!IsGrabbingWall ? ThrowAnimations : WallThrowAnimations), 154, KeepPlayingThrowAnimation);
    }

    public void PlayThrowSound() {
        Sound.Play(ThrowGrenadeSound.GetSound(null), transform.position, null);
    }

    public float GrenadeGravity {
        get { return Trajectory.Gravity; }
    }

    public void UpdateAiming() {
        if (Input.LeftShoulder.Released) {
            m_lockPressingInputTime = 0.64f;
            SpawnGrenade(m_rawAimOffset);
            PlayThrowAnimation();
            EndAiming();
            PlayThrowSound();
            return;
        }

        if (Input.Jump.OnPressed || Input.Cancel.OnPressed || !CanAim) {
            CancelAiming();
            return;
        }

        m_sein.Speed = Vector2.zero;
        if (RandomizerRebinding.ResetGrenadeAim.OnPressed) {
            ResetAimToDefault();
        }

        Vector2 axis = Input.Axis;
        if (!RandomizerSettings.Controls.FastGrenadeAim) {
            Vector2 b = AimSpeed.Evaluate(axis.magnitude) * axis.normalized * RandomizerSettings.Controls.GrenadeAimSpeed;
            if (b.magnitude > 0f) {
                m_autoAim = false;
            }

            m_rawAimOffset += b;
        } else {
            float greater = Math.Max(Math.Abs(axis.x), Math.Abs(axis.y));
            if (greater > 0f) {
                m_rawAimOffset = axis * axis.sqrMagnitude * Math.Min(Math.Abs(UI.Cameras.Current.OffsetController.Offset.z), MaxAimDistance) / greater + Vector2.up * CursorSpeedYOffset;
            } else {
                m_rawAimOffset = Vector2.up * CursorSpeedYOffset;
            }

            m_autoAim = false;
        }

        if (m_autoAim) {
            AutoTarget();
        } else {
            m_autoTarget = null;
        }

        ClampAim();
        if (Input.CursorMoved) {
            Vector2 v = UI.Cameras.Current.Camera.WorldToScreenPoint(transform.position);
            Vector2 b2 = UI.Cameras.System.GUICamera.ScreenToWorldPoint(v);
            m_rawAimOffset = (Input.CursorPositionUI - b2) * CursorSpeedMultiplier + Vector2.up * CursorSpeedYOffset;
            m_autoAim = false;
            ClampAim();
        }

        m_aimOffset = Vector2.Lerp(m_rawAimOffset, m_aimOffset, 0.5f);
        if (!m_sein.Controller.IsGrabbingWall) {
            if (m_lockAimAnimationRemainingTime <= 0f) {
                bool faceLeft = m_faceLeft;
                m_faceLeft = m_aimOffset.x < 0f;
                if (faceLeft != m_faceLeft) {
                    m_lockAimAnimationRemainingTime = 0.17f;
                    m_animationAimAngle = 90f;
                    Sound.Play(TurnAroundAimingSound.GetSound(null), transform.position, null);
                }
            }

            m_sein.FaceLeft = m_faceLeft;
        }

        UpdateTrajectory();
        if (m_lockAimAnimationRemainingTime > 0f) {
            m_lockAimAnimationRemainingTime -= Time.deltaTime;
        }

        if (m_lockAimAnimationRemainingTime <= 0f) {
            Vector3 v2 = m_aimOffset.normalized;
            if (m_aimOffset.y > 0f) {
                float num = m_aimOffset.y / GrenadeGravity;
                float d = m_aimOffset.y * num + 0.5f * GrenadeGravity * num * num;
                v2 = (m_aimOffset.x * num * Vector3.right + d * Vector3.up).normalized;
            }

            v2.x = Mathf.Abs(v2.x);
            float target = MoonMath.Angle.AngleFromVector(v2);
            m_animationAimAngle = Mathf.MoveTowardsAngle(m_animationAimAngle, target, 90f * Time.deltaTime * 2f);
            PlayAimAnimation();
        }

        if (m_grenadeAiming) {
            SpriteAnimatorWithTransitions animator = m_sein.Animation.Animator;
            TextureAnimation currentAnimation = animator.CurrentAnimation;
            if (currentAnimation.AnimationMetaData) {
                PositionGrenadeAiming(currentAnimation.AnimationMetaData, (int)animator.TextureAnimator.Frame);
                return;
            }

            if (IsGrabbingWall) {
                PositionGrenadeAiming(WallAimingMetaData, PickAnimationIndex(WallAimingAnimations.Length));
                return;
            }

            PositionGrenadeAiming(AimingMetaData, PickAnimationIndex(AimingAnimations.Length));
        }
    }

    private void PositionGrenadeAiming(AnimationMetaData metaData, int frame) {
        AnimationMetaData.AnimationData animationData = metaData.FindData("#grenade");
        if (animationData != null) {
            Vector3 positionAtFrame = animationData.GetPositionAtFrame(frame);
            m_grenadeAiming.transform.position = m_sein.PlatformBehaviour.Visuals.Sprite.transform.TransformPoint(positionAtFrame);
        }
    }

    public void EndAiming() {
        m_lockAimAnimationRemainingTime = 0f;
        m_isAiming = false;
        if (m_sein.Abilities.GrabWall) {
            m_sein.Abilities.GrabWall.LockVerticalMovement = false;
        }

        if (m_grenadeAiming) {
            m_grenadeAiming.GetComponent<TransparencyAnimator>().AnimatorDriver.ContinueBackwards();
        }

        Trajectory.HideTrajectory();
        if (AimingSound) {
            AimingSound.Stop();
        }
    }

    private void ClampAim() {
        m_rawAimOffset.x = Mathf.Clamp(m_rawAimOffset.x, -MaxAimDistance, MaxAimDistance);
        if (IsGrabbingWall) {
            m_rawAimOffset.x = !m_faceLeft ? Mathf.Min(0f, m_rawAimOffset.x) : Mathf.Max(0f, m_rawAimOffset.x);
        }

        float num = m_rawAimOffset.y <= 0f ? MinAimDistanceDown : MinAimDistanceUp;
        float num2 = MinAimDistanceHorizontal / num;
        m_rawAimOffset.y = m_rawAimOffset.y * num2;
        if (m_rawAimOffset.magnitude < MinAimDistanceHorizontal) {
            m_rawAimOffset = m_rawAimOffset.normalized * MinAimDistanceHorizontal;
        }

        m_rawAimOffset.y = m_rawAimOffset.y / num2;
        m_rawAimOffset.y = Mathf.Clamp(m_rawAimOffset.y, !IsGrabbingWall ? MinAimVertical : MinAimVerticalWall, MaxAimVertical);
    }

    public void UpdateTrajectory() {
        Trajectory.StartPosition = GrenadeSpawnPosition;
        Trajectory.InitialVelocity = m_aimOffset;
    }

    public float TimeToTarget(Vector2 velocity, IAttackable target) {
        return Mathf.Abs(target.Position.x - GrenadeSpawnPosition.x) / Mathf.Abs(velocity.x);
    }

    public bool WillRayHitEnemy(Vector2 initialVelocity, IAttackable target) {
        Vector3 vector = GrenadeSpawnPosition;
        Vector3 a = initialVelocity;
        Vector3 vector2 = vector;
        float grenadeGravity = GrenadeGravity;
        float num = 0f;
        float num2 = TimeToTarget(initialVelocity, target);
        while (num < num2) {
            for (int i = 0; i < 2; i++) {
                vector += a * 0.01666667f;
                a += Vector3.down * grenadeGravity * 0.01666667f;
                num += 0.01666667f;
            }

            Vector3 vector3 = vector - vector2;
            RaycastHit raycastHit;
            if (Physics.SphereCast(vector2, 0.5f, vector3.normalized, out raycastHit, vector3.magnitude)) {
                break;
            }

            vector2 = vector;
        }

        return Vector3.Distance(vector2, target.Position) <= 4f;
    }

    public bool CompareAnimations(TextureAnimationWithTransitions current, TextureAnimationWithTransitions[] array) {
        for (int i = 0; i < array.Length; i++) {
            if (array[i] == current) {
                return true;
            }
        }

        return false;
    }

    public Func<bool> AnimationRule(FastThrowAnimationRule.AnimationRule rule) {
        if (rule == FastThrowAnimationRule.AnimationRule.InAir) {
            return KeepPlayingAirThrowAnimation;
        }

        if (rule != FastThrowAnimationRule.AnimationRule.OnGround) {
            return null;
        }

        return KeepPlayingGroundThrowAnimation;
    }

    public void PlayFastThrowAnimation() {
        TextureAnimation currentAnimation = m_sein.PlatformBehaviour.Visuals.Animation.Animator.CurrentAnimation;
        TextureAnimationWithTransitions currentTextureAnimationTransitions = m_sein.PlatformBehaviour.Visuals.Animation.Animator.CurrentTextureAnimationTransitions;
        foreach (FastThrowAnimationRule fastThrowAnimationRule in FastThrowAnimations) {
            if (fastThrowAnimationRule.Animations.Contains(currentAnimation)) {
                m_sein.Animation.Play(fastThrowAnimationRule.ThrowAnimation, 10, AnimationRule(fastThrowAnimationRule.PlayRule));
                return;
            }
        }

        foreach (FastThrowAnimationRule fastThrowAnimationRule2 in FastThrowAnimations) {
            if (fastThrowAnimationRule2.AnimationsWithTransitions.Contains(currentTextureAnimationTransitions)) {
                m_sein.Animation.Play(fastThrowAnimationRule2.ThrowAnimation, 10, AnimationRule(fastThrowAnimationRule2.PlayRule));
                break;
            }
        }
    }

    public bool KeepPlayingAirThrowAnimation() {
        return m_sein.PlatformBehaviour.PlatformMovement.IsInAir;
    }

    public bool KeepPlayingGroundThrowAnimation() {
        return m_sein.PlatformBehaviour.PlatformMovement.IsOnGround;
    }

    public void UpdateNormal() {
        if (RandomizerRebinding.ResetGrenadeAim.OnPressed) {
            ResetAimToDefault();
        }

        m_lockPressingInputTime -= Time.deltaTime;
        m_autoTarget = null;
        if (Input.LeftShoulder.OnPressed && m_lockPressingInputTime <= 0f) {
            m_inputPressed = true;
        }

        if (Input.LeftShoulder.Released) {
            m_inputPressed = false;
        }

        RefreshListOfQuickSpiritGrenades();
        if (Input.LeftShoulder.Pressed && m_lockPressingInputTime <= 0f && HasEnoughEnergy && CanAim) {
            m_inputPressed = false;
            SpendEnergy();
            BeginAiming();
            UpdateTrajectory();
            Trajectory.ShowTrajectory();
        }

        if (m_inputPressed) {
            if (!HasEnoughEnergy) {
                m_inputPressed = false;
                UI.SeinUI.ShakeEnergyOrbBar();
                if (NotEnoughEnergySound) {
                    Sound.Play(NotEnoughEnergySound.GetSound(null), transform.position, null);
                }

                m_sein.Animation.Play(PickAnimation(!IsGrabbingWall ? NotEnoughEnergyThrowAnimations : NotEnoughEnergyWallThrowAnimations), 154, KeepPlayingNotEnoughEnergyAnimation);
                if (CanAim) {
                    Vector3 b = !IsGrabbingWall ? new Vector2(-0.5f, 0.1f) : new Vector2(-0.8f, -0.13f);
                    if (m_sein.FaceLeft) {
                        b.x *= -1f;
                    }

                    InstantiateUtility.Instantiate(GrenadeFailEffect, m_sein.Position + b, Quaternion.identity);
                }

                m_lockPressingInputTime = 0.2f;
                return;
            }

            if (!CanAim) {
                m_autoTarget = FindAutoAttackable;
                if (m_autoTarget != null) {
                    m_inputPressed = false;
                    m_lockPressingInputTime = 0.2f;
                    SpawnGrenade(VelocityToAimAtTarget(m_autoTarget)).Bashable = false;
                    SpendEnergy();
                    PlayFastThrowAnimation();
                    PlayThrowSound();
                    ResetAimToDefault();
                } else {
                    m_inputPressed = false;
                    m_lockPressingInputTime = 0.2f;
                    Vector2 quickThrowSpeed = QuickThrowSpeed;
                    if (m_sein.FaceLeft) {
                        quickThrowSpeed.x *= -1f;
                    }

                    SpawnGrenade(quickThrowSpeed).Bashable = false;
                    SpendEnergy();
                    PlayFastThrowAnimation();
                    PlayThrowSound();
                    ResetAimToDefault();
                }

                if (m_sein.Abilities.Glide) {
                    m_sein.Abilities.Glide.LockGliding(0.2f);
                    m_sein.Abilities.Glide.IsGliding = false;
                }
            }
        }
    }

    public bool KeepPlayingAimAnimation() {
        return m_isAiming;
    }

    public bool KeepPlayingThrowAnimation() {
        return !m_sein.PlatformBehaviour.PlatformMovement.MovingHorizontally;
    }

    public bool KeepPlayingNotEnoughEnergyAnimation() {
        return m_sein.PlatformBehaviour.PlatformMovement.LocalSpeed == Vector2.zero;
    }

    public void BeginAiming() {
        m_sein.PlatformBehaviour.PlatformMovement.LocalSpeed = Vector2.zero;
        if (IsGrabbingWall) {
            if (!m_lastAimWasOnWall) {
                ResetAimToDefault();
            }

            m_lastAimWasOnWall = true;
            m_animationAimAngle = IndexToAnimationAngle(8, WallAimingAnimations.Length);
            m_lockAimAnimationRemainingTime = 0.3667f;
        } else {
            if (m_lastAimWasOnWall) {
                ResetAimToDefault();
            }

            m_lastAimWasOnWall = false;
            m_animationAimAngle = IndexToAnimationAngle(8, AimingAnimations.Length);
            m_lockAimAnimationRemainingTime = 0.1f;
        }

        m_isAiming = true;
        m_faceLeft = m_sein.FaceLeft;
        m_rawAimOffset.x = Mathf.Abs(m_rawAimOffset.x) * (!m_sein.FaceLeft ? 1 : -1);
        if (IsGrabbingWall) {
            m_rawAimOffset.x = m_rawAimOffset.x * -1f;
        }

        ClampAim();
        m_aimOffset = m_rawAimOffset;
        m_autoAim = true;
        AutoTarget();
        if (m_sein.Abilities.GrabWall) {
            m_sein.Abilities.GrabWall.LockVerticalMovement = true;
        }

        m_grenadeAiming = (GameObject)InstantiateUtility.Instantiate(GrenadeAiming);
        Sound.Play(StartAimingSound.GetSound(null), transform.position, null);
        if (AimingSound) {
            AimingSound.Play();
        }

        PlayAimAnimation();
    }

    public IAttackable FindAutoAttackable {
        get {
            IAttackable result = null;
            int num = 0;
            float num2 = float.MaxValue;
            foreach (IAttackable attackable in Targets.Attackables) {
                if (attackable as Component && attackable.CanBeGrenaded() && attackable is EntityTargetting && UI.Cameras.Current.IsOnScreen(attackable.Position)) {
                    Vector2 vector = attackable.Position - m_sein.Position;
                    float magnitude = vector.magnitude;
                    int num3 = !m_sein.FaceLeft ? 1 : -1;
                    if (IsGrabbingWall) {
                        num3 *= -1;
                    }

                    int num4 = !(((EntityTargetting)attackable).Entity is Enemy) ? 0 : 1;
                    if (magnitude > AutoAim.MinDistance && magnitude < AutoAim.MaxDistance && num3 == (int)Mathf.Sign(vector.x) && (num < num4 || (num == num4 && magnitude < num2))) {
                        Vector2 initialVelocity = VelocityToAimAtTarget(attackable);
                        if (WillRayHitEnemy(initialVelocity, attackable)) {
                            result = attackable;
                            num2 = magnitude;
                            num = num4;
                        }
                    }
                }
            }

            return result;
        }
    }

    public void AutoTarget() {
        m_autoTarget = FindAutoAttackable;
        if (m_autoTarget as Component != null) {
            SetAimVelocity(VelocityToAimAtTarget(m_autoTarget));
        }
    }

    private void SetAimVelocity(Vector2 aim) {
        m_aimOffset = aim;
        m_rawAimOffset = aim;
    }

    public Vector2 VelocityToAimAtTarget(IAttackable attackable) {
        Vector2 vector = attackable.Position - m_sein.Position;
        float num = !IsInAir ? AutoAim.Speed + Mathf.Abs(vector.x) * AutoAim.SpeedPerXDistance + Mathf.Max(0f, vector.y) * AutoAim.SpeedPerYDistance : AutoAim.InAirSpeed;
        float num2 = vector.magnitude / num;
        return new Vector2(vector.x / num2, vector.y / num2 + GrenadeGravity * num2 * 0.5f);
    }

    public override void OnExit() {
        base.OnExit();
        CancelAiming();
    }

    public void CancelAiming() {
        if (m_isAiming) {
            RestoreEnergy();
            EndAiming();
            Sound.Play(StopAimingSound.GetSound(null), transform.position, null);
        }
    }

    public GameObject Grenade;

    public GameObject GrenadeUpgraded;

    public GameObject GrenadeAiming;

    private GameObject m_grenadeAiming;

    public SeinGrenadeTrajectory Trajectory;

    public AnimationCurve AimSpeed;

    public float MaxAimDistance;

    public float MinAimDistanceUp;

    public float MinAimDistanceDown;

    public float MinAimDistanceHorizontal;

    public float MaxAimVertical = 50f;

    public float MinAimVertical = 2f;

    public float MinAimVerticalWall = -30f;

    public int MaxSpamGrenades = 3;

    public float EnergyCost = 1f;

    public SoundProvider NotEnoughEnergySound;

    public SoundProvider TurnAroundAimingSound;

    public SoundProvider ThrowGrenadeSound;

    public SoundProvider StopAimingSound;

    public SoundProvider StartAimingSound;

    public SoundSource AimingSound;

    public Vector2 QuickThrowSpeed = new Vector2(14f, 16f);

    public GameObject GrenadeFailEffect;

    public float AimAnimationAngleOffset = 5f;

    public float CursorSpeedMultiplier = 1f;

    public float CursorSpeedYOffset = 12f;

    private float m_lockPressingInputTime;

    private Vector2 m_rawAimOffset = new Vector2(14f, 16f);

    private SeinCharacter m_sein;

    private bool m_isAiming;

    private Vector2 m_aimOffset;

    private List<SpiritGrenade> m_spiritGrenades = new List<SpiritGrenade>();

    private float m_animationAimAngle;

    private bool m_lastAimWasOnWall;

    public TextureAnimationWithTransitions[] AimingAnimations;

    public TextureAnimationWithTransitions[] ThrowAnimations;

    public TextureAnimationWithTransitions[] WallAimingAnimations;

    public TextureAnimationWithTransitions[] WallThrowAnimations;

    public TextureAnimationWithTransitions[] NotEnoughEnergyThrowAnimations;

    public TextureAnimationWithTransitions[] NotEnoughEnergyWallThrowAnimations;

    public QuickThrowAnimations QuickThrow;

    public AnimationMetaData WallAimingMetaData;

    public AnimationMetaData AimingMetaData;

    private float m_lockAimAnimationRemainingTime;

    private bool m_faceLeft;

    public float MaxAimWallAnimationAngle = 85f;

    public float MinAimWallAnimationAngle = -80f;

    public float MaxAimGroundAnimationAngle = 90f;

    public float MinAimGroundAnimationAngle = -30f;

    private bool m_inputPressed;

    public List<FastThrowAnimationRule> FastThrowAnimations;

    private bool m_autoAim;

    private IAttackable m_autoTarget;

    public AutoAimSettings AutoAim;

    [Serializable]
    public class QuickThrowAnimations {
        public TextureAnimationWithTransitions FallIdleThrowAnimation;

        public TextureAnimationWithTransitions FallThrowAnimation;

        public TextureAnimationWithTransitions RunThrowAnimation;

        public TextureAnimationWithTransitions JogThrowAnimation;

        public TextureAnimationWithTransitions WalkThrowAnimation;

        public TextureAnimationWithTransitions JumpThrowAnimation;

        public TextureAnimationWithTransitions JumpIdleThrowAnimation;

        public TextureAnimationWithTransitions IdleThrowAnimation;

        public TextureAnimationWithTransitions WallThrowAnimation;
    }

    [Serializable]
    public class FastThrowAnimationRule {
        public TextureAnimationWithTransitions ThrowAnimation;

        public List<TextureAnimationWithTransitions> AnimationsWithTransitions;

        public List<TextureAnimation> Animations;

        public AnimationRule PlayRule;

        public enum AnimationRule {
            InAir,
            OnGround
        }
    }

    [Serializable]
    public class AutoAimSettings {
        public float MaxDistance = 30f;

        public float MinDistance = 2f;

        public float Speed = 5f;

        public float SpeedPerXDistance = 0.7f;

        public float SpeedPerYDistance = 2f;

        public float InAirSpeed = 30f;
    }
}

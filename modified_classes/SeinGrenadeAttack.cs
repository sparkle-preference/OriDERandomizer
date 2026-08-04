using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinGrenadeAttack : CharacterState, ISeinReceiver {
    private bool IsGrabbingWall => sein.Controller.IsGrabbingWall;

    private bool IsInAir => !isAiming;

    private void ResetAimToDefault() {
        SetAimVelocity(new Vector2(14f, 16f));
    }

    private int PickAnimationIndex(int length) {
        return Mathf.Clamp(Mathf.FloorToInt((!IsGrabbingWall ? Mathf.InverseLerp(MinAimGroundAnimationAngle, MaxAimGroundAnimationAngle, animationAimAngle) : Mathf.InverseLerp(MinAimWallAnimationAngle, MaxAimWallAnimationAngle, animationAimAngle)) * length), 0, length - 1);
    }

    private float IndexToAnimationAngle(int index, int length) {
        var t = index / (float)length;
        if (IsGrabbingWall) {
            return Mathf.Lerp(MinAimWallAnimationAngle, MaxAimWallAnimationAngle, t);
        }

        return Mathf.Lerp(MinAimGroundAnimationAngle, MaxAimGroundAnimationAngle, t);
    }

    private TextureAnimationWithTransitions PickAnimation(TextureAnimationWithTransitions[] animations) {
        var num = PickAnimationIndex(animations.Length);
        return animations[num];
    }

    private float EnergyCostFinal => 0f;

    private bool HasGrenadeEfficiencySkill() {
        return sein.PlayerAbilities.GrenadeEfficiency.HasAbility;
    }

    private bool HasEnoughEnergy => sein.Energy.CanAfford(EnergyCostFinal);

    private void SpendEnergy() {
        sein.Energy.Spend(EnergyCostFinal);
    }

    private void RestoreEnergy() {
        sein.Energy.Gain(EnergyCostFinal);
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

    public CharacterLeftRightMovement CharacterLeftRightMovement => sein.PlatformBehaviour.LeftRightMovement;

    public CharacterGravity CharacterGravity => sein.PlatformBehaviour.Gravity;

    private void ModifyHorizontalPlatformMovementSettings(HorizontalPlatformMovementSettings settings) {
        if (isAiming) {
            settings.Ground.Acceleration = 0f;
            settings.Ground.MaxSpeed = 0f;
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        this.sein = sein;
        sein.Abilities.Grenade = this;
    }

    public override void UpdateCharacterState() {
        if (sein.IsSuspended) {
            return;
        }

        if (sein.Controller.InputLocked) {
            return;
        }

        if (SeinAbilityRestrictZone.IsInside()) {
            return;
        }

        base.UpdateCharacterState();
        if (isAiming) {
            UpdateAiming();
            return;
        }

        UpdateNormal();
    }

    private bool HasGrenadeUpgrade() {
        return sein.PlayerAbilities.GrenadeUpgrade.HasAbility;
    }

    private Vector3 GrenadeSpawnPosition => sein.Position;

    private SpiritGrenade SpawnGrenade(Vector2 velocity) {
        RefreshListOfQuickSpiritGrenades();
        if (spiritGrenades.Count >= MaxSpamGrenades) {
            spiritGrenades[0].Explode();
            spiritGrenades.RemoveAt(0);
        }

        var component = ((GameObject)InstantiateUtility.Instantiate(!HasGrenadeUpgrade() ? Grenade : GrenadeUpgraded, GrenadeSpawnPosition, Quaternion.identity)).GetComponent<SpiritGrenade>();
        component.SetTrajectory(velocity);
        spiritGrenades.Add(component);
        if (autoTarget as Component != null) {
            component.Duration = TimeToTarget(velocity, autoTarget) + 0.2f;
            autoTarget = null;
        }

        return component;
    }

    private void RefreshListOfQuickSpiritGrenades() {
        spiritGrenades.RemoveAll(a => a == null);
    }

    public bool IsAiming => isAiming;

    public bool CanAim => !sein.PlatformBehaviour.PlatformMovement.MovingHorizontally && (sein.IsOnGround || IsGrabbingWall);

    public void PlayAimAnimation() {
        sein.Animation.PlayLoop(PickAnimation(!IsGrabbingWall ? AimingAnimations : WallAimingAnimations), 154, KeepPlayingAimAnimation, true);
    }

    public void PlayThrowAnimation() {
        if (Mathf.Approximately(Mathf.Abs(rawAimOffset.x), QuickThrowSpeed.x) && Mathf.Approximately(rawAimOffset.y, QuickThrowSpeed.y)) {
            sein.Animation.Play(!IsGrabbingWall ? QuickThrow.IdleThrowAnimation : QuickThrow.WallThrowAnimation, 154, KeepPlayingThrowAnimation);
            return;
        }

        sein.Animation.Play(PickAnimation(!IsGrabbingWall ? ThrowAnimations : WallThrowAnimations), 154, KeepPlayingThrowAnimation);
    }

    public void PlayThrowSound() {
        Sound.Play(ThrowGrenadeSound.GetSound(null), transform.position, null);
    }

    public float GrenadeGravity => Trajectory.Gravity;

    public void UpdateAiming() {
        if (Input.LeftShoulder.Released) {
            lockPressingInputTime = 0.64f;
            SpawnGrenade(rawAimOffset);
            PlayThrowAnimation();
            EndAiming();
            PlayThrowSound();
            return;
        }

        if (Input.Jump.OnPressed || Input.Cancel.OnPressed || !CanAim) {
            CancelAiming();
            return;
        }

        sein.Speed = Vector2.zero;
        if (RandomizerRebinding.ResetGrenadeAim.OnPressed) {
            ResetAimToDefault();
        }

        var axis = Input.Axis;
        if (!RandomizerSettings.Controls.FastGrenadeAim) {
            var b = AimSpeed.Evaluate(axis.magnitude) * axis.normalized * RandomizerSettings.Controls.GrenadeAimSpeed;
            if (b.magnitude > 0f) {
                autoAim = false;
            }

            rawAimOffset += b;
        } else {
            var greater = Math.Max(Math.Abs(axis.x), Math.Abs(axis.y));
            if (greater > 0f) {
                rawAimOffset = axis * axis.sqrMagnitude * Math.Min(Math.Abs(UI.Cameras.Current.OffsetController.Offset.z), MaxAimDistance) / greater + Vector2.up * CursorSpeedYOffset;
            } else {
                rawAimOffset = Vector2.up * CursorSpeedYOffset;
            }

            autoAim = false;
        }

        if (autoAim) {
            AutoTarget();
        } else {
            autoTarget = null;
        }

        ClampAim();
        if (Input.CursorMoved) {
            Vector2 v = UI.Cameras.Current.Camera.WorldToScreenPoint(transform.position);
            Vector2 b2 = UI.Cameras.System.GUICamera.ScreenToWorldPoint(v);
            rawAimOffset = (Input.CursorPositionUI - b2) * CursorSpeedMultiplier + Vector2.up * CursorSpeedYOffset;
            autoAim = false;
            ClampAim();
        }

        aimOffset = Vector2.Lerp(rawAimOffset, aimOffset, 0.5f);
        if (!sein.Controller.IsGrabbingWall) {
            if (lockAimAnimationRemainingTime <= 0f) {
                var faceLeft = this.faceLeft;
                this.faceLeft = aimOffset.x < 0f;
                if (faceLeft != this.faceLeft) {
                    lockAimAnimationRemainingTime = 0.17f;
                    animationAimAngle = 90f;
                    Sound.Play(TurnAroundAimingSound.GetSound(null), transform.position, null);
                }
            }

            sein.FaceLeft = faceLeft;
        }

        UpdateTrajectory();
        if (lockAimAnimationRemainingTime > 0f) {
            lockAimAnimationRemainingTime -= Time.deltaTime;
        }

        if (lockAimAnimationRemainingTime <= 0f) {
            Vector3 v2 = aimOffset.normalized;
            if (aimOffset.y > 0f) {
                var num = aimOffset.y / GrenadeGravity;
                var d = aimOffset.y * num + 0.5f * GrenadeGravity * num * num;
                v2 = (aimOffset.x * num * Vector3.right + d * Vector3.up).normalized;
            }

            v2.x = Mathf.Abs(v2.x);
            var target = MoonMath.Angle.AngleFromVector(v2);
            animationAimAngle = Mathf.MoveTowardsAngle(animationAimAngle, target, 90f * Time.deltaTime * 2f);
            PlayAimAnimation();
        }

        if (grenadeAiming) {
            var animator = sein.Animation.Animator;
            var currentAnimation = animator.CurrentAnimation;
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
        var animationData = metaData.FindData("#grenade");
        if (animationData != null) {
            var positionAtFrame = animationData.GetPositionAtFrame(frame);
            grenadeAiming.transform.position = sein.PlatformBehaviour.Visuals.Sprite.transform.TransformPoint(positionAtFrame);
        }
    }

    public void EndAiming() {
        lockAimAnimationRemainingTime = 0f;
        isAiming = false;
        if (sein.Abilities.GrabWall) {
            sein.Abilities.GrabWall.LockVerticalMovement = false;
        }

        if (grenadeAiming) {
            grenadeAiming.GetComponent<TransparencyAnimator>().AnimatorDriver.ContinueBackwards();
        }

        Trajectory.HideTrajectory();
        if (AimingSound) {
            AimingSound.Stop();
        }
    }

    private void ClampAim() {
        rawAimOffset.x = Mathf.Clamp(rawAimOffset.x, -MaxAimDistance, MaxAimDistance);
        if (IsGrabbingWall) {
            rawAimOffset.x = !faceLeft ? Mathf.Min(0f, rawAimOffset.x) : Mathf.Max(0f, rawAimOffset.x);
        }

        var num = rawAimOffset.y <= 0f ? MinAimDistanceDown : MinAimDistanceUp;
        var num2 = MinAimDistanceHorizontal / num;
        rawAimOffset.y *= num2;
        if (rawAimOffset.magnitude < MinAimDistanceHorizontal) {
            rawAimOffset = rawAimOffset.normalized * MinAimDistanceHorizontal;
        }

        rawAimOffset.y /= num2;
        rawAimOffset.y = Mathf.Clamp(rawAimOffset.y, !IsGrabbingWall ? MinAimVertical : MinAimVerticalWall, MaxAimVertical);
    }

    public void UpdateTrajectory() {
        Trajectory.StartPosition = GrenadeSpawnPosition;
        Trajectory.InitialVelocity = aimOffset;
    }

    public float TimeToTarget(Vector2 velocity, IAttackable target) {
        return Mathf.Abs(target.Position.x - GrenadeSpawnPosition.x) / Mathf.Abs(velocity.x);
    }

    public bool WillRayHitEnemy(Vector2 initialVelocity, IAttackable target) {
        var vector = GrenadeSpawnPosition;
        Vector3 a = initialVelocity;
        var vector2 = vector;
        var grenadeGravity = GrenadeGravity;
        var num = 0f;
        var num2 = TimeToTarget(initialVelocity, target);
        while (num < num2) {
            for (var i = 0; i < 2; i++) {
                vector += a * 0.01666667f;
                a += Vector3.down * grenadeGravity * 0.01666667f;
                num += 0.01666667f;
            }

            var vector3 = vector - vector2;
            if (Physics.SphereCast(vector2, 0.5f, vector3.normalized, out _, vector3.magnitude)) {
                break;
            }

            vector2 = vector;
        }

        return Vector3.Distance(vector2, target.Position) <= 4f;
    }

    public bool CompareAnimations(TextureAnimationWithTransitions current, TextureAnimationWithTransitions[] array) {
        for (var i = 0; i < array.Length; i++) {
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
        var currentAnimation = sein.PlatformBehaviour.Visuals.Animation.Animator.CurrentAnimation;
        var currentTextureAnimationTransitions = sein.PlatformBehaviour.Visuals.Animation.Animator.CurrentTextureAnimationTransitions;
        foreach (var fastThrowAnimationRule in FastThrowAnimations) {
            if (fastThrowAnimationRule.Animations.Contains(currentAnimation)) {
                sein.Animation.Play(fastThrowAnimationRule.ThrowAnimation, 10, AnimationRule(fastThrowAnimationRule.PlayRule));
                return;
            }
        }

        foreach (var fastThrowAnimationRule2 in FastThrowAnimations) {
            if (fastThrowAnimationRule2.AnimationsWithTransitions.Contains(currentTextureAnimationTransitions)) {
                sein.Animation.Play(fastThrowAnimationRule2.ThrowAnimation, 10, AnimationRule(fastThrowAnimationRule2.PlayRule));
                break;
            }
        }
    }

    public bool KeepPlayingAirThrowAnimation() {
        return sein.PlatformBehaviour.PlatformMovement.IsInAir;
    }

    public bool KeepPlayingGroundThrowAnimation() {
        return sein.PlatformBehaviour.PlatformMovement.IsOnGround;
    }

    public void UpdateNormal() {
        if (RandomizerRebinding.ResetGrenadeAim.OnPressed) {
            ResetAimToDefault();
        }

        lockPressingInputTime -= Time.deltaTime;
        autoTarget = null;
        if (Input.LeftShoulder.OnPressed && lockPressingInputTime <= 0f) {
            inputPressed = true;
        }

        if (Input.LeftShoulder.Released) {
            inputPressed = false;
        }

        RefreshListOfQuickSpiritGrenades();
        if (Input.LeftShoulder.Pressed && lockPressingInputTime <= 0f && HasEnoughEnergy && CanAim) {
            inputPressed = false;
            SpendEnergy();
            BeginAiming();
            UpdateTrajectory();
            Trajectory.ShowTrajectory();
        }

        if (inputPressed) {
            if (!HasEnoughEnergy) {
                inputPressed = false;
                UI.SeinUI.ShakeEnergyOrbBar();
                if (NotEnoughEnergySound) {
                    Sound.Play(NotEnoughEnergySound.GetSound(null), transform.position, null);
                }

                sein.Animation.Play(PickAnimation(!IsGrabbingWall ? NotEnoughEnergyThrowAnimations : NotEnoughEnergyWallThrowAnimations), 154, KeepPlayingNotEnoughEnergyAnimation);
                if (CanAim) {
                    Vector3 b = !IsGrabbingWall ? new Vector2(-0.5f, 0.1f) : new Vector2(-0.8f, -0.13f);
                    if (sein.FaceLeft) {
                        b.x *= -1f;
                    }

                    InstantiateUtility.Instantiate(GrenadeFailEffect, sein.Position + b, Quaternion.identity);
                }

                lockPressingInputTime = 0.2f;
                return;
            }

            if (!CanAim) {
                autoTarget = FindAutoAttackable;
                if (autoTarget != null) {
                    inputPressed = false;
                    lockPressingInputTime = 0.2f;
                    SpawnGrenade(VelocityToAimAtTarget(autoTarget)).Bashable = false;
                    SpendEnergy();
                    PlayFastThrowAnimation();
                    PlayThrowSound();
                    ResetAimToDefault();
                } else {
                    inputPressed = false;
                    lockPressingInputTime = 0.2f;
                    var quickThrowSpeed = QuickThrowSpeed;
                    if (sein.FaceLeft) {
                        quickThrowSpeed.x *= -1f;
                    }

                    SpawnGrenade(quickThrowSpeed).Bashable = false;
                    SpendEnergy();
                    PlayFastThrowAnimation();
                    PlayThrowSound();
                    ResetAimToDefault();
                }

                if (sein.Abilities.Glide) {
                    sein.Abilities.Glide.LockGliding(0.2f);
                    sein.Abilities.Glide.IsGliding = false;
                }
            }
        }
    }

    public bool KeepPlayingAimAnimation() {
        return isAiming;
    }

    public bool KeepPlayingThrowAnimation() {
        return !sein.PlatformBehaviour.PlatformMovement.MovingHorizontally;
    }

    public bool KeepPlayingNotEnoughEnergyAnimation() {
        return sein.PlatformBehaviour.PlatformMovement.LocalSpeed == Vector2.zero;
    }

    public void BeginAiming() {
        sein.PlatformBehaviour.PlatformMovement.LocalSpeed = Vector2.zero;
        if (IsGrabbingWall) {
            if (!lastAimWasOnWall) {
                ResetAimToDefault();
            }

            lastAimWasOnWall = true;
            animationAimAngle = IndexToAnimationAngle(8, WallAimingAnimations.Length);
            lockAimAnimationRemainingTime = 0.3667f;
        } else {
            if (lastAimWasOnWall) {
                ResetAimToDefault();
            }

            lastAimWasOnWall = false;
            animationAimAngle = IndexToAnimationAngle(8, AimingAnimations.Length);
            lockAimAnimationRemainingTime = 0.1f;
        }

        isAiming = true;
        faceLeft = sein.FaceLeft;
        rawAimOffset.x = Mathf.Abs(rawAimOffset.x) * (!sein.FaceLeft ? 1 : -1);
        if (IsGrabbingWall) {
            rawAimOffset.x *= -1f;
        }

        ClampAim();
        aimOffset = rawAimOffset;
        autoAim = true;
        AutoTarget();
        if (sein.Abilities.GrabWall) {
            sein.Abilities.GrabWall.LockVerticalMovement = true;
        }

        grenadeAiming = (GameObject)InstantiateUtility.Instantiate(GrenadeAiming);
        Sound.Play(StartAimingSound.GetSound(null), transform.position, null);
        if (AimingSound) {
            AimingSound.Play();
        }

        PlayAimAnimation();
    }

    public IAttackable FindAutoAttackable {
        get {
            IAttackable result = null;
            var num = 0;
            var num2 = float.MaxValue;
            foreach (var attackable in Targets.Attackables) {
                if (attackable as Component && attackable.CanBeGrenaded() && attackable is EntityTargetting && UI.Cameras.Current.IsOnScreen(attackable.Position)) {
                    Vector2 vector = attackable.Position - sein.Position;
                    var magnitude = vector.magnitude;
                    var num3 = !sein.FaceLeft ? 1 : -1;
                    if (IsGrabbingWall) {
                        num3 *= -1;
                    }

                    var num4 = !(((EntityTargetting)attackable).Entity is Enemy) ? 0 : 1;
                    if (magnitude > AutoAim.MinDistance && magnitude < AutoAim.MaxDistance && num3 == (int)Mathf.Sign(vector.x) && (num < num4 || (num == num4 && magnitude < num2))) {
                        var initialVelocity = VelocityToAimAtTarget(attackable);
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
        autoTarget = FindAutoAttackable;
        if (autoTarget as Component != null) {
            SetAimVelocity(VelocityToAimAtTarget(autoTarget));
        }
    }

    private void SetAimVelocity(Vector2 aim) {
        aimOffset = aim;
        rawAimOffset = aim;
    }

    public Vector2 VelocityToAimAtTarget(IAttackable attackable) {
        Vector2 vector = attackable.Position - sein.Position;
        var num = !IsInAir ? AutoAim.Speed + Mathf.Abs(vector.x) * AutoAim.SpeedPerXDistance + Mathf.Max(0f, vector.y) * AutoAim.SpeedPerYDistance : AutoAim.InAirSpeed;
        var num2 = vector.magnitude / num;
        return new Vector2(vector.x / num2, vector.y / num2 + GrenadeGravity * num2 * 0.5f);
    }

    public override void OnExit() {
        base.OnExit();
        CancelAiming();
    }

    public void CancelAiming() {
        if (isAiming) {
            RestoreEnergy();
            EndAiming();
            Sound.Play(StopAimingSound.GetSound(null), transform.position, null);
        }
    }

    public GameObject Grenade;

    public GameObject GrenadeUpgraded;

    public GameObject GrenadeAiming;

    private GameObject grenadeAiming;

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

    private float lockPressingInputTime;

    private Vector2 rawAimOffset = new Vector2(14f, 16f);

    private SeinCharacter sein;

    private bool isAiming;

    private Vector2 aimOffset;

    private List<SpiritGrenade> spiritGrenades = new List<SpiritGrenade>();

    private float animationAimAngle;

    private bool lastAimWasOnWall;

    public TextureAnimationWithTransitions[] AimingAnimations;

    public TextureAnimationWithTransitions[] ThrowAnimations;

    public TextureAnimationWithTransitions[] WallAimingAnimations;

    public TextureAnimationWithTransitions[] WallThrowAnimations;

    public TextureAnimationWithTransitions[] NotEnoughEnergyThrowAnimations;

    public TextureAnimationWithTransitions[] NotEnoughEnergyWallThrowAnimations;

    public QuickThrowAnimations QuickThrow;

    public AnimationMetaData WallAimingMetaData;

    public AnimationMetaData AimingMetaData;

    private float lockAimAnimationRemainingTime;

    private bool faceLeft;

    public float MaxAimWallAnimationAngle = 85f;

    public float MinAimWallAnimationAngle = -80f;

    public float MaxAimGroundAnimationAngle = 90f;

    public float MinAimGroundAnimationAngle = -30f;

    private bool inputPressed;

    public List<FastThrowAnimationRule> FastThrowAnimations;

    private bool autoAim;

    private IAttackable autoTarget;

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
            OnGround,
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

using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinDashAttack : CharacterState, ISeinReceiver {
    static SeinDashAttack() {
        OnDashEvent = delegate { };
        OnWallDashEvent = delegate { };
    }

    public static event Action OnDashEvent;

    public static event Action OnWallDashEvent;

    public bool HasEnoughEnergy => sein.Energy.CanAfford(AdjustedEnergyCost);

    public override void Serialize(Archive ar) {
        if (ar.Reading) {
            ReturnToNormal();
        }
    }

    public override void OnExit() {
        ReturnToNormal();
        base.OnExit();
    }

    public void OnDisable() {
        Exit();
    }

    public void ReturnToNormal() {
        if (CurrentState != State.Normal) {
            if (CurrentState == State.Dashing) {
                sein.PlatformBehaviour.PlatformMovement.LocalSpeedX = (!faceLeft ? 1 : -1) * DashSpeedOverTime.Evaluate(DashSpeedOverTime.length);
            }

            if (CurrentState == State.ChargeDashing) {
                sein.PlatformBehaviour.PlatformMovement.LocalSpeedX = (!faceLeft ? 1 : -1) * ChargeDashSpeedOverTime.Evaluate(ChargeDashSpeedOverTime.length);
            }

            UI.Cameras.Current.ChaseTarget.CameraSpeedMultiplier.x = 1f;
            if (CurrentState == State.ChargeDashing) {
                RestoreEnergy();
            }

            ChangeState(State.Normal);
        }
    }

    public void SpendEnergy() {
        sein.Energy.Spend(AdjustedEnergyCost);
    }

    public void RestoreEnergy() {
        sein.Energy.Gain(AdjustedEnergyCost);
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        this.sein = sein;
        sein.Abilities.Dash = this;
    }

    public override void UpdateCharacterState() {
        UpdateState();
    }

    public bool IsDashingOrChangeDashing {
        get {
            if (CurrentState == State.Dashing) {
                return stateCurrentTime < DashTime;
            }

            return CurrentState == State.ChargeDashing && stateCurrentTime < ChargeDashTime;
        }
    }

    public void ChangeState(State state) {
        CurrentState = state;
        stateCurrentTime = 0f;
        attackablesIgnore.Clear();
    }

    public IChargeDashAttackable FindClosestAttackable {
        get {
            IChargeDashAttackable result = null;
            var num = float.MaxValue;
            foreach (var attackable in Targets.Attackables) {
                if (attackable as Component && attackable.CanBeChargeDashed() && attackable is IChargeDashAttackable) {
                    var chargeDashAttackable = (IChargeDashAttackable)attackable;
                    if (UI.Cameras.Current.IsOnScreen(attackable.Position)) {
                        var magnitude = (attackable.Position - sein.Position).magnitude;
                        if (magnitude < num && magnitude < ChargeDashTargetMaxDistance) {
                            result = chargeDashAttackable;
                            num = magnitude;
                        }
                    }
                }
            }

            return result;
        }
    }

    public void AttackNearbyEnemies() {
        var i = 0;
        while (i < Targets.Attackables.Count) {
            var attackable = Targets.Attackables[i];
            if (!InstantiateUtility.IsDestroyed(attackable as Component) && !attackablesIgnore.Contains(attackable) && attackable.CanBeChargeFlamed() && (attackable.Position - sein.PlatformBehaviour.PlatformMovement.HeadPosition).magnitude <= 3f) {
                attackablesIgnore.Add(attackable);
                var v = !chargeDashAtTarget ? (!faceLeft ? Vector3.right : Vector3.left) * 3f : chargeDashDirection * 3f;
                if (RandomizerBonus.EnhancedDash) {
                    v = enhancedDashDirection * 3f;
                }

                new Damage(Damage, v, sein.Position, DamageType.ChargeFlame, gameObject).DealToComponents(((Component)attackable).gameObject);
                hasHitAttackable = true;
                if (ExplosionEffect && Time.time - timeOfLastExplosionEffect > 0.1f) {
                    timeOfLastExplosionEffect = Time.time;
                    InstantiateUtility.Instantiate(ExplosionEffect, Vector3.Lerp(transform.position, attackable.Position, 0.5f), Quaternion.identity);
                }

                break;
            }

            i++;
        }
    }

    private void PerformDash(TextureAnimationWithTransitions dashAnimation, SoundProvider dashSound) {
        sein.Mortality.DamageReciever.ResetInviciblity();
        hasDashed = true;
        if (RandomizerBonus.DoubleAirDash() && !RandomizerBonus.DoubleAirDashUsed) {
            hasDashed = false;
            RandomizerBonus.DoubleAirDashUsed = true;
        }

        isOnGround = sein.IsOnGround;
        lastDashTime = Time.time;
        lastPressTime = 0f;
        SpriteRotation = sein.PlatformBehaviour.PlatformMovement.GroundAngle;
        allowNoDecelerationForThisDash = true;
        if (chargeDashAtTarget) {
            faceLeft = chargeDashDirection.x < 0f;
        } else if (sein.PlatformBehaviour.PlatformMovement.HasWallLeft) {
            faceLeft = false;
        } else if (sein.PlatformBehaviour.PlatformMovement.HasWallRight) {
            faceLeft = true;
        } else if (sein.Input.NormalizedHorizontal != 0) {
            faceLeft = sein.Input.NormalizedHorizontal < 0;
        } else if (!Mathf.Approximately(sein.Speed.x, 0f)) {
            faceLeft = sein.Speed.x < 0f;
        } else {
            faceLeft = sein.FaceLeft;
            allowNoDecelerationForThisDash = false;
        }

        sein.FaceLeft = faceLeft;
        stopAnimation = false;
        if (!chargeDashAtTarget && RandomizerBonus.EnhancedDash) {
            enhancedDashDirection = faceLeft ? Vector3.left : Vector3.right;

            if (Input.Axis.magnitude > 0f) {
                if (!sein.IsOnGround) {
                    enhancedDashDirection = Input.Axis.normalized;
                } else if (Input.Axis.y > 0f) {
                    var dot = Vector3.Dot(Input.Axis.normalized, Vector3.left);
                    if (dot < 0.94f && dot > -0.94f) {
                        enhancedDashDirection = Input.Axis.normalized;
                    }
                }
            }

            SpriteRotation = Mathf.Atan2(enhancedDashDirection.y, enhancedDashDirection.x) * 57.29578f;
            if (faceLeft) {
                SpriteRotation = Mathf.Repeat(SpriteRotation, 360f) - 180f;
            }
        }

        if (dashSound) {
            Sound.Play(dashSound.GetSound(null), sein.Position, null);
        }

        sein.Animation.Play(dashAnimation, 154, KeepDashAnimationPlaying);
        if (RainbowDashActivated) {
            ((GameObject)InstantiateUtility.Instantiate(DashFollowRainbowEffect, sein.Position, Quaternion.identity)).transform.parent = sein.Transform;
        }

        sein.PlatformBehaviour.PlatformMovement.LocalSpeedY = -DashDownwardSpeed;
    }

    public void PerformDash() {
        chargeDashAtTarget = false;
        var dashSound = !RainbowDashActivated ? DashSound : RainbowDashSound;
        var isGliding = sein.Controller.IsGliding;
        PerformDash(!isGliding ? DashAnimation : GlideDashAnimation, dashSound);
        ChangeState(State.Dashing);
        UpdateDashing();
        OnDashEvent();
    }

    public void PerformWallDash() {
        chargeDashAtTarget = false;
        var dashSound = !RainbowDashActivated ? DashSound : RainbowDashSound;
        PerformDash(DashAnimation, dashSound);
        ChangeState(State.Dashing);
        UpdateDashing();
        OnWallDashEvent();
    }

    public void PerformDashIntoWall() {
        lastPressTime = 0f;
        lastDashTime = Time.time;
        sein.Animation.Play(DashIntoWallAnimation, 154, KeepDashIntoWallAnimationPlaying);
        Sound.Play(DashIntoWallSound.GetSound(null), sein.Position, null);
    }

    public bool KeepDashIntoWallAnimationPlaying() {
        return AgainstWall() && sein.IsOnGround;
    }

    public void PerformChargeDash() {
        hasHitAttackable = false;
        chargeJumpWasReleased = false;
        chargeDashAttackTarget = FindClosestAttackable as IAttackable;
        if (chargeDashAttackTarget != null) {
            chargeDashAtTarget = true;
            chargeDashDirection = (chargeDashAttackTarget.Position - sein.Position).normalized;
        } else {
            chargeDashAtTarget = false;
        }

        var dashSound = !RainbowDashActivated ? ChargeDashSound : RainbowDashSound;
        PerformDash(ChargeDashAnimation, dashSound);
        if (chargeDashAtTarget) {
            SpriteRotation = Mathf.Atan2(chargeDashDirection.y, chargeDashDirection.x) * 57.29578f - (!faceLeft ? 0 : 180);
        }

        ChangeState(State.ChargeDashing);
        CompleteChargeEffect();
        UpdateChargeDashing();
    }

    private bool HasChargeDashSkill() {
        return sein.PlayerAbilities.ChargeDash.HasAbility;
    }

    private bool HasAirDashSkill() {
        return sein.PlayerAbilities.AirDash.HasAbility;
    }

    private bool CanChargeDash() {
        return HasChargeDashSkill() && Input.ChargeJump.Pressed && chargeJumpWasReleased && !Characters.Sein.Abilities.Swimming.IsSwimming;
    }

    public void CompleteChargeEffect() {
        if (sein.Abilities.ChargeJumpCharging) {
            sein.Abilities.ChargeJumpCharging.EndCharge();
        }
    }

    private void UpdateTargetHighlight(IChargeDashAttackable target) {
        if (lastTarget == target) {
            return;
        }

        if (!InstantiateUtility.IsDestroyed(lastTarget as Component)) {
            lastTarget.OnChargeDashDehighlight();
        }

        lastTarget = target;
        if (!InstantiateUtility.IsDestroyed(lastTarget as Component)) {
            lastTarget.OnChargeDashHighlight();
        }
    }

    public bool KeepDashAnimationPlaying() {
        return !stopAnimation && !sein.Abilities.WallSlide.IsOnWall && Active;
    }

    public bool KeepChargeDashAnimationPlaying() {
        return KeepDashAnimationPlaying();
    }

    public bool AgainstWall() {
        var platformMovement = sein.PlatformBehaviour.PlatformMovement;
        return (platformMovement.HasWallLeft && sein.FaceLeft) || (platformMovement.HasWallRight && !sein.FaceLeft);
    }

    public bool CanPerformNormalDash() {
        return (HasAirDashSkill() || sein.IsOnGround || (RandomizerBonus.GravitySuit() && Characters.Sein.Abilities.Swimming.IsSwimming)) && !AgainstWall() && DashHasCooledDown && !hasDashed;
    }

    private bool DashHasCooledDown => Time.time - lastDashTime > 0.4f;

    public bool CanPerformDashIntoWall() {
        return sein.IsOnGround && AgainstWall() && DashHasCooledDown;
    }

    public bool CanWallDash() {
        var platformMovement = sein.PlatformBehaviour.PlatformMovement;
        return ((platformMovement.HasWallLeft && sein.Input.Horizontal >= 0f) || (platformMovement.HasWallRight && sein.Input.Horizontal <= 0f)) && !sein.IsOnGround && sein.PlayerAbilities.AirDash.HasAbility;
    }

    public void UpdateNormal() {
        var num = Time.time - lastPressTime;
        if (sein.IsOnGround || (RandomizerBonus.GravitySuit() && Characters.Sein.Abilities.Swimming.IsSwimming)) {
            hasDashed = false;
            RandomizerBonus.DoubleAirDashUsed = false;
        }

        if (Input.Glide.Pressed && timeWhenDashJumpHappened + 5f > Time.time) {
            timeWhenDashJumpHappened = 0f;
            var platformMovement = sein.PlatformBehaviour.PlatformMovement;
            var num2 = OffGroundSpeed - 2f;
            if (Mathf.Abs(platformMovement.LocalSpeedX) > num2) {
                platformMovement.LocalSpeedX = Mathf.Sign(platformMovement.LocalSpeedX) * num2;
            }
        }

        IChargeDashAttackable target;
        if (CanChargeDash()) {
            target = FindClosestAttackable;
        } else {
            target = null;
        }

        UpdateTargetHighlight(target);
        if (Input.RightShoulder.Pressed && num < 0.15f) {
            if (CanChargeDash()) {
                if (HasEnoughEnergy) {
                    SpendEnergy();
                    PerformChargeDash();
                    return;
                }

                ShowNotEnoughEnergy();
                lastPressTime = 0f;
            } else {
                if (CanPerformNormalDash()) {
                    PerformDash();
                    return;
                }

                if (CanWallDash()) {
                    PerformWallDash();
                    return;
                }

                if (CanPerformDashIntoWall()) {
                    PerformDashIntoWall();
                }
            }
        }
    }

    private void ShowNotEnoughEnergy() {
        UI.SeinUI.ShakeEnergyOrbBar();
        if (NotEnoughEnergySound) {
            Sound.Play(NotEnoughEnergySound.GetSound(null), transform.position, null);
        }
    }

    public void UpdateDashing() {
        var platformMovement = sein.PlatformBehaviour.PlatformMovement;
        UI.Cameras.Current.ChaseTarget.CameraSpeedMultiplier.x = Mathf.Clamp01(stateCurrentTime / DashTime);
        var velocity = DashSpeedOverTime.Evaluate(stateCurrentTime);
        velocity *= 1.0f + .2f * RandomizerBonus.Velocity();
        if (RandomizerBonus.GravitySuit() && Characters.Sein.Abilities.Swimming.IsSwimming) {
            var newSpeed = new Vector2(velocity, 0f);
            platformMovement.LocalSpeed = newSpeed.Rotate(sein.Abilities.Swimming.SwimAngle);
        } else if (RandomizerBonus.EnhancedDash && enhancedDashDirection.y != 0f) {
            platformMovement.LocalSpeed = enhancedDashDirection * velocity;
        } else {
            platformMovement.LocalSpeedX = (!faceLeft ? 1 : -1) * velocity;
        }

        sein.FaceLeft = faceLeft;
        if (AgainstWall()) {
            platformMovement.LocalSpeed = Vector2.zero;
        }

        SpriteRotation = Mathf.Lerp(SpriteRotation, sein.PlatformBehaviour.PlatformMovement.GroundAngle, 0.2f);
        if (sein.IsOnGround) {
            if (Input.Horizontal > 0f && faceLeft) {
                StopDashing();
            }

            if (Input.Horizontal < 0f && !faceLeft) {
                StopDashing();
            }

            if (!isOnGround && RandomizerBonus.EnhancedDash) {
                isOnGround = true;
                enhancedDashDirection = faceLeft ? Vector3.left : Vector3.right;
                SpriteRotation = sein.PlatformBehaviour.PlatformMovement.GroundAngle;
            }
        }

        if (stateCurrentTime > DashTime) {
            if (platformMovement.IsOnGround && Input.Horizontal == 0f) {
                platformMovement.LocalSpeedX = 0f;
            }

            ChangeState(State.Normal);
        }

        if (Input.Jump.OnPressed || Input.Glide.OnPressed) {
            platformMovement.LocalSpeedX = !faceLeft ? OffGroundSpeed : -OffGroundSpeed;
            sein.PlatformBehaviour.AirNoDeceleration.NoDeceleration = allowNoDecelerationForThisDash;
            stopAnimation = true;
            ChangeState(State.Normal);
            timeWhenDashJumpHappened = Time.time;
        }

        if (RaycastTest() && isOnGround) {
            StickOntoGround();
            return;
        }

        isOnGround = false;
    }

    private void StickOntoGround() {
        var platformMovement = sein.PlatformBehaviour.PlatformMovement;
        var vector = platformMovement.Position;
        platformMovement.PlaceOnGround(0f, 8f);
        var vector2 = vector;
        platformMovement.PlaceOnGround(0.5f, 8f);
        var vector3 = vector;
        vector = vector2;
        if (vector3.y > vector2.y) {
            vector = vector3;
        }

        platformMovement.Position = vector;
    }

    public void UpdateChargeDashing() {
        var platformMovement = sein.PlatformBehaviour.PlatformMovement;
        AttackNearbyEnemies();
        sein.Mortality.DamageReciever.MakeInvincibleToEnemies(1f);
        var velocity = ChargeDashSpeedOverTime.Evaluate(stateCurrentTime);
        velocity *= 1.0f + .2f * RandomizerBonus.Velocity();
        if (chargeDashAtTarget) {
            platformMovement.LocalSpeed = chargeDashDirection * velocity;
        } else if (RandomizerBonus.EnhancedDash && enhancedDashDirection.y != 0f) {
            platformMovement.LocalSpeed = enhancedDashDirection * velocity;
        } else {
            platformMovement.LocalSpeedX = (!faceLeft ? 1 : -1) * velocity;
        }

        if (hasHitAttackable) {
            platformMovement.LocalSpeed *= 0.33f;
        }

        sein.FaceLeft = faceLeft;
        SpriteRotation = Mathf.Lerp(SpriteRotation, sein.PlatformBehaviour.PlatformMovement.GroundAngle, 0.3f);
        if (AgainstWall()) {
            platformMovement.LocalSpeed = Vector2.zero;
        }

        if (sein.IsOnGround) {
            if (Input.Horizontal > 0f && faceLeft) {
                StopDashing();
            }

            if (Input.Horizontal < 0f && !faceLeft) {
                StopDashing();
            }

            if (!isOnGround && RandomizerBonus.EnhancedDash) {
                isOnGround = true;
                enhancedDashDirection = faceLeft ? Vector3.left : Vector3.right;
                SpriteRotation = sein.PlatformBehaviour.PlatformMovement.GroundAngle;
            }
        }

        if (stateCurrentTime > ChargeDashTime) {
            ChangeState(State.Normal);
        }

        if (Input.Jump.OnPressed || Input.Glide.OnPressed) {
            platformMovement.LocalSpeedX = !faceLeft ? OffGroundSpeed : -OffGroundSpeed;
            sein.PlatformBehaviour.AirNoDeceleration.NoDeceleration = true;
            stopAnimation = true;
            ChangeState(State.Normal);
        }

        if (RaycastTest() && isOnGround && !chargeDashAtTarget) {
            StickOntoGround();
            return;
        }

        isOnGround = false;
    }

    public void UpdateState() {
        UI.Cameras.Current.ChaseTarget.CameraSpeedMultiplier.x = 1f;
        if (Input.RightShoulder.OnPressed) {
            lastPressTime = Time.time;
        }

        if (Input.ChargeJump.Released) {
            chargeJumpWasReleased = true;
        }

        switch (CurrentState) {
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

        stateCurrentTime += Time.deltaTime;
    }

    public void StopDashing() {
        sein.PlatformBehaviour.PlatformMovement.LocalSpeed = Vector2.zero;
        ChangeState(State.Normal);
        stopAnimation = true;
        chargeDashAtTarget = false;
    }

    private bool RaycastTest() {
        var a = Vector3.Cross(sein.PlatformBehaviour.PlatformMovement.GroundRayNormal, Vector3.forward);
        var num = sein.Speed.x * Time.deltaTime;
        var vector = sein.Position + a * num + Vector3.up;
        var vector2 = Vector3.down * (1.8f + Mathf.Abs(num));
        Debug.DrawRay(vector, vector2, Color.yellow, 0.5f);
        return sein.Controller.RayTest(vector, vector2, out _);
    }

    public void ResetDashLimit() {
        hasDashed = false;
        RandomizerBonus.DoubleAirDashUsed = false;
    }

    public float AdjustedEnergyCost {
        get {
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

    private SeinCharacter sein;

    private bool faceLeft;

    private float stateCurrentTime;

    private HashSet<IAttackable> attackablesIgnore = new HashSet<IAttackable>();

    private bool stopAnimation;

    private float lastPressTime;

    private float lastDashTime;

    private bool isOnGround;

    public static bool RainbowDashActivated;

    private bool hasDashed;

    public float ChargeDashTargetMaxDistance = 20f;

    private float timeOfLastExplosionEffect;

    private float timeWhenDashJumpHappened;

    private bool allowNoDecelerationForThisDash;

    private IAttackable chargeDashAttackTarget;

    private bool hasHitAttackable;

    private bool chargeJumpWasReleased = true;

    private IChargeDashAttackable lastTarget;

    public float SpriteRotation;

    private Vector3 chargeDashDirection;

    private bool chargeDashAtTarget;

    private Vector3 enhancedDashDirection;

    public enum State {
        Normal,
        Dashing,
        ChargeDashing,
    }
}

using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinBashAttack : CharacterState, ISeinReceiver {
    static SeinBashAttack() {
        OnBashAttackEvent = delegate { };
        OnBashBegin = delegate { };
        OnBashEnemy = delegate { };
    }

    public static event Action<Vector2> OnBashAttackEvent;

    public static event Action OnBashBegin;

    public static event Action<EntityTargetting> OnBashEnemy;

    public Component TargetAsComponent => Target as Component;

    public CharacterAirNoDeceleration AirNoDeceleration => Sein.PlatformBehaviour.AirNoDeceleration;

    public SeinDoubleJump DoubleJump => Sein.Abilities.DoubleJump;

    public CharacterApplyFrictionToSpeed ApplyFrictionToSpeed => Sein.PlatformBehaviour.ApplyFrictionToSpeed;

    public CharacterGravity Gravity => Sein.PlatformBehaviour.Gravity;

    public CharacterLeftRightMovement CharacterLeftRightMovement => Sein.PlatformBehaviour.LeftRightMovement;

    public PlayerAbilities PlayerAbilities => Sein.PlayerAbilities;

    public PlatformMovement PlatformMovement => Sein.PlatformBehaviour.PlatformMovement;

    public SeinController SeinController => Sein.Controller;

    public TextureAnimationWithTransitions BashChargeAnimation {
        get {
            Vector2 vector = directionToTarget;
            var num = Mathf.Cos(0.3926991f);
            var directionalAnimationSet = !Sein.Controller.IsSwimming ? BashChargeAnimationSet : SwimBashChargeAnimationSet;
            vector.x = Mathf.Abs(vector.x);
            if (Vector3.Dot(Vector3.up, vector) > num) {
                return directionalAnimationSet.Up;
            }

            var vector2 = new Vector3(1f, 1f);
            if (Vector3.Dot(vector2.normalized, vector) > num) {
                return directionalAnimationSet.UpDiagonal;
            }

            if (Vector3.Dot(Vector3.right, vector) > num) {
                return directionalAnimationSet.Horizontal;
            }

            var vector3 = new Vector3(1f, -1f);
            if (Vector3.Dot(vector3.normalized, vector) > num) {
                return directionalAnimationSet.DownDiagonal;
            }

            if (Vector3.Dot(Vector3.down, vector) > num) {
                return directionalAnimationSet.Down;
            }

            return directionalAnimationSet.Up;
        }
    }

    public TextureAnimationWithTransitions BashJumpAnimation {
        get {
            var vector = MoonMath.Angle.VectorFromAngle(bashAngle + 90f);
            var num = Mathf.Cos(0.3926991f);
            var directionalAnimationSet = !Sein.Controller.IsSwimming ? BashJumpAnimationSet : SwimBashJumpAnimationSet;
            vector.x = Mathf.Abs(vector.x);
            if (Vector3.Dot(Vector3.up, vector) > num) {
                return directionalAnimationSet.Up;
            }

            var vector2 = new Vector3(1f, 1f);
            if (Vector3.Dot(vector2.normalized, vector) > num) {
                return directionalAnimationSet.UpDiagonal;
            }

            if (Vector3.Dot(Vector3.right, vector) > num) {
                return directionalAnimationSet.Horizontal;
            }

            var vector3 = new Vector3(1f, -1f);
            if (Vector3.Dot(vector3.normalized, vector) > num) {
                return directionalAnimationSet.DownDiagonal;
            }

            if (Vector3.Dot(Vector3.down, vector) > num) {
                return directionalAnimationSet.Down;
            }

            return directionalAnimationSet.Up;
        }
    }

    public bool SpriteMirrorLock {
        get => spriteMirrorLock;
        set {
            if (spriteMirrorLock != value) {
                spriteMirrorLock = value;
                int @lock;
                if (value) {
                    var spriteMirror = Sein.PlatformBehaviour.Visuals.SpriteMirror;
                    @lock = spriteMirror.Lock;
                    spriteMirror.Lock = @lock + 1;
                    return;
                }

                var spriteMirror2 = Sein.PlatformBehaviour.Visuals.SpriteMirror;
                @lock = spriteMirror2.Lock;
                spriteMirror2.Lock = @lock - 1;
            }
        }
    }

    public bool CanBash => PlayerAbilities.Bash.HasAbility && !(TargetAsComponent == null) && TargetAsComponent.gameObject.activeInHierarchy && (!(Sein != null) || Sein.Active) && !SeinAbilityRestrictZone.IsInside();

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        seinTransform = Sein.transform;
        Sein.Abilities.Bash = this;
    }

    public void Start() {
        hasStarted = true;
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
        CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent += ModifyHorizontalPlatformMovementSettings;
        Gravity.ModifyGravityPlatformMovementSettingsEvent += ModifyGravityPlatformMovementSettings;
    }

    public new void OnDestroy() {
        base.OnDestroy();
        if (hasStarted) {
            Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
            CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent -= ModifyHorizontalPlatformMovementSettings;
            Gravity.ModifyGravityPlatformMovementSettingsEvent -= ModifyGravityPlatformMovementSettings;
        }
    }

    public void ModifyGravityPlatformMovementSettings(GravityPlatformMovementSettings settings) {
        if (IsBashing) {
            settings.GravityStrength = 0f;
        }
    }

    public void ModifyHorizontalPlatformMovementSettings(HorizontalPlatformMovementSettings settings) {
        if (IsBashing) {
            settings.LockInput = true;
        }
    }

    public void OnRestoreCheckpoint() {
        if (IsBashing) {
            ExitBash();
        }

        ApplyFrictionToSpeed.SpeedFactor = 0f;
        spriteMirrorLock = false;
    }

    public void OnDisable() {
        if (IsBashing) {
            ExitBash();
        }
    }

    public void ExitBash() {
        if (GameController.Instance) {
            GameController.Instance.ResumeGameplay();
        }

        ApplyFrictionToSpeed.SpeedFactor = 0f;
        IsBashing = false;
        isEnhancedBashing = false;
    }

    public void MovePlayerToTargetAndCreateEffect() {
        var component = Target as Component;
        var vector = !InstantiateUtility.IsDestroyed(component) ? component.transform.position : PlatformMovement.Position;
        if (isEnhancedBashing) {
            vector = enhancedBashTarget;
        }

        var gameObject = (GameObject)InstantiateUtility.Instantiate(BashFromFx);
        gameObject.transform.position = vector;
        var localScale = gameObject.transform.localScale;
        localScale.x = (vector - PlatformMovement.Position).magnitude;
        gameObject.transform.localScale = localScale;
        gameObject.transform.localRotation = Quaternion.Euler(0f, 0f, MoonMath.Angle.AngleFromVector(PlatformMovement.Position - vector));
        if (!PlatformMovement.IsOnGround) {
            PlatformMovement.Position2D = vector;
        }
    }

    public void BeginBash() {
        timeRemainingOfBashButtonPress = 0f;
        IsBashing = true;
        Vector3 target;
        if (isEnhancedBashing) {
            target = enhancedBashTarget = PlatformMovement.Position + Vector3.up;
        } else {
            Target.OnEnterBash();
            target = TargetAsComponent.transform.position;
        }

        Sound.Play(!Sein.PlayerAbilities.BashBuff.HasAbility ? BashStartSound.GetSound(null) : UpgradedBashStartSound.GetSound(null), seinTransform.position, null);
        if (GameController.Instance) {
            GameController.Instance.SuspendGameplay();
        }

        if (UI.Cameras.Current != null) {
            SuspensionManager.GetSuspendables(bashSuspendables, UI.Cameras.Current.GameObject);
            SuspensionManager.Resume(bashSuspendables);
            bashSuspendables.Clear();
        }

        PlatformMovement.LocalSpeed = Vector2.zero;
        var vectorToTarget = target - PlatformMovement.Position;
        var gameObject = (GameObject)InstantiateUtility.Instantiate(BashAttackGamePrefab);
        bashAttackGame = gameObject.GetComponent<BashAttackGame>();
        bashAttackGame.SendDirection(vectorToTarget);
        bashAttackGame.OnBashGameComplete += BashGameComplete;
        bashAttackGame.transform.position = target;
        vectorToTarget = Vector3.ClampMagnitude(vectorToTarget, 2f);
        playerTargetPosition = target - vectorToTarget;
        directionToTarget = vectorToTarget.normalized;
        OnBashBegin();
        Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(BashChargeAnimation, 10, ShouldBashChargeAnimationKeepPlaying);
    }

    public void BashGameComplete(float angle) {
        JumpOffTarget(angle);
        AttackTarget();
        ExitBash();
    }

    public void JumpOffTarget(float angle) {
        if (GameController.Instance) {
            GameController.Instance.ResumeGameplay();
        }

        Vector2 vector = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
        var vector2 = vector * (BashVelocity + BashVelocity * .10f * RandomizerBonus.Velocity());
        PlatformMovement.WorldSpeed = vector2;
        AirNoDeceleration.NoDeceleration = true;
        Sein.ResetAirLimits();
        frictionTimeRemaining = FrictionDuration;
        ApplyFrictionToSpeed.SpeedToSlowDown = PlatformMovement.LocalSpeed;
        MovePlayerToTargetAndCreateEffect();
        var component = Target as Component;
        var position = !InstantiateUtility.IsDestroyed(component) ? component.transform.position : Sein.Position;
        var gameObject = (GameObject)InstantiateUtility.Instantiate(BashOffFx);
        gameObject.transform.position = position;
        var localScale = gameObject.transform.localScale;
        localScale.x = vector2.magnitude * 0.1f;
        gameObject.transform.localScale = localScale;
        gameObject.transform.localRotation = Quaternion.Euler(0f, 0f, MoonMath.Angle.AngleFromVector(vector));
        if (BashReleaseEffect) {
            ((GameObject)InstantiateUtility.Instantiate(BashReleaseEffect)).transform.position = position;
        }

        OnBashAttackEvent(vector2);
        timeRemainingTillNextBash = DelayTillNextBash;
        var characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(BashJumpAnimation, 10, ShouldBashJumpAnimationKeepPlaying);
        characterAnimationState.OnStartPlaying = OnAnimationStart;
        characterAnimationState.OnStopPlaying = OnAnimationEnd;
        Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft = vector2.x > 0f;
        if (Sein.Abilities.Swimming) {
            Sein.Abilities.Swimming.OnBash(angle);
        }
    }

    public void OnAnimationStart() {
        SpriteMirrorLock = true;
    }

    public void AttackTarget() {
        var component = Target as Component;
        if (!InstantiateUtility.IsDestroyed(component)) {
            var force = -MoonMath.Angle.VectorFromAngle(bashAngle + 90f) * (4f + RandomizerBonus.Velocity());
            new Damage(RandomizerBonusSkill.AbilityDamage(!Sein.PlayerAbilities.BashBuff.HasAbility ? Damage : UpgradedDamage), force, Characters.Sein.Position, DamageType.Bash, gameObject).DealToComponents(component.gameObject);
            var component2 = component.gameObject.GetComponent<EntityTargetting>();
            if (component2 && component2.Entity is Enemy) {
                OnBashEnemy(component2);
            }

            if (Sein.PlayerAbilities.BashBuff.HasAbility) {
                BeginBashThroughEnemies();
            }
        } else if (isEnhancedBashing && Sein.PlayerAbilities.BashBuff.HasAbility) {
            BeginBashThroughEnemies();
        }
    }

    public void BeginBashThroughEnemies() {
        bashThroughEnemiesRemainingTime = 0.5f;
        Sein.Mortality.DamageReciever.MakeInvincibleToEnemies(bashThroughEnemiesRemainingTime);
        enemiesBashedThrough.Clear();
    }

    public void UpdateBashThroughEnemies() {
        if (bashThroughEnemiesRemainingTime > 0f) {
            bashThroughEnemiesRemainingTime -= Time.deltaTime;
            for (var i = 0; i < Targets.Attackables.Count; i++) {
                var attackable = Targets.Attackables[i];
                if (attackable.CanBeSpiritFlamed() && !enemiesBashedThrough.Contains(attackable)) {
                    var vector = attackable.Position - Sein.PlatformBehaviour.PlatformMovement.Position;
                    if (vector.magnitude < 3f && Vector2.Dot(vector.normalized, PlatformMovement.LocalSpeed.normalized) > 0f) {
                        var damage = new Damage(UpgradedDamage, PlatformMovement.WorldSpeed.normalized, Sein.Position, DamageType.SpiritFlame, this.gameObject);
                        var gameObject = ((Component)attackable).gameObject;
                        damage.DealToComponents(gameObject);
                        enemiesBashedThrough.Add(attackable);
                        break;
                    }
                }
            }

            if (bashThroughEnemiesRemainingTime <= 0f) {
                bashThroughEnemiesRemainingTime = 0f;
                FinishBashThroughEnemies();
            }
        }
    }

    public void FinishBashThroughEnemies() {
        enemiesBashedThrough.Clear();
    }

    public void UpdateBashingState() {
        HandleBashAngle();
        Sein.Mortality.DamageReciever.MakeInvincibleToEnemies(0.2f);
        HandleMovingTowardsBashTarget();
        Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft = directionToTarget.x < 0f;
    }

    public void BashFailed() {
        if (NoBashTargetEffect) {
            ((GameObject)InstantiateUtility.Instantiate(NoBashTargetEffect, transform.position, Quaternion.identity)).transform.parent = seinTransform;
        }
    }

    public void UpdateNormalState() {
        Randomizer.BashWasQueued = Randomizer.QueueBash;
        if (Input.Bash.OnPressed || Randomizer.QueueBash) {
            Randomizer.QueueBash = false;
            timeRemainingOfBashButtonPress = 0.5f;
            if (Sein.IsOnGround && Sein.Speed.x == 0f && !SeinAbilityRestrictZone.IsInside() && !Sein.Abilities.Carry.IsCarrying) {
                Sein.Animation.Play(BackFlipAnimation, 10);
                Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY = BackFlipSpeed;
                if (!Sein.PlayerAbilities.BashBuff.HasAbility ? StationaryBashSound : UpgradedStationaryBashSound) {
                    Sound.Play(!Sein.PlayerAbilities.BashBuff.HasAbility ? StationaryBashSound.GetSound(null) : UpgradedStationaryBashSound.GetSound(null), transform.position, null);
                }
            }
        }

        if (timeRemainingOfBashButtonPress > 0f) {
            timeRemainingOfBashButtonPress -= Time.deltaTime;
            if ((Input.Bash.OnReleased || (timeRemainingOfBashButtonPress <= 0.4 && timeRemainingOfBashButtonPress >= 0.4 - Time.deltaTime)) && !SeinAbilityRestrictZone.IsInside() && !Sein.Abilities.Carry.IsCarrying) {
                BashFailed();
            }

            if (Input.Bash.Released || timeRemainingOfBashButtonPress <= 0f) {
                timeRemainingOfBashButtonPress = 0f;
            }

            if (RandomizerBonus.EnhancedBash && timeRemainingOfBashButtonPress <= 0.3f) {
                isEnhancedBashing = true;
                BeginBash();
            }
        }

        if ((timeRemainingOfBashButtonPress > 0f || Randomizer.BashWasQueued) && CanBash) {
            BeginBash();
        }

        HandleFindingTarget();
        UpdateTargetHighlight(Target);
    }

    public override void UpdateCharacterState() {
        if (Sein.IsSuspended) {
            return;
        }

        if (!Sein.PlayerAbilities.Bash.HasAbility) {
            return;
        }

        if (!Sein.Active) {
            ExitBash();
            return;
        }

        if (timeRemainingTillNextBash > 0f) {
            timeRemainingTillNextBash -= Time.deltaTime;
        }

        UpdateBashThroughEnemies();
        if (frictionTimeRemaining > 0f) {
            frictionTimeRemaining -= Time.deltaTime;
            var time = FrictionDuration - frictionTimeRemaining;
            ApplyFrictionToSpeed.SpeedFactor = FrictionCurve.Evaluate(time);
        }

        if (frictionTimeRemaining + NoAirDecelerationDuration - FrictionDuration > 0f) {
            AirNoDeceleration.NoDeceleration = true;
        }

        if (IsBashing) {
            UpdateBashingState();
            return;
        }

        UpdateNormalState();
    }

    public void HandleMovingTowardsBashTarget() {
        var a = playerTargetPosition - PlatformMovement.Position;
        PlatformMovement.WorldSpeed = a / Time.deltaTime * 0.1f;
    }

    public void HandleBashAngle() {
        if (!InstantiateUtility.IsDestroyed(bashAttackGame)) {
            bashAngle = bashAttackGame.Angle;
        }
    }

    public void HandleFindingTarget() {
        if (Sein.Controller.IsCarrying) {
            Target = null;
            return;
        }

        if (timeRemainingTillNextBash > 0f) {
            Target = null;
            return;
        }

        if (PlayerAbilities.Bash.HasAbility) {
            Target = FindClosestAttackHandler();
            return;
        }

        Target = null;
    }

    public void UpdateTargetHighlight(IBashAttackable target) {
        if (lastTarget == target) {
            return;
        }

        if (!InstantiateUtility.IsDestroyed(lastTarget as Component)) {
            lastTarget.OnBashDehighlight();
        }

        lastTarget = target;
        if (!InstantiateUtility.IsDestroyed(lastTarget as Component)) {
            lastTarget.OnBashHighlight();
        }
    }

    public IBashAttackable FindClosestAttackHandler() {
        IBashAttackable result = null;
        var num = float.MaxValue;
        var num2 = int.MinValue;
        var position = Sein.Position;
        for (var i = 0; i < Targets.Attackables.Count; i++) {
            var attackable = Targets.Attackables[i];
            if (attackable.CanBeBashed()) {
                var magnitude = (attackable.Position - position).magnitude;
                if (magnitude <= Range) {
                    var bashAttackable = attackable as IBashAttackable;
                    if (bashAttackable != null) {
                        var bashPriority = bashAttackable.BashPriority;
                        if ((bashPriority > num2 || (magnitude <= num && bashPriority == num2)) && Sein.Controller.RayTest(((Component)bashAttackable).gameObject)) {
                            num = magnitude;
                            num2 = bashPriority;
                            result = bashAttackable;
                        }
                    }
                }
            }
        }

        return result;
    }

    public bool ShouldBashChargeAnimationKeepPlaying() {
        return IsBashing;
    }

    public bool ShouldBashJumpAnimationKeepPlaying() {
        return !PlatformMovement.IsOnGround;
    }

    public void OnAnimationEnd() {
        SpriteMirrorLock = false;
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref timeRemainingOfBashButtonPress);
        ar.Serialize(ref frictionTimeRemaining);
        ar.Serialize(ref timeRemainingTillNextBash);
        ar.Serialize(ref spriteMirrorLock);
        base.Serialize(ar);
        if (ar.Reading && !InstantiateUtility.IsDestroyed(bashAttackGame)) {
            InstantiateUtility.Destroy(bashAttackGame.gameObject);
        }
    }

    public DirectionalAnimationSet BashChargeAnimationSet;

    public DirectionalAnimationSet BashJumpAnimationSet;

    public DirectionalAnimationSet SwimBashChargeAnimationSet;

    public DirectionalAnimationSet SwimBashJumpAnimationSet;

    public TextureAnimationWithTransitions BackFlipAnimation;

    public GameObject BashAttackGamePrefab;

    public SoundProvider BashEndSound;

    public SoundProvider BashLoopSound;

    public SoundProvider BashStartSound;

    public SoundProvider StationaryBashSound;

    public SoundProvider UpgradedBashEndSound;

    public SoundProvider UpgradedBashLoopSound;

    public SoundProvider UpgradedBashStartSound;

    public SoundProvider UpgradedStationaryBashSound;

    public GameObject BashFromFx;

    public GameObject BashOffFx;

    public GameObject BashReleaseEffect;

    public float BashVelocity = 56.568f;

    public float Damage = 2f;

    public float UpgradedDamage = 5f;

    public float DelayTillNextBash = 0.2f;

    public AnimationCurve FrictionCurve;

    public float FrictionDuration;

    public float NoAirDecelerationDuration = 0.2f;

    public float Range = 4f;

    public SeinCharacter Sein;

    public IBashAttackable Target;

    private Vector3 directionToTarget;

    private float bashAngle;

    private Vector3 playerTargetPosition;

    private BashAttackGame bashAttackGame;

    private float frictionTimeRemaining;

    private IBashAttackable lastTarget;

    private Transform seinTransform;

    private bool spriteMirrorLock;

    private float timeRemainingTillNextBash;

    private float timeRemainingOfBashButtonPress;

    private readonly HashSet<ISuspendable> bashSuspendables = new HashSet<ISuspendable>();

    public GameObject NoBashTargetEffect;

    public bool IsBashing;

    private float bashThroughEnemiesRemainingTime;

    private HashSet<IAttackable> enemiesBashedThrough = new HashSet<IAttackable>();

    private bool hasStarted;

    public float BackFlipSpeed = 5f;

    private Vector3 enhancedBashTarget;

    private bool isEnhancedBashing;

    [Serializable]
    public class DirectionalAnimationSet {
        public TextureAnimationWithTransitions Down;

        public TextureAnimationWithTransitions DownDiagonal;

        public TextureAnimationWithTransitions Horizontal;

        public TextureAnimationWithTransitions Up;

        public TextureAnimationWithTransitions UpDiagonal;
    }
}

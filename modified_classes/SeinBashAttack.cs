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

    public Component TargetAsComponent {
        get { return Target as Component; }
    }

    public CharacterAirNoDeceleration AirNoDeceleration {
        get { return Sein.PlatformBehaviour.AirNoDeceleration; }
    }

    public SeinDoubleJump DoubleJump {
        get { return Sein.Abilities.DoubleJump; }
    }

    public CharacterApplyFrictionToSpeed ApplyFrictionToSpeed {
        get { return Sein.PlatformBehaviour.ApplyFrictionToSpeed; }
    }

    public CharacterGravity Gravity {
        get { return Sein.PlatformBehaviour.Gravity; }
    }

    public CharacterLeftRightMovement CharacterLeftRightMovement {
        get { return Sein.PlatformBehaviour.LeftRightMovement; }
    }

    public PlayerAbilities PlayerAbilities {
        get { return Sein.PlayerAbilities; }
    }

    public PlatformMovement PlatformMovement {
        get { return Sein.PlatformBehaviour.PlatformMovement; }
    }

    public SeinController SeinController {
        get { return Sein.Controller; }
    }

    public TextureAnimationWithTransitions BashChargeAnimation {
        get {
            Vector2 vector = m_directionToTarget;
            float num = Mathf.Cos(0.3926991f);
            DirectionalAnimationSet directionalAnimationSet = (!Sein.Controller.IsSwimming) ? BashChargeAnimationSet : SwimBashChargeAnimationSet;
            vector.x = Mathf.Abs(vector.x);
            if (Vector3.Dot(Vector3.up, vector) > num) {
                return directionalAnimationSet.Up;
            }

            Vector3 vector2 = new Vector3(1f, 1f);
            if (Vector3.Dot(vector2.normalized, vector) > num) {
                return directionalAnimationSet.UpDiagonal;
            }

            if (Vector3.Dot(Vector3.right, vector) > num) {
                return directionalAnimationSet.Horizontal;
            }

            Vector3 vector3 = new Vector3(1f, -1f);
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
            Vector2 vector = MoonMath.Angle.VectorFromAngle(m_bashAngle + 90f);
            float num = Mathf.Cos(0.3926991f);
            DirectionalAnimationSet directionalAnimationSet = (!Sein.Controller.IsSwimming) ? BashJumpAnimationSet : SwimBashJumpAnimationSet;
            vector.x = Mathf.Abs(vector.x);
            if (Vector3.Dot(Vector3.up, vector) > num) {
                return directionalAnimationSet.Up;
            }

            Vector3 vector2 = new Vector3(1f, 1f);
            if (Vector3.Dot(vector2.normalized, vector) > num) {
                return directionalAnimationSet.UpDiagonal;
            }

            if (Vector3.Dot(Vector3.right, vector) > num) {
                return directionalAnimationSet.Horizontal;
            }

            Vector3 vector3 = new Vector3(1f, -1f);
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
        get { return m_spriteMirrorLock; }
        set {
            if (m_spriteMirrorLock != value) {
                m_spriteMirrorLock = value;
                int @lock;
                if (value) {
                    CharacterSpriteMirror spriteMirror = Sein.PlatformBehaviour.Visuals.SpriteMirror;
                    @lock = spriteMirror.Lock;
                    spriteMirror.Lock = @lock + 1;
                    return;
                }

                CharacterSpriteMirror spriteMirror2 = Sein.PlatformBehaviour.Visuals.SpriteMirror;
                @lock = spriteMirror2.Lock;
                spriteMirror2.Lock = @lock - 1;
            }
        }
    }

    public bool CanBash {
        get { return PlayerAbilities.Bash.HasAbility && !(TargetAsComponent == null) && TargetAsComponent.gameObject.activeInHierarchy && (!(Sein != null) || Sein.Active) && !SeinAbilityRestrictZone.IsInside(); }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        m_seinTransform = Sein.transform;
        Sein.Abilities.Bash = this;
    }

    public void Start() {
        m_hasStarted = true;
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
        CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent += ModifyHorizontalPlatformMovementSettings;
        Gravity.ModifyGravityPlatformMovementSettingsEvent += ModifyGravityPlatformMovementSettings;
    }

    public new void OnDestroy() {
        base.OnDestroy();
        if (m_hasStarted) {
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
        m_spriteMirrorLock = false;
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
        m_isEnhancedBashing = false;
    }

    public void MovePlayerToTargetAndCreateEffect() {
        Component component = Target as Component;
        Vector3 vector = (!InstantiateUtility.IsDestroyed(component)) ? component.transform.position : PlatformMovement.Position;
        if (m_isEnhancedBashing) {
            vector = m_enhancedBashTarget;
        }

        GameObject gameObject = (GameObject)InstantiateUtility.Instantiate(BashFromFx);
        gameObject.transform.position = vector;
        Vector3 localScale = gameObject.transform.localScale;
        localScale.x = (vector - PlatformMovement.Position).magnitude;
        gameObject.transform.localScale = localScale;
        gameObject.transform.localRotation = Quaternion.Euler(0f, 0f, MoonMath.Angle.AngleFromVector(PlatformMovement.Position - vector));
        if (!PlatformMovement.IsOnGround) {
            PlatformMovement.Position2D = vector;
        }
    }

    public void BeginBash() {
        m_timeRemainingOfBashButtonPress = 0f;
        IsBashing = true;
        Vector3 target;
        if (m_isEnhancedBashing) {
            target = m_enhancedBashTarget = PlatformMovement.Position + Vector3.up;
        } else {
            Target.OnEnterBash();
            target = TargetAsComponent.transform.position;
        }

        Sound.Play((!Sein.PlayerAbilities.BashBuff.HasAbility) ? BashStartSound.GetSound(null) : UpgradedBashStartSound.GetSound(null), m_seinTransform.position, null);
        if (GameController.Instance) {
            GameController.Instance.SuspendGameplay();
        }

        if (UI.Cameras.Current != null) {
            SuspensionManager.GetSuspendables(m_bashSuspendables, UI.Cameras.Current.GameObject);
            SuspensionManager.Resume(m_bashSuspendables);
            m_bashSuspendables.Clear();
        }

        PlatformMovement.LocalSpeed = Vector2.zero;
        Vector3 vectorToTarget = target - PlatformMovement.Position;
        GameObject gameObject = (GameObject)InstantiateUtility.Instantiate(BashAttackGamePrefab);
        m_bashAttackGame = gameObject.GetComponent<BashAttackGame>();
        m_bashAttackGame.SendDirection(vectorToTarget);
        m_bashAttackGame.BashGameComplete += BashGameComplete;
        m_bashAttackGame.transform.position = target;
        vectorToTarget = Vector3.ClampMagnitude(vectorToTarget, 2f);
        m_playerTargetPosition = target - vectorToTarget;
        m_directionToTarget = vectorToTarget.normalized;
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
        Vector2 vector2 = vector * (BashVelocity + BashVelocity * .10f * RandomizerBonus.Velocity());
        PlatformMovement.WorldSpeed = vector2;
        AirNoDeceleration.NoDeceleration = true;
        Sein.ResetAirLimits();
        m_frictionTimeRemaining = FrictionDuration;
        ApplyFrictionToSpeed.SpeedToSlowDown = PlatformMovement.LocalSpeed;
        MovePlayerToTargetAndCreateEffect();
        Component component = Target as Component;
        Vector3 position = (!InstantiateUtility.IsDestroyed(component)) ? component.transform.position : Sein.Position;
        GameObject gameObject = (GameObject)InstantiateUtility.Instantiate(BashOffFx);
        gameObject.transform.position = position;
        Vector3 localScale = gameObject.transform.localScale;
        localScale.x = vector2.magnitude * 0.1f;
        gameObject.transform.localScale = localScale;
        gameObject.transform.localRotation = Quaternion.Euler(0f, 0f, MoonMath.Angle.AngleFromVector(vector));
        if (BashReleaseEffect) {
            ((GameObject)InstantiateUtility.Instantiate(BashReleaseEffect)).transform.position = position;
        }

        OnBashAttackEvent(vector2);
        m_timeRemainingTillNextBash = DelayTillNextBash;
        CharacterAnimationSystem.CharacterAnimationState characterAnimationState = Sein.PlatformBehaviour.Visuals.Animation.Play(BashJumpAnimation, 10, ShouldBashJumpAnimationKeepPlaying);
        characterAnimationState.OnStartPlaying = OnAnimationStart;
        characterAnimationState.OnStopPlaying = OnAnimationEnd;
        Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft = (vector2.x > 0f);
        if (Sein.Abilities.Swimming) {
            Sein.Abilities.Swimming.OnBash(angle);
        }
    }

    public void OnAnimationStart() {
        SpriteMirrorLock = true;
    }

    public void AttackTarget() {
        Component component = Target as Component;
        if (!InstantiateUtility.IsDestroyed(component)) {
            Vector2 force = -MoonMath.Angle.VectorFromAngle(m_bashAngle + 90f) * (4f + RandomizerBonus.Velocity());
            new Damage(RandomizerBonusSkill.AbilityDamage((!Sein.PlayerAbilities.BashBuff.HasAbility) ? Damage : UpgradedDamage), force, Characters.Sein.Position, DamageType.Bash, gameObject).DealToComponents(component.gameObject);
            EntityTargetting component2 = component.gameObject.GetComponent<EntityTargetting>();
            if (component2 && component2.Entity is Enemy) {
                OnBashEnemy(component2);
            }

            if (Sein.PlayerAbilities.BashBuff.HasAbility) {
                BeginBashThroughEnemies();
            }
        } else if (m_isEnhancedBashing && Sein.PlayerAbilities.BashBuff.HasAbility) {
            BeginBashThroughEnemies();
        }
    }

    public void BeginBashThroughEnemies() {
        m_bashThroughEnemiesRemainingTime = 0.5f;
        Sein.Mortality.DamageReciever.MakeInvincibleToEnemies(m_bashThroughEnemiesRemainingTime);
        m_enemiesBashedThrough.Clear();
    }

    public void UpdateBashThroughEnemies() {
        if (m_bashThroughEnemiesRemainingTime > 0f) {
            m_bashThroughEnemiesRemainingTime -= Time.deltaTime;
            for (int i = 0; i < Targets.Attackables.Count; i++) {
                IAttackable attackable = Targets.Attackables[i];
                if (attackable.CanBeSpiritFlamed() && !m_enemiesBashedThrough.Contains(attackable)) {
                    Vector3 vector = attackable.Position - Sein.PlatformBehaviour.PlatformMovement.Position;
                    if (vector.magnitude < 3f && Vector2.Dot(vector.normalized, PlatformMovement.LocalSpeed.normalized) > 0f) {
                        Damage damage = new Damage(UpgradedDamage, PlatformMovement.WorldSpeed.normalized, Sein.Position, DamageType.SpiritFlame, this.gameObject);
                        GameObject gameObject = ((Component)attackable).gameObject;
                        damage.DealToComponents(gameObject);
                        m_enemiesBashedThrough.Add(attackable);
                        break;
                    }
                }
            }

            if (m_bashThroughEnemiesRemainingTime <= 0f) {
                m_bashThroughEnemiesRemainingTime = 0f;
                FinishBashThroughEnemies();
            }
        }
    }

    public void FinishBashThroughEnemies() {
        m_enemiesBashedThrough.Clear();
    }

    public void UpdateBashingState() {
        HandleBashAngle();
        Sein.Mortality.DamageReciever.MakeInvincibleToEnemies(0.2f);
        HandleMovingTowardsBashTarget();
        Sein.PlatformBehaviour.Visuals.SpriteMirror.FaceLeft = (m_directionToTarget.x < 0f);
    }

    public void BashFailed() {
        if (NoBashTargetEffect) {
            ((GameObject)InstantiateUtility.Instantiate(NoBashTargetEffect, transform.position, Quaternion.identity)).transform.parent = m_seinTransform;
        }
    }

    public void UpdateNormalState() {
        Randomizer.BashWasQueued = Randomizer.QueueBash;
        if (Input.Bash.OnPressed || Randomizer.QueueBash) {
            Randomizer.QueueBash = false;
            m_timeRemainingOfBashButtonPress = 0.5f;
            if (Sein.IsOnGround && Sein.Speed.x == 0f && !SeinAbilityRestrictZone.IsInside() && !Sein.Abilities.Carry.IsCarrying) {
                Sein.Animation.Play(BackFlipAnimation, 10);
                Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY = BackFlipSpeed;
                if ((!Sein.PlayerAbilities.BashBuff.HasAbility) ? StationaryBashSound : UpgradedStationaryBashSound) {
                    Sound.Play((!Sein.PlayerAbilities.BashBuff.HasAbility) ? StationaryBashSound.GetSound(null) : UpgradedStationaryBashSound.GetSound(null), transform.position, null);
                }
            }
        }

        if (m_timeRemainingOfBashButtonPress > 0f) {
            m_timeRemainingOfBashButtonPress -= Time.deltaTime;
            if ((Input.Bash.OnReleased || (m_timeRemainingOfBashButtonPress <= 0.4 && m_timeRemainingOfBashButtonPress >= 0.4 - Time.deltaTime)) && !SeinAbilityRestrictZone.IsInside() && !Sein.Abilities.Carry.IsCarrying) {
                BashFailed();
            }

            if (Input.Bash.Released || m_timeRemainingOfBashButtonPress <= 0f) {
                m_timeRemainingOfBashButtonPress = 0f;
            }

            if (RandomizerBonus.EnhancedBash && m_timeRemainingOfBashButtonPress <= 0.3f) {
                m_isEnhancedBashing = true;
                BeginBash();
            }
        }

        if ((m_timeRemainingOfBashButtonPress > 0f || Randomizer.BashWasQueued) && CanBash) {
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

        if (m_timeRemainingTillNextBash > 0f) {
            m_timeRemainingTillNextBash -= Time.deltaTime;
        }

        UpdateBashThroughEnemies();
        if (m_frictionTimeRemaining > 0f) {
            m_frictionTimeRemaining -= Time.deltaTime;
            float time = FrictionDuration - m_frictionTimeRemaining;
            ApplyFrictionToSpeed.SpeedFactor = FrictionCurve.Evaluate(time);
        }

        if (m_frictionTimeRemaining + NoAirDecelerationDuration - FrictionDuration > 0f) {
            AirNoDeceleration.NoDeceleration = true;
        }

        if (IsBashing) {
            UpdateBashingState();
            return;
        }

        UpdateNormalState();
    }

    public void HandleMovingTowardsBashTarget() {
        Vector3 a = m_playerTargetPosition - PlatformMovement.Position;
        PlatformMovement.WorldSpeed = a / Time.deltaTime * 0.1f;
    }

    public void HandleBashAngle() {
        if (!InstantiateUtility.IsDestroyed(m_bashAttackGame)) {
            m_bashAngle = m_bashAttackGame.Angle;
        }
    }

    public void HandleFindingTarget() {
        if (Sein.Controller.IsCarrying) {
            Target = null;
            return;
        }

        if (m_timeRemainingTillNextBash > 0f) {
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
        if (m_lastTarget == target) {
            return;
        }

        if (!InstantiateUtility.IsDestroyed(m_lastTarget as Component)) {
            m_lastTarget.OnBashDehighlight();
        }

        m_lastTarget = target;
        if (!InstantiateUtility.IsDestroyed(m_lastTarget as Component)) {
            m_lastTarget.OnBashHighlight();
        }
    }

    public IBashAttackable FindClosestAttackHandler() {
        IBashAttackable result = null;
        float num = float.MaxValue;
        int num2 = int.MinValue;
        Vector3 position = Sein.Position;
        for (int i = 0; i < Targets.Attackables.Count; i++) {
            IAttackable attackable = Targets.Attackables[i];
            if (attackable.CanBeBashed()) {
                float magnitude = (attackable.Position - position).magnitude;
                if (magnitude <= Range) {
                    IBashAttackable bashAttackable = attackable as IBashAttackable;
                    if (bashAttackable != null) {
                        int bashPriority = bashAttackable.BashPriority;
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
        ar.Serialize(ref m_timeRemainingOfBashButtonPress);
        ar.Serialize(ref m_frictionTimeRemaining);
        ar.Serialize(ref m_timeRemainingTillNextBash);
        ar.Serialize(ref m_spriteMirrorLock);
        base.Serialize(ar);
        if (ar.Reading && !InstantiateUtility.IsDestroyed(m_bashAttackGame)) {
            InstantiateUtility.Destroy(m_bashAttackGame.gameObject);
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

    private Vector3 m_directionToTarget;

    private float m_bashAngle;

    private Vector3 m_playerTargetPosition;

    private BashAttackGame m_bashAttackGame;

    private float m_frictionTimeRemaining;

    private IBashAttackable m_lastTarget;

    private Transform m_seinTransform;

    private bool m_spriteMirrorLock;

    private float m_timeRemainingTillNextBash;

    private float m_timeRemainingOfBashButtonPress;

    private readonly HashSet<ISuspendable> m_bashSuspendables = new HashSet<ISuspendable>();

    public GameObject NoBashTargetEffect;

    public bool IsBashing;

    private float m_bashThroughEnemiesRemainingTime;

    private HashSet<IAttackable> m_enemiesBashedThrough = new HashSet<IAttackable>();

    private bool m_hasStarted;

    public float BackFlipSpeed = 5f;

    private Vector3 m_enhancedBashTarget;

    private bool m_isEnhancedBashing;

    [Serializable]
    public class DirectionalAnimationSet {
        public TextureAnimationWithTransitions Down;

        public TextureAnimationWithTransitions DownDiagonal;

        public TextureAnimationWithTransitions Horizontal;

        public TextureAnimationWithTransitions Up;

        public TextureAnimationWithTransitions UpDiagonal;
    }
}

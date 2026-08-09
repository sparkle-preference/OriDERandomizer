using System;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinController : SaveSerialize, IDamageReciever, ISeinReceiver, ISuspendable, ICanActivateStompers {
    public event Action OnTriggeredAnimationFinished = delegate { };

    public bool InputLocked {
        get { return (Sein.Abilities.Lever && Sein.Abilities.Lever.InputLocked) || GameController.Instance.LockInput || GameController.Instance.LockInputByAction; }
    }

    public bool CanMove {
        get { return !InputLocked && !IsPlayingAnimation; }
    }

    public bool FaceLeft {
        get { return Sein.PlatformBehaviour.LeftRightMovement.SpriteMirror.FaceLeft; }
        set { Sein.PlatformBehaviour.LeftRightMovement.SpriteMirror.FaceLeft = value; }
    }

    public Transform Transform {
        get { return m_transform; }
    }

    public bool IsCrouching {
        get { return Sein.Abilities.Crouch && Sein.Abilities.Crouch.IsCrouching; }
    }

    private bool IsGrabbingBlock {
        get { return Sein.Abilities.GrabBlock && Sein.Abilities.GrabBlock.IsGrabbing; }
    }

    public bool IsGrabbingWall {
        get { return Sein.Abilities.GrabWall && Sein.Abilities.GrabWall.IsGrabbing; }
    }

    public bool IsGrabbingLever {
        get { return Sein.Abilities.Lever && Sein.Abilities.Lever.IsUsingLever; }
    }

    public bool IsGliding {
        get { return Sein.Abilities.Glide && Sein.Abilities.Glide.IsGliding; }
    }

    public bool IsPushPulling {
        get { return Sein.Abilities.GrabBlock && Sein.Abilities.GrabBlock.IsGrabbing; }
    }

    public bool IsSwimming {
        get { return Sein.Abilities.Swimming && Sein.Abilities.Swimming.IsSwimming; }
    }

    public bool IsBashing {
        get { return Sein.Abilities.Bash && Sein.Abilities.Bash.IsBashing; }
    }

    public bool IsAimingGrenade {
        get { return Sein.Abilities.Grenade && Sein.Abilities.Grenade.IsAiming; }
    }

    public bool IsInsideSoulFlame {
        get { return Sein.SoulFlame.InsideCheckpointMarker; }
    }

    public bool IsCarrying {
        get { return Sein.Abilities.Carry && (Sein.Abilities.Carry.IsCarrying || Sein.Abilities.Carry.IsPickingUp); }
    }

    public bool IsStomping {
        get { return Sein.Abilities.Stomp && Sein.Abilities.Stomp.IsStomping; }
    }

    public bool IsCharging {
        get { return Sein.Abilities.ChargeFlame && Sein.Abilities.ChargeFlame.IsCharging; }
    }

    public bool IsChargingJump {
        get { return Sein.Abilities.ChargeJumpCharging && Sein.Abilities.ChargeJumpCharging.IsCharging; }
    }

    public bool IsSuspended { get; set; }

    public Component[] Suspendables {
        get { return m_suspendables; }
    }

    public bool AnimationHasMetaData {
        get { return IsPlayingAnimation && Sein.Animation.Animator.CurrentAnimation.AnimationMetaData != null; }
    }

    public bool IsDashing {
        get { return Sein.Abilities.Dash && Sein.Abilities.Dash.IsDashingOrChangeDashing; }
    }

    public bool IsStandingOnEdge {
        get { return Sein.Abilities.StandingOnEdge && Sein.Abilities.StandingOnEdge.StandingOnEdge; }
    }

    public void EnterPlayingAnimation() {
        IsPlayingAnimation = true;
        if (Sein.PlatformBehaviour.PlatformMovement) {
            Vector2 localSpeed = Sein.PlatformBehaviour.PlatformMovement.LocalSpeed;
            localSpeed.x = 0f;
            if (localSpeed.y > 0f) {
                localSpeed.y = 0f;
            }

            Sein.PlatformBehaviour.PlatformMovement.LocalSpeed = localSpeed;
        }
    }

    public bool CanActivateSwitch(GameObject theSwitch) {
        return true;
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
    }

    public void HandleControllerInput() {
        if (Sein.PlatformBehaviour.LeftRightMovement == null) {
            return;
        }

        if (!IgnoreControllerInput) {
            if (CanMove && !LockMovementInput) {
                Sein.PlatformBehaviour.LeftRightMovement.HorizontalInput = Sein.Input.NormalizedHorizontal;
                if (Sein.Abilities.Run.Active && Sein.PlatformBehaviour.PlatformMovement.IsOnGround) {
                    float num = Sein.Controller.InputCurve.Evaluate(Mathf.Abs(Sein.Input.Horizontal)) * Mathf.Sign(Sein.Input.Horizontal);
                    Sein.PlatformBehaviour.LeftRightMovement.HorizontalInput = 0f;
                    if (num == 0f) {
                        m_horizontalInputDelay = 0.06666667f;
                    }

                    if (Mathf.Abs(num) > Sein.Controller.InputSettings.JogThreshold) {
                        Sein.PlatformBehaviour.LeftRightMovement.HorizontalInput = num;
                    }

                    m_horizontalInputDelay = Mathf.Max(0f, m_horizontalInputDelay - Time.deltaTime);
                    if (m_horizontalInputDelay == 0f) {
                        Sein.PlatformBehaviour.LeftRightMovement.HorizontalInput = num;
                    }
                } else {
                    Sein.PlatformBehaviour.LeftRightMovement.HorizontalInput = Input.NormalizedHorizontal;
                }
            } else {
                Sein.PlatformBehaviour.LeftRightMovement.HorizontalInput = 0f;
            }
        }

        OnHorizontalInputPostCalculate();
    }

    [UberBuildMethod]
    private void ProvideComponents() {
        m_suspendables = gameObject.FindComponentsInChildren<ISuspendable>();
    }

    public override void Awake() {
        m_transform = transform;
        ProvideComponents();
        SuspensionManager.Register(this);
        UI.Cameras.Current.ChangeTargetToCurrentCharacter();
        base.Awake();
    }

    public override void OnDestroy() {
        SuspensionManager.Unregister(this);
        base.OnDestroy();
        PlatformMovementPortalVisitor component = Sein.GetComponent<PlatformMovementPortalVisitor>();
        if (component) {
            PlatformMovementPortalVisitor platformMovementPortalVisitor = component;
            platformMovementPortalVisitor.OnGoThroughPortalAction = (Action)Delegate.Remove(platformMovementPortalVisitor.OnGoThroughPortalAction, new Action(OnGoThroughPortal));
        }
    }

    public void OnGoThroughPortal() {
        Sein.ResetAirLimits();
    }

    public void Start() {
        Sein.PlatformBehaviour.PlatformMovement.PlaceOnGround(0.5f, 0f);
        UI.Cameras.Current.MoveCameraToTargetInstantly();
        PlatformMovementPortalVisitor component = Sein.GetComponent<PlatformMovementPortalVisitor>();
        if (component) {
            PlatformMovementPortalVisitor platformMovementPortalVisitor = component;
            platformMovementPortalVisitor.OnGoThroughPortalAction = (Action)Delegate.Combine(platformMovementPortalVisitor.OnGoThroughPortalAction, new Action(OnGoThroughPortal));
        }
    }

    public void HandleJumping() {
        if (IgnoreControllerInput || LockMovementInput || !CanMove) {
            return;
        }

        bool grenadeJumpPressed = false;
        bool grenadeJumpHeld = false;
        if (RandomizerSettings.Controls.GrenadeJump == RandomizerSettings.GrenadeJumpMode.Auto) {
            grenadeJumpPressed = RandomizerRebinding.FreeGrenadeJump.OnPressed;
            grenadeJumpHeld = RandomizerRebinding.FreeGrenadeJump.Pressed;
        }

        if (Randomizer.GrenadeJumpQueued) {
            Randomizer.GrenadeJumpQueued = false;
            if (grenadeJumpHeld && CharacterState.IsActive(Sein.Abilities.WallChargeJump) && Sein.Abilities.GrabWall && Sein.Abilities.WallChargeJump.CanChargeJump && IsAimingGrenade) {
                Input.LeftShoulder.IsPressed = true;
                Input.Jump.IsPressed = true;
            }
        }

        if (grenadeJumpPressed && CharacterState.IsActive(Sein.Abilities.WallChargeJump) && Sein.Abilities.GrabWall && Sein.Abilities.WallChargeJump.CanChargeJump && Sein.Abilities.Grenade && Sein.Abilities.Grenade.CanAim && !IsAimingGrenade) {
            Randomizer.GrenadeJumpQueued = true;
            Input.LeftShoulder.IsPressed = true;
            Input.Jump.IsPressed = false;
        }

        if (Input.Jump.OnPressed) {
            PerformJump();
        }
    }

    public void PerformJump() {
        if (CharacterState.IsActive(Sein.Abilities.WallChargeJump) && Sein.Abilities.GrabWall && Sein.Abilities.WallChargeJump.CanChargeJump) {
            Sein.Abilities.WallChargeJump.PerformChargeJump();
        } else if (CharacterState.IsActive(Sein.Abilities.WallJump) && Sein.Abilities.WallJump.CanPerformWallJump) {
            Sein.Abilities.WallJump.PerformWallJump();
        } else if (!IsGrabbingBlock) {
            if (CharacterState.IsActive(Sein.Abilities.ChargeJump) && Sein.Abilities.ChargeJump.CanChargeJump) {
                Sein.Abilities.ChargeJump.PerformChargeJump();
            } else if (CharacterState.IsActive(Sein.Abilities.Jump) && Sein.Abilities.Jump.CanJump) {
                Sein.Abilities.Jump.PerformJump();
            } else if (CharacterState.IsActive(Sein.Abilities.DoubleJump) && Sein.Abilities.DoubleJump.CanDoubleJump) {
                if (Sein.Controller.IsGliding) {
                    Sein.Abilities.Glide.Exit();
                }

                Sein.Abilities.DoubleJump.PerformDoubleJump();
            }
        }
    }

    public bool RayTest(GameObject target) {
        return RayTest(target, Vector2.zero, Vector2.zero);
    }

    public bool RayTest(GameObject target, Vector2 startOffset, Vector2 endOffset) {
        Vector3 vector = m_transform.position + (Vector3)startOffset;
        Vector3 a = target.transform.position + (Vector3)endOffset;
        Vector3 vector2 = a - vector;
        Rigidbody component = target.GetComponent<Rigidbody>();
        RaycastHit raycastHit;
        return !Physics.Raycast(vector, vector2.normalized, out raycastHit, vector2.magnitude, RayTestLayerMask) || !(raycastHit.collider.gameObject != target) || (component && !(component != raycastHit.collider.attachedRigidbody)) || raycastHit.collider.isTrigger;
    }

    public bool RayTest(Vector3 position, Vector3 delta, out RaycastHit hitInfo) {
        float magnitude = delta.magnitude;
        return Physics.Raycast(position, delta / magnitude, out hitInfo, magnitude);
    }

    public void StopAnimation() {
        IsPlayingAnimation = false;
    }

    public void PlayAnimation(TextureAnimationWithTransitions animation) {
        Characters.Sein.Controller.EnterPlayingAnimation();
        if (animation.Animation.Loop) {
            Sein.PlatformBehaviour.Visuals.Animation.PlayLoop(animation, 200, ShouldAnimationKeepPlaying);
        } else {
            Sein.PlatformBehaviour.Visuals.Animation.Play(animation, 200, ShouldAnimationKeepPlaying);
            Sein.PlatformBehaviour.Visuals.Animation.Animator.OnAnimationEndEvent += OnAnimationEndEvent;
        }
    }

    private void OnAnimationEndEvent(TextureAnimation textureAnimation) {
        Sein.PlatformBehaviour.Visuals.Animation.Animator.OnAnimationEndEvent -= OnAnimationEndEvent;
        if (IsPlayingAnimation) {
            IsPlayingAnimation = false;
            OnTriggeredAnimationFinished();
        }
    }

    public bool ShouldAnimationKeepPlaying() {
        return IsPlayingAnimation;
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (IsPlayingAnimation) {
            TextureAnimation currentAnimation = Sein.Animation.Animator.CurrentAnimation;
            if (currentAnimation) {
                AnimationMetaData animationMetaData = currentAnimation.AnimationMetaData;
                if (animationMetaData) {
                    Vector3 deltaPositionAtTime = animationMetaData.CameraData.GetDeltaPositionAtTime(Sein.Animation.Animator.CurrentAnimationTime);
                    Vector3 a = Vector3.Scale(deltaPositionAtTime, Sein.PlatformBehaviour.Visuals.Sprite.transform.lossyScale);
                    if (FaceLeft) {
                        a.x *= -1f;
                    }

                    Sein.PlatformBehaviour.PlatformMovement.LocalSpeed = a / Time.deltaTime;
                } else {
                    Sein.PlatformBehaviour.PlatformMovement.LocalSpeed = Vector2.zero;
                }
            }
        }

        HandleControllerInput();
        HandleJumping();
        UpdateOriActiveState();
    }

    public void HandleOffscreenIssue() {
        if (Scenes.Manager.PositionInsideSceneStillLoading(Sein.Position)) {
            Sein.PlatformBehaviour.PlatformMovement.LocalSpeed = Vector2.zero;
            Sein.Mortality.DamageReciever.MakeInvincible(0.1f);
        }
    }

    public void UpdateOriActiveState() {
        if (Characters.Ori && Characters.Ori.gameObject.activeSelf != Sein.PlayerAbilities.SpiritFlame.HasAbility) {
            Characters.Ori.gameObject.SetActive(Sein.PlayerAbilities.SpiritFlame.HasAbility);
        }
    }

    public void UpdateMovementStuff() {
        Sein.Controller.HandleJumping();
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref m_horizontalInputDelay);
        if (ar.Reading) {
            IsPlayingAnimation = false;
            LockMovementInput = false;
        }
    }

    public void OnRecieveDamage(Damage damage) {
        Sein.Mortality.DamageReciever.OnRecieveDamage(damage);
    }

    public SeinAnimationSpeedSettings AnimationSpeedSettings;

    public bool IgnoreControllerInput;

    public bool LockMovementInput;

    public AnimationCurve InputCurve;

    public SeinInputSettings InputSettings;

    public LayerMask RayTestLayerMask;

    public SeinCharacter Sein;

    public bool IsPlayingAnimation;

    public Action OnHorizontalInputPostCalculate = delegate { };

    private Transform m_transform;

    public Transform GetItemTransform;

    [SerializeField] [HideInInspector] private Component[] m_suspendables;

    private float m_horizontalInputDelay;
}

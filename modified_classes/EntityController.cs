using System;
using fsm;
using fsm.triggers;
using Game;
using UnityEngine;

public class EntityController : SaveSerialize, INearSeinReceiver, IDamageReciever {
    private SpriteEntity SpriteEntity => Entity as SpriteEntity;

    public void OnValidate() {
        Entity = transform.FindComponentUpwards<Entity>();
        Entity.Controller = this;
    }

    public new void Awake() {
        base.Awake();
        if (Entity == null) {
            OnValidate();
        }

        if (SpriteEntity && SpriteEntity.Animation) {
            SpriteEntity.Animation.Animator.OnAnimationEndEvent += OnAnimationEnd;
        }
    }

    public void FixedUpdate() {
        if (Entity.IsSuspended) {
            return;
        }

        if (transManager == null) {
            transManager = StateMachine.GetTransistionManager<OnFixedUpdate>();
        }

        if (transManager == null) {
            return;
        }

        var deltaTime = Time.deltaTime;
        if (Entity is Enemy) {
            deltaTime = RandomizerBonusSkill.TimeScale(deltaTime);
        }

        StateMachine.UpdateState(deltaTime);
        StateMachine.CurrentTrigger = null;
        transManager.Process(StateMachine);
    }

    public void OnAnimationEnd(TextureAnimation anim) {
        StateMachine.Trigger<OnAnimationOrTransitionEnded>();
        if (!SpriteEntity.Animation.Animator.IsTransitionPlaying) {
            StateMachine.Trigger<OnAnimationEnded>();
        }
    }

    public void OnCollisionEnter(Collision collision) {
        StateMachine.Trigger(new OnCollisionEnter(collision));
    }

    public void OnCollisionStay(Collision collision) {
        StateMachine.Trigger(new OnCollisionStay(collision));
    }

    public void OnCollisionExit(Collision collision) {
        StateMachine.Trigger(new OnCollisionExit(collision));
    }

    public void OnRecieveDamage(Damage damage) {
        if (OnReceiveDamage != null) {
            OnReceiveDamage(damage);
        }

        StateMachine.Trigger(new OnReceiveDamage(damage));
    }

    public void OnNearSeinEnter() {
        nearSein = true;
    }

    public void OnNearSeinExit() {
        nearSein = false;
    }

    public bool NearSein => nearSein && Characters.Sein.Controller.CanMove;

    public bool IsNearSein() {
        return NearSein;
    }

    public void OnSeinNearStay() {
        LastSeenSeinPosition = Characters.Sein.Position;
    }

    public Vector3 LastSeenSeinPosition { get; private set; }

    [ContextMenu("Current state class name")]
    public void ShowCurrentStateClassName() {
    }

    public override void Serialize(Archive ar) {
        StateMachine.Serialize(ar);
    }

    public Entity Entity;

    public StateMachine StateMachine = new StateMachine();

    public Action<Damage> OnReceiveDamage;

    private TransitionManager transManager;

    private bool nearSein;
}

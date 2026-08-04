using System.Collections;
using Game;
using UnityEngine;
using Input = Core.Input;

public class GetAbilityPedestal : SaveSerialize {
    public bool SeinInRange => !(Characters.Sein == null) && Vector3.Distance(transform.position, Characters.Sein.Position) < Radius;

    private void ChangeState(States state) {
        if (CurrentState == States.InRange) {
            ExitInRangeState();
        }

        CurrentState = state;
    }

    public void UpdateStates() {
        var currentState = CurrentState;

        if (currentState != States.Completed && RandomizerLocationManager.IsPickupCollected(MoonGuid)) {
            ChangeState(States.Completed);
            ActivatePedestalSequence.PerformInstantly(null);
            return;
        }

        if (currentState != States.OutOfRange) {
            if (currentState == States.InRange) {
                UpdateInRangeState();
            }
        } else {
            UpdateOutOfRange();
        }
    }

    private void UpdateOutOfRange() {
        if (SeinInRange) {
            ChangeState(States.InRange);
        }
    }

    private void ExitInRangeState() {
        if (message != null) {
            message.HideMessageScreen();
        }
    }

    public void UpdateInRangeState() {
        if (Characters.Sein.PlatformBehaviour.PlatformMovement.IsOnGround) {
            if (message == null && !SeinUI.DebugHideUI) {
                message = UI.Hints.Show(PressUpToActivatePedestalMessage, HintLayer.Gameplay, float.PositiveInfinity);
            }

            if (!Characters.Sein.IsSuspended && Characters.Sein.Controller.CanMove && Input.SpiritFlame.OnPressed) {
                Input.SpiritFlame.Used = true;
                ActivatePedestal();
                return;
            }
        }

        if (!SeinInRange) {
            ChangeState(States.OutOfRange);
        }
    }

    public void FixedUpdate() {
        UpdateStates();
    }

    public void ActivatePedestal() {
        StartCoroutine(MoveSeinToCenterSmoothly());
        if (Characters.Sein.Abilities.Carry && Characters.Sein.Abilities.Carry.CurrentCarryable != null) {
            Characters.Sein.Abilities.Carry.CurrentCarryable.Drop();
        }

        Characters.Sein.Mortality.Health.RestoreAllHealth();
        Characters.Sein.Energy.RestoreAllEnergy();
        Characters.Sein.Controller.PlayAnimation(GetAbilityAnimation);
        RandomizerLocationManager.GivePickup(MoonGuid);
        ChangeState(States.Completed);
        ActivatePedestalSequence.Perform(null);
        GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
    }

    public IEnumerator MoveSeinToCenterSmoothly() {
        var seinPlatformMovement = Characters.Sein.PlatformBehaviour.PlatformMovement;
        for (var i = 0; i < 10; i++) {
            seinPlatformMovement.PositionX = Mathf.Lerp(seinPlatformMovement.PositionX, transform.position.x, 0.2f);
            yield return new WaitForFixedUpdate();
        }

        seinPlatformMovement.PositionX = transform.position.x;
    }

    public override void Serialize(Archive ar) {
        if (ar.Reading) {
            var state = ar.Serialize(0);
            ChangeState((States)state);
        } else {
            ar.Serialize((int)CurrentState);
        }
    }

    public override void Awake() {
        base.Awake();
        transform = base.transform;
    }

    public States CurrentState;

    public AbilityType Ability;

    public PerformingAction ActivatePedestalSequence;

    public float ActivationDuration = 6f;

    public TextureAnimationWithTransitions GetAbilityAnimation;

    public Texture2D PressUpToActivatePedestal;

    public MessageProvider PressUpToActivatePedestalMessage;

    private MessageBox message;

    public float Radius = 1.5f;

    private new Transform transform;

    public enum States {
        OutOfRange,
        InRange,
        Completed,
    }
}

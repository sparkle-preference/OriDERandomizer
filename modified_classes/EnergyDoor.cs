using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using UnityEngine.Serialization;
using Input = Core.Input;

public class EnergyDoor : SaveSerialize {
    public void OnValidate() {
        Transform = transform;
    }

    public void Highlight() {
        if (OriTarget) {
            Characters.Ori.MoveOriToPosition(OriTarget.position, OriDuration);
        } else {
            Characters.Ori.MoveOriToPosition(Transform.position, OriDuration);
        }

        if (Characters.Sein.Abilities.SpiritFlame) {
            Characters.Sein.Abilities.SpiritFlame.AddLock("energyDoor");
        }

        Characters.Ori.GetComponent<Rigidbody>().velocity = Vector3.zero;
        Characters.Ori.EnableHoverWobbling = false;
        if (hint == null) {
            hint = UI.Hints.Show(HintMessage, HintLayer.HintZone);
        }

        if (OnOriEnterSoundProvider) {
            Sound.Play(OnOriEnterSoundProvider.GetSound(null), Transform.position, null);
        }
    }

    public void Unhighlight() {
        Characters.Ori.ChangeState(Ori.State.Hovering);
        Characters.Ori.EnableHoverWobbling = true;
        if (Characters.Sein.Abilities.SpiritFlame) {
            Characters.Sein.Abilities.SpiritFlame.RemoveLock("energyDoor");
        }

        if (hint) {
            hint.HideMessageScreen();
        }

        if (OnOriExitSoundProvider) {
            Sound.Play(OnOriExitSoundProvider.GetSound(null), Transform.position, null);
        }
    }

    public void RestoreOrbs() {
        if (AmountOfEnergyUsed > 0 && RestoreSoundProvider) {
            Sound.Play(RestoreSoundProvider.GetSound(null), Transform.position, null);
        }

        if (Characters.Sein) {
            Characters.Sein.Energy.Gain(AmountOfEnergyUsed);
        }

        AmountOfEnergyUsed = 0;
    }

    public void OnDisable() {
        if (CurrentState == State.Highlighted) {
            RestoreOrbs();
            Unhighlight();
        }
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref slotsPending);
        ar.Serialize(ref AmountOfEnergyUsed);
        ar.Serialize(ref slotsFilled);
        if (ar.Reading && CurrentState == State.Highlighted) {
            Unhighlight();
            CurrentState = State.Normal;
        }

        CurrentState = (State)ar.Serialize((int)CurrentState);
        if (ar.Reading && CurrentState == State.Highlighted) {
            RestoreOrbs();
            CurrentState = State.Normal;
        }
    }

    public float DistanceToSein => Vector3.Distance(Transform.position, Characters.Sein.Position);

    public bool OriHasTargets {
        get {
            var spiritFlameTargetting = Characters.Sein.Abilities.SpiritFlameTargetting;
            return spiritFlameTargetting && spiritFlameTargetting.ClosestAttackables.Count > 0;
        }
    }

    public bool SeinInRange => !OriHasTargets && DistanceToSein <= Radius;

    public void RegisterSlot(EnergyDoorSlot slot) {
        slots.Add(slot);
    }

    public void UpdateSlots() {
        foreach (var energyDoorSlot in slots) {
            energyDoorSlot.Refresh();
        }
    }

    public void FixedUpdate() {
        if (!Characters.Sein) {
            return;
        }

        var currentState = CurrentState;
        if (currentState != State.Normal) {
            if (currentState == State.Highlighted) {
                if (!SeinInRange) {
                    RestoreOrbs();
                    Unhighlight();
                    CurrentState = State.Normal;
                }

                if (!Characters.Sein.Controller.CanMove) {
                    RestoreOrbs();
                    Unhighlight();
                    CurrentState = State.Normal;
                    return;
                }

                if (Characters.Sein.Controller.CanMove && !Characters.Sein.IsSuspended && Input.SpiritFlame.OnPressed) {
                    if (Characters.Sein.Energy.Current < 1f && AmountOfEnergyRequired > AmountOfEnergyUsed) {
                        OnFailAction.Perform(null);
                        Characters.Sein.Energy.NotifyOutOfEnergy();
                    }

                    if (Characters.Sein.Energy.Current >= 1f && AmountOfEnergyUsed < AmountOfEnergyRequired) {
                        AmountOfEnergyUsed++;
                        Characters.Sein.Energy.Spend(1f);
                        UpdateSlots();
                        if (PlaceSlotSoundProvider) {
                            Sound.Play(PlaceSlotSoundProvider.GetSound(null), Transform.position, null);
                        }
                    }

                    if (AmountOfEnergyUsed == AmountOfEnergyRequired) {
                        BingoController.OnEnergyDoor();
                        OnOpenedAction.Perform(null);
                        Unhighlight();
                        CurrentState = State.Opened;
                        if (ActivateSoundProvider) {
                            Sound.Play(ActivateSoundProvider.GetSound(null), Transform.position, null);
                        }
                    }
                }
            }
        } else if (SeinInRange && !OriHasTargets && Characters.Sein.Controller.CanMove) {
            Highlight();
            CurrentState = State.Highlighted;
        }
    }

    public Transform OriTarget;

    [FormerlySerializedAs("m_transform")] [SerializeField] [HideInInspector] private Transform Transform;

    private int slotsPending;

    private int slotsFilled;

    public ActionMethod OnOpenedAction;

    public ActionMethod OnFailAction;

    public int AmountOfEnergyRequired;

    public int AmountOfEnergyUsed;

    public SoundProvider PlaceSlotSoundProvider;

    public SoundProvider ActivateSoundProvider;

    public SoundProvider RestoreSoundProvider;

    public SoundProvider OnOriEnterSoundProvider;

    public SoundProvider OnOriExitSoundProvider;

    public float OriDuration = 1f;

    public float Radius = 10f;

    public MessageProvider HintMessage;

    private MessageBox hint;

    public State CurrentState;

    private List<EnergyDoorSlot> slots = new List<EnergyDoorSlot>();

    public enum State {
        Normal,
        Highlighted,
        Opened,
    }
}

using Core;
using Game;
using UnityEngine;
using UnityEngine.Serialization;
using Input = Core.Input;

public class DoorWithSlots : SaveSerialize {
    public void OnValidate() {
        Transform = transform;
    }

    public override void Awake() {
        base.Awake();
        opensOnLeftSide = Transform.TransformPoint(Vector3.right).x < Transform.position.x;
    }

    public void Highlight() {
        if (OriTarget) {
            Characters.Ori.MoveOriToPosition(OriTarget.position, OriDuration);
        } else {
            Characters.Ori.MoveOriToPosition(Transform.position, OriDuration);
        }

        if (Characters.Sein.Abilities.SpiritFlame) {
            Characters.Sein.Abilities.SpiritFlame.AddLock("doorWithSlots");
        }

        Characters.Ori.GetComponent<Rigidbody>().velocity = Vector3.zero;
        Characters.Ori.EnableHoverWobbling = false;
        Characters.Ori.InsideDoor = true;
        if (hint == null) {
            hint = UI.Hints.Show(HintMessage, HintLayer.HintZone, 600f);
        }

        if (OnOriEnterSoundProvider) {
            Sound.Play(OnOriEnterSoundProvider.GetSound(null), Transform.position, null);
        }

        Randomizer.Keysanity.ApplyKeystoneCount(MoonGuid, NumberOfOrbsUsed);
    }

    public void Unhighlight() {
        Characters.Ori.ChangeState(Ori.State.Hovering);
        Characters.Ori.EnableHoverWobbling = true;
        Characters.Ori.InsideDoor = false;
        if (Characters.Sein.Abilities.SpiritFlame) {
            Characters.Sein.Abilities.SpiritFlame.RemoveLock("doorWithSlots");
        }

        if (hint) {
            hint.HideMessageScreen();
        }

        if (OnOriExitSoundProvider) {
            Sound.Play(OnOriExitSoundProvider.GetSound(null), Transform.position, null);
        }

        Randomizer.Keysanity.ResetKeystoneCount();
    }

    public void RestoreOrbs() {
        if (NumberOfOrbsUsed > 0 && RestoreLeafsSoundProvider) {
            Sound.Play(RestoreLeafsSoundProvider.GetSound(null), Transform.position, null);
        }

        Characters.Sein.Inventory.CollectKeystones(NumberOfOrbsUsed);
        NumberOfOrbsUsed = 0;
        Randomizer.Keysanity.ResetKeystoneCount();
    }

    public void OnDisable() {
        if (!Characters.Sein) {
            return;
        }

        if (CurrentState == State.Highlighted) {
            RestoreOrbs();
            Unhighlight();
        }
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref slotsPending);
        ar.Serialize(ref NumberOfOrbsUsed);
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

        if (openDoorSound) {
            openDoorSound.FadeOut(0.5f, true);
            UberPoolManager.Instance.RemoveOnDestroyed(openDoorSound.gameObject);
            openDoorSound = null;
        }

        if (ar.Reading && CurrentState == State.Opened) {
            checkItOpened = true;
        }
    }

    public float DistanceToSein => Vector3.Distance(Transform.position, Characters.Sein.Position);

    public bool OriHasTargets {
        get {
            var spiritFlameTargetting = Characters.Sein.Abilities.SpiritFlameTargetting;
            return spiritFlameTargetting && spiritFlameTargetting.ClosestAttackables.Count > 0;
        }
    }

    public bool SeinInRange => !OriHasTargets && DistanceToSein <= Radius && (Randomizer.OpenMode || ((!opensOnLeftSide || Transform.position.x >= Characters.Sein.Position.x) && (opensOnLeftSide || Transform.position.x <= Characters.Sein.Position.x)));

    public void FixedUpdate() {
        switch (CurrentState) {
            case State.Normal:
                if (SeinInRange && !OriHasTargets && Characters.Sein.Controller.CanMove) {
                    Highlight();
                    CurrentState = State.Highlighted;
                }

                break;
            case State.Highlighted:
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
                    if (Characters.Sein.Inventory.Keystones == 0 && NumberOfOrbsRequired > NumberOfOrbsUsed) {
                        OnFailAction.Perform(null);
                        UI.SeinUI.ShakeKeystones();
                        if (NotEnoughLeafsSoundProvider) {
                            Sound.Play(NotEnoughLeafsSoundProvider.GetSound(null), Transform.position, null);
                        }
                    }

                    if (Characters.Sein.Inventory.Keystones > 0 && NumberOfOrbsUsed < NumberOfOrbsRequired) {
                        NumberOfOrbsUsed++;
                        Characters.Sein.Inventory.SpendKeystones(1);
                        if (PlaceLeafSoundSoundProvider) {
                            Sound.Play(PlaceLeafSoundSoundProvider.GetSound(null), Transform.position, null);
                        }
                    }

                    if (NumberOfOrbsUsed == NumberOfOrbsRequired) {
                        OnOpenedAction.Perform(null);
                        Unhighlight();
                        BingoController.OnKSDoor();
                        RandomizerLocationManager.OpenDoorByGuid(MoonGuid);
                        CurrentState = State.Opened;
                        if (OpenDoorSoundProvider) {
                            openDoorSound = Sound.Play(OpenDoorSoundProvider.GetSound(null), Transform.position, delegate { openDoorSound = null; });
                            openDoorSound.PauseOnSuspend = true;
                        }
                    }
                }

                break;
            case State.Opened:
                if (checkItOpened) {
                    checkItOpened = false;
                    MakeSureItsAtEnd(transform.FindChild("doorPieces/doorLeft"));
                    MakeSureItsAtEnd(transform.FindChild("doorPieces/doorRight"));
                }

                break;
            default:
                return;
        }
    }

    private void MakeSureItsAtEnd(Transform c) {
        if (c == null) {
            return;
        }

        var component = c.GetComponent<LegacyTranslateAnimator>();
        if (component.CurrentTime <= 0f && component.Stopped) {
            component.StopAndSampleAtEnd();
        }
    }

    public Transform OriTarget;

    [FormerlySerializedAs("m_transform")] [SerializeField] [HideInInspector] private Transform Transform;

    private int slotsPending;

    private int slotsFilled;

    public ActionMethod OnOpenedAction;

    public ActionMethod OnFailAction;

    public int NumberOfOrbsRequired;

    public int NumberOfOrbsUsed;

    public SoundProvider PlaceLeafSoundSoundProvider;

    public SoundProvider NotEnoughLeafsSoundProvider;

    public SoundProvider OpenDoorSoundProvider;

    public SoundProvider RestoreLeafsSoundProvider;

    public SoundProvider OnOriEnterSoundProvider;

    public SoundProvider OnOriExitSoundProvider;

    public float OriDuration = 1f;

    public float Radius = 10f;

    public MessageProvider HintMessage;

    private MessageBox hint;

    private bool opensOnLeftSide;

    public State CurrentState;

    private bool checkItOpened;

    private SoundPlayer openDoorSound;

    public enum State {
        Normal,
        Highlighted,
        Opened,
    }
}

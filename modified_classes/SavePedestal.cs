using System.Collections;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SavePedestal : SaveSerialize {
    public bool IsInside => CurrentState == State.Highlighted;

    public override void Awake() {
        base.Awake();
        transform = base.transform;
        sceneTeleporter = GetComponent<SceneTeleporter>();
        All.Add(this);
    }

    public override void OnDestroy() {
        base.OnDestroy();
        All.Remove(this);
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref hasBeenUsedBefore);
    }

    private bool CanTeleport => sceneTeleporter && TeleporterController.CanTeleport(sceneTeleporter.Identifier);

    public void Highlight() {
        if (OriTarget) {
            Characters.Ori.MoveOriToPosition(OriTarget.position, OriDuration);
        }

        if (Characters.Sein.Abilities.SpiritFlame) {
            Characters.Sein.Abilities.SpiritFlame.AddLock("savePedestal");
        }

        Characters.Ori.GetComponent<Rigidbody>().velocity = Vector3.zero;
        Characters.Ori.EnableHoverWobbling = false;
        if (OriEnterAction) {
            OriEnterAction.Perform(null);
        }

        if (hint == null) {
            hint = UI.Hints.Show(SaveAndTeleportHintMessage, HintLayer.HintZone);
        }

        if (OnOriEnter) {
            Sound.Play(OnOriEnter.GetSound(null), ((Component)this).transform.position, null);
        }

        if (sceneTeleporter) {
            TeleporterController.Activate(sceneTeleporter.Identifier);
            BingoController.OnPedestalTouch(sceneTeleporter.Identifier);
        }
    }

    public void Unhighlight() {
        used = false;
        Characters.Ori.ChangeState(Ori.State.Hovering);
        Characters.Ori.EnableHoverWobbling = true;
        if (Characters.Sein.Abilities.SpiritFlame) {
            Characters.Sein.Abilities.SpiritFlame.RemoveLock("savePedestal");
        }

        if (OriExitAction) {
            OriExitAction.Perform(null);
        }

        if (hint) {
            hint.HideMessageScreen();
        }

        if (OnOriExit) {
            Sound.Play(OnOriExit.GetSound(null), ((Component)this).transform.position, null);
        }
    }

    public bool OriHasTargets {
        get {
            var spiritFlameTargetting = Characters.Sein.Abilities.SpiritFlameTargetting;
            return spiritFlameTargetting && spiritFlameTargetting.ClosestAttackables.Count > 0;
        }
    }

    public float DistanceToSein => Vector3.Distance(transform.position, Characters.Sein.Position);

    public void FixedUpdate() {
        if (Characters.Sein == null) {
            return;
        }

        if (Characters.Sein.IsSuspended) {
            return;
        }

        var currentState = CurrentState;
        if (currentState != State.Normal) {
            if (currentState == State.Highlighted) {
                if ((!Characters.Sein.Controller.IsPlayingAnimation && DistanceToSein > Radius) || OriHasTargets) {
                    Unhighlight();
                    CurrentState = State.Normal;
                }

                if (Characters.Sein.Controller.CanMove && Characters.Sein.PlatformBehaviour.PlatformMovement.IsOnGround) {
                    if (Input.SpiritFlame.OnPressed && !used) {
                        SaveOnPedestal();
                        return;
                    }

                    if (Input.SoulFlame.OnPressedNotUsed && !Input.Cancel.Used) {
                        if (hint) {
                            hint.HideMessageScreen();
                        }

                        Input.SoulFlame.Used = true;
                        UI.Menu.ShowSkillTree();
                        return;
                    }

                    if (Input.SpiritFlame.OnPressed && used) {
                        if (OnSaveSecondTime) {
                            Sound.Play(OnSaveSecondTime.GetSound(null), ((Component)this).transform.position, null);
                        }
                    } else if (Input.Bash.OnPressed && WorldMapUI.IsReady) {
                        if (CanTeleport) {
                            TeleportOnPedestal();
                            return;
                        }

                        UI.Hints.Show(CantTeleportMessage, HintLayer.Gameplay, 2f);
                    }
                }
            }
        } else if (DistanceToSein < Radius && !OriHasTargets) {
            Highlight();
            CurrentState = State.Highlighted;
        }
    }

    private void TeleportOnPedestal() {
        if (hint) {
            hint.HideMessageScreen();
        }

        MarkAsUsed();
        Characters.Sein.PlatformBehaviour.PlatformMovement.PositionX = ((Component)this).transform.position.x;
        TeleporterController.Show(sceneTeleporter.Identifier);
    }

    public void OnBeginTeleporting() {
        if (TeleportEffect) {
            TeleportEffect.gameObject.SetActive(true);
            TeleportEffect.Initialize();
            TeleportEffect.AnimatorDriver.Restart();
        }
    }

    public void OnFinishedTeleporting() {
        if (TeleportEffect) {
            TeleportEffect.gameObject.SetActive(false);
        }
    }

    public void MarkAsUsed() {
        if (!hasBeenUsedBefore) {
            hasBeenUsedBefore = true;
            AchievementsLogic.Instance.OnSavePedestalUsedFirstTime();
        }
    }

    private void SaveOnPedestal() {
        if (hint) {
            hint.HideMessageScreen();
        }

        used = true;
        MarkAsUsed();
        RandomizerStatsManager.OnSave();
        if (Characters.Sein.Abilities.Carry && Characters.Sein.Abilities.Carry.CurrentCarryable != null) {
            Characters.Sein.Abilities.Carry.CurrentCarryable.Drop();
        }

        if (OnOpenedAction) {
            OnOpenedAction.Perform(null);
        }

        StartCoroutine(MoveSeinToCenterSmoothly());
    }

    public IEnumerator MoveSeinToCenterSmoothly() {
        var seinPlatformMovement = Characters.Sein.PlatformBehaviour.PlatformMovement;
        int num;
        for (var i = 0; i < 10; i = num + 1) {
            seinPlatformMovement.PositionX = Mathf.Lerp(seinPlatformMovement.PositionX, ((Component)this).transform.position.x, 0.2f);
            yield return new WaitForFixedUpdate();
            num = i;
        }

        seinPlatformMovement.PositionX = ((Component)this).transform.position.x;
    }

    public static List<SavePedestal> All = new List<SavePedestal>();

    public Transform OriTarget;

    public float Radius = 2f;

    public float OriDuration = 1f;

    private new Transform transform;

    private MessageBox hint;

    public MessageProvider CantTeleportMessage;

    public MessageProvider SaveAndTeleportHintMessage;

    public SoundProvider OnOriEnter;

    public SoundProvider OnOriExit;

    public SoundProvider OnSaveSecondTime;

    private bool hasBeenUsedBefore;

    private SceneTeleporter sceneTeleporter;

    public TimelineSequence TeleportEffect;

    public ActionMethod OriEnterAction;

    public ActionMethod OriExitAction;

    public ActionMethod OnOpenedAction;

    private bool used;

    public State CurrentState;

    public enum State {
        Normal,
        Highlighted,
    }
}

using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinDoorHandler : SaveSerialize, ISeinReceiver {
    public bool IsOverlappingDoor { get; private set; }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
    }

    public void OnDoorOverlap(Door door) {
        if (m_enterDoorHint == null) {
            if (Characters.Sein.Controller.CanMove) {
                m_enterDoorHint = UI.Hints.Show(!door.OverrideEnterDoorMessage ? EnterDoorMessage : door.OverrideEnterDoorMessage, HintLayer.Gameplay, 1f);
            }
        } else {
            m_enterDoorHint.Visibility.ResetWaitDuration();
        }

        m_isOverlappingDoor = true;
        if (Sein.Controller.CanMove && Input.Up.OnPressed && Sein.PlatformBehaviour.PlatformMovement.IsOnGround && !Sein.Controller.IsBashing && !UI.MainMenuVisible) {
            EnterIntoDoor(door);
        }
    }

    public void EnterIntoDoor(Door door) {
        if (m_enterDoorHint) {
            m_enterDoorHint.HideMessageScreen();
        }

        m_createCheckpoint = door.CreateCheckpoint;
        m_targetDoor = null;
        foreach (var sceneManagerScene in Scenes.Manager.ActiveScenes) {
            if (sceneManagerScene.SceneRoot) {
                foreach (var door2 in sceneManagerScene.SceneRoot.SceneRootData.Doors) {
                    if (door2 != null && door2.name == door.OtherDoorName && door2 != door) {
                        m_targetDoor = door2;
                    }
                }
            }
        }

        if (m_targetDoor == null) {
            return;
        }

        var gameObject = (GameObject)InstantiateUtility.Instantiate(EnterDoorAnimationPrefab);
        gameObject.transform.position = Sein.Position;
        if (Characters.Sein.Controller.FaceLeft) {
            gameObject.transform.localScale = Vector3.Scale(new Vector3(-1f, 1f, 1f), gameObject.transform.localScale);
        }

        if (door.EnterDoorAction) {
            door.EnterDoorAction.Perform(null);
        }

        Utility.DisableLate(Sein);
        UI.Fader.Fade(0.5f, 0.05f, 0.2f, OnFadedToBlack, null);
    }

    public void OnFadedToBlack() {
        var position = Sein.Position;
        if (m_targetDoor) {
            position = m_targetDoor.transform.position;
        }

        if (Randomizer.Entrance) {
            Randomizer.EnterDoor(Characters.Sein.Position);
        } else {
            Sein.Position = position;
        }

        CameraPivotZone.InstantUpdate();
        Scenes.Manager.UpdatePosition();
        Scenes.Manager.UnloadScenesAtPosition(true);
        Scenes.Manager.EnableDisabledScenesAtPosition();
        Sein.gameObject.SetActive(true);
        UI.Cameras.Current.MoveCameraToTargetInstantly();
        Sein.PlatformBehaviour.PlatformMovement.PlaceOnGround(0.5f, 0f);
        UI.Cameras.Current.MoveCameraToTargetInstantly();
        if (Characters.Ori) {
            Characters.Ori.MoveOriBackToPlayer();
        }

        if (m_createCheckpoint) {
            GameController.Instance.CreateCheckpoint();
            GameController.Instance.PerformSaveGameSequence();
        }

        LateStartHook.AddLateStartMethod(OnGoneThroughDoor);
    }

    public void OnGoneThroughDoor() {
        if (m_targetDoor != null && m_targetDoor.ComeOutOfDoorAction) {
            m_targetDoor.ComeOutOfDoorAction.Perform(null);
        }

        m_targetDoor = null;
        CameraFrustumOptimizer.ForceUpdate();
    }

    public void FixedUpdate() {
        IsOverlappingDoor = m_isOverlappingDoor;
        m_isOverlappingDoor = false;
        var isSuspended = Sein.IsSuspended;
    }

    public override void Serialize(Archive ar) {
    }

    public SeinCharacter Sein;

    public GameObject EnterDoorAnimationPrefab;

    public MessageProvider EnterDoorMessage;

    private MessageBox m_enterDoorHint;

    private bool m_createCheckpoint;

    private bool m_isOverlappingDoor;

    private Door m_targetDoor;
}

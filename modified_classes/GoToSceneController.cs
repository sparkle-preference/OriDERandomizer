using System;
using Core;
using Game;
using UnityEngine;

public class GoToSceneController : MonoBehaviour {
    public ScenesManager ScenesManager {
        get { return Scenes.Manager; }
    }

    public static bool CheckStartInScene(MoonGuid guid) {
        return GoToSceneController.Instance == null || GoToSceneController.Instance.StartInScene == guid || GoToSceneController.Instance.StartInScene == MoonGuid.Empty;
    }

    public void Awake() {
        GoToSceneController.Instance = this;
    }

    public void OnDestroy() {
        if (GoToSceneController.Instance == this) {
            GoToSceneController.Instance = null;
        }
    }

    private void GoToScene(MoonGuid sceneGuid, Vector3 position, string sceneName, Action onComplete, bool createCheckpoint, bool async) {
        this.StartInScene = sceneGuid;
        if (sceneName == "titleScreenSwallowsNest") {
            GameStateMachine.Instance.SetToStartScreen();
        }

        this.m_onCompleteLoad = onComplete;
        this.m_position = position;
        this.ScenesManager.SetTargetPositions(this.m_position);
        InstantLoadScenesController.Instance.LoadScenesAtPosition(null, async);
        this.m_createCheckpointLater = createCheckpoint;
        this.m_useAfterSceneLoad = true;
        this.ScenesManager.AllowUnloadingOnScenes(position);
    }

    private void FinishGoingToPositionImmediately() {
        UI.Cameras.Current.MoveCameraToTargetInstantly(false);
        this.ScenesManager.UnloadScenesAtPosition(true);
        this.ScenesManager.AutoLoadingUnloading = true;
        if (this.m_onCompleteImmediateLoad != null) {
            this.m_onCompleteImmediateLoad();
            this.m_onCompleteImmediateLoad = null;
        }

        UI.Cameras.Current.Controller.UpdateCamera();
    }

    public void OnScenesEnabled() {
        if (this.m_useAfterSceneLoad) {
            this.m_useAfterSceneLoad = false;
            this.CompleteGoingToAScene();
        }
    }

    public void CompleteGoingToAScene() {
        if (Characters.Current != null) {
            Characters.Current.Position = this.m_position;
            Characters.Current.PlaceOnGround();
        }

        UI.Cameras.Current.CameraTarget.SetTargetPosition(this.m_position);
        UI.Cameras.Current.Controller.PuppetController.Reset();
        UI.Cameras.Current.GoToChaseMode();
        UI.Cameras.Current.MoveCameraToTargetInstantly(false);
        UI.Cameras.Current.OffsetController.UpdateOffset(true);
        UI.Cameras.Current.MoveCameraToTargetInstantly(false);
        if (Characters.Ori) {
            Characters.Ori.MoveOriBackToPlayer();
        }
    }

    public void OnInstantLoadScenesControllerCompletedLoading() {
        if (this.m_onCompleteLoad != null) {
            this.m_onCompleteLoad();
            this.m_onCompleteLoad = null;
        }

        UI.Cameras.Current.Controller.UpdateCamera();
        if (this.m_createCheckpointLater) {
            this.m_createCheckpointLater = false;
            GameController.Instance.CreateCheckpoint();
            GameController.Instance.SaveGameController.PerformSave();
            GameController.Instance.PerformSaveGameSequence();
        }
    }

    public void OnScenesManagerFixedUpdate() {
        if (this.m_isMovingImmediately) {
            this.m_isMovingImmediately = false;
            this.ScenesManager.SetTargetPositions(this.m_position);
            this.ScenesManager.AutoLoadingUnloading = false;
            this.ScenesManager.EnableDisabledScenesAtPosition();
            this.CompleteGoingToAScene();
            LateStartHook.AddLateStartMethod(new Action(this.FinishGoingToPositionImmediately));
        }
    }

    public void GoToScene(SceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint) {
        this.GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.SeinPlaceholderPosition, sceneMetaData.name, onComplete, createCheckpoint, false);
    }

    public void GoToScene(RuntimeSceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint) {
        this.GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.PlaceholderPosition, sceneMetaData.Scene, onComplete, createCheckpoint, false);
    }

    public void GoToSceneAsync(SceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint) {
        this.GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.SeinPlaceholderPosition, sceneMetaData.name, onComplete, createCheckpoint, true);
    }

    public void GoToSceneAsync(RuntimeSceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint) {
        this.GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.PlaceholderPosition, sceneMetaData.Scene, onComplete, createCheckpoint, true);
    }

    public void GoToSceneImmediately(SceneMetaData scene, Action onComplete) {
        this.StartInScene = scene.SceneMoonGuid;
        this.m_position = scene.SeinPlaceholderPosition;
        this.m_onCompleteImmediateLoad = onComplete;
        this.m_isMovingImmediately = true;
    }

    public void GoToScene(string path) {
        RuntimeSceneMetaData sceneInformation = Scenes.Manager.GetSceneInformation(path);
        if (sceneInformation == null) {
            Randomizer.LogError("Bad scene path: " + path);
        } else {
            this.GoToScene(sceneInformation, null, true);
        }
    }

    public static GoToSceneController Instance;

    public MoonGuid StartInScene;

    private Vector3 m_position;

    private bool m_useAfterSceneLoad;

    private bool m_createCheckpointLater;

    private Action m_onCompleteLoad;

    private Action m_onCompleteImmediateLoad;

    private bool m_isMovingImmediately;
}

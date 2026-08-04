using System;
using Core;
using Game;
using UnityEngine;

public class GoToSceneController : MonoBehaviour {
    public ScenesManager ScenesManager => Scenes.Manager;

    public static bool CheckStartInScene(MoonGuid guid) {
        return Instance == null || Instance.StartInScene == guid || Instance.StartInScene == MoonGuid.Empty;
    }

    public void Awake() {
        Instance = this;
    }

    public void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    private void GoToScene(MoonGuid sceneGuid, Vector3 position, string sceneName, Action onComplete, bool createCheckpoint, bool async) {
        StartInScene = sceneGuid;
        if (sceneName == "titleScreenSwallowsNest") {
            GameStateMachine.Instance.SetToStartScreen();
        }

        onCompleteLoad = onComplete;
        this.position = position;
        ScenesManager.SetTargetPositions(this.position);
        InstantLoadScenesController.Instance.LoadScenesAtPosition(null, async);
        createCheckpointLater = createCheckpoint;
        useAfterSceneLoad = true;
        ScenesManager.AllowUnloadingOnScenes(position);
    }

    private void FinishGoingToPositionImmediately() {
        UI.Cameras.Current.MoveCameraToTargetInstantly(false);
        ScenesManager.UnloadScenesAtPosition(true);
        ScenesManager.AutoLoadingUnloading = true;
        if (onCompleteImmediateLoad != null) {
            onCompleteImmediateLoad();
            onCompleteImmediateLoad = null;
        }

        UI.Cameras.Current.Controller.UpdateCamera();
    }

    public void OnScenesEnabled() {
        if (useAfterSceneLoad) {
            useAfterSceneLoad = false;
            CompleteGoingToAScene();
        }
    }

    public void CompleteGoingToAScene() {
        if (Characters.Current != null) {
            Characters.Current.Position = position;
            Characters.Current.PlaceOnGround();
        }

        UI.Cameras.Current.CameraTarget.SetTargetPosition(position);
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
        if (onCompleteLoad != null) {
            onCompleteLoad();
            onCompleteLoad = null;
        }

        UI.Cameras.Current.Controller.UpdateCamera();
        if (createCheckpointLater) {
            createCheckpointLater = false;
            GameController.Instance.CreateCheckpoint();
            GameController.Instance.SaveGameController.PerformSave();
            GameController.Instance.PerformSaveGameSequence();
        }
    }

    public void OnScenesManagerFixedUpdate() {
        if (isMovingImmediately) {
            isMovingImmediately = false;
            ScenesManager.SetTargetPositions(position);
            ScenesManager.AutoLoadingUnloading = false;
            ScenesManager.EnableDisabledScenesAtPosition();
            CompleteGoingToAScene();
            LateStartHook.AddLateStartMethod(FinishGoingToPositionImmediately);
        }
    }

    public void GoToScene(SceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint) {
        GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.SeinPlaceholderPosition, sceneMetaData.name, onComplete, createCheckpoint, false);
    }

    public void GoToScene(RuntimeSceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint) {
        GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.PlaceholderPosition, sceneMetaData.Scene, onComplete, createCheckpoint, false);
    }

    public void GoToSceneAsync(SceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint) {
        GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.SeinPlaceholderPosition, sceneMetaData.name, onComplete, createCheckpoint, true);
    }

    public void GoToSceneAsync(RuntimeSceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint) {
        GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.PlaceholderPosition, sceneMetaData.Scene, onComplete, createCheckpoint, true);
    }

    public void GoToSceneImmediately(SceneMetaData scene, Action onComplete) {
        StartInScene = scene.SceneMoonGuid;
        position = scene.SeinPlaceholderPosition;
        onCompleteImmediateLoad = onComplete;
        isMovingImmediately = true;
    }

    public void GoToScene(string path) {
        var sceneInformation = Scenes.Manager.GetSceneInformation(path);
        if (sceneInformation == null) {
            Randomizer.LogError("Bad scene path: " + path);
        } else {
            GoToScene(sceneInformation, null, true);
        }
    }

    public static GoToSceneController Instance;

    public MoonGuid StartInScene;

    private Vector3 position;

    private bool useAfterSceneLoad;

    private bool createCheckpointLater;

    private Action onCompleteLoad;

    private Action onCompleteImmediateLoad;

    private bool isMovingImmediately;
}

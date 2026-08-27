using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;

public class ScenesManager : SaveSerialize {
    public bool ScenesNotLoadedOnTime { get; private set; }

    public float PaddingWidthExtension { get; set; }

    public RuntimeSceneMetaData CurrentScene {
        get {
            for (int i = 0; i < this.ActiveScenes.Count; i++) {
                SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
                if (!sceneManagerScene.MetaData.DependantScene) {
                    if (sceneManagerScene.IsVisible && UI.Cameras.Current && sceneManagerScene.MetaData.IsInsideSceneBounds(this.CurrentCameraTargetPosition)) {
                        return this.ActiveScenes[i].MetaData;
                    }
                }
            }
            return null;
        }
    }

    public SceneManagerScene CurrentSceneManagerScene {
        get {
            for (int i = 0; i < this.ActiveScenes.Count; i++) {
                if (!this.ActiveScenes[i].MetaData.DependantScene) {
                    if (UI.Cameras.Current && this.ActiveScenes[i].MetaData.IsInsideSceneBounds(this.CurrentCameraTargetPosition)) {
                        return this.ActiveScenes[i];
                    }
                }
            }
            return null;
        }
    }

    public Vector2 CurrentCameraTargetPosition { get; private set; }

    public Vector2 CurrentCameraTargetPositionExtrapolated { get; private set; }

    public bool SceneVisibleAtPosition(Vector3 position) {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            if (!this.ActiveScenes[i].MetaData.DependantScene) {
                if (this.ActiveScenes[i].IsVisible && this.ActiveScenes[i].MetaData.IsInsideSceneBounds(position)) {
                    return true;
                }
            }
        }
        return false;
    }

    public bool SceneIsEnabled(SceneMetaData sceneMetaData) {
        return this.SceneIsEnabled(sceneMetaData.SceneMoonGuid);
    }

    public bool SceneIsEnabled(MoonGuid sceneMoonGuid) {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (sceneManagerScene.MetaData.SceneMoonGuid == sceneMoonGuid && sceneManagerScene.CurrentState == SceneManagerScene.State.Loaded) {
                return true;
            }
        }
        return false;
    }

    public void SetTargetPositions(Vector3 target) {
        this.CurrentCameraTargetPosition = target;
        this.CurrentCameraTargetPositionExtrapolated = target;
        this.m_cameraPositions.Clear();
    }

    public bool IsLoadingScenes {
        get {
            for (int i = 0; i < this.ActiveScenes.Count; i++) {
                SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
                if (sceneManagerScene.CurrentState == SceneManagerScene.State.Loading) {
                    return true;
                }
            }
            return false;
        }
    }

    public Rect GetClampedRect(Vector3 position) {
        Rect rect = default(Rect);
        Rect rect2 = rect;
        rect2.width = 48f;
        rect2.height = 48f;
        rect2.center = position;
        rect = rect2;
        Rect rect3;
        if (this.GetSceneBoundaryAtPosition(rect.center, out rect3)) {
            rect.xMin = Mathf.Max(rect.xMin, rect3.xMin + 0.1f);
            rect.yMin = Mathf.Max(rect.yMin, rect3.yMin + 0.1f);
            rect.xMax = Mathf.Min(rect.xMax, rect3.xMax - 0.1f);
            rect.yMax = Mathf.Min(rect.yMax, rect3.yMax - 0.1f);
        }
        return rect;
    }

    public bool IsLoadingScene(Vector3 position) {
        Rect clampedRect = this.GetClampedRect(position);
        Rect rect;
        this.GetSceneBoundaryAtPosition(position, out rect);
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (!sceneManagerScene.MetaData.DependantScene) {
                if (sceneManagerScene.MetaData.IsInsideSceneBounds(clampedRect) || sceneManagerScene.MetaData.IsInsideScenePaddingBounds(clampedRect, rect)) {
                    if (!sceneManagerScene.IsLoadingComplete) {
                        this.m_scenes.Clear();
                        return true;
                    }
                    foreach (MoonGuid moonGuid in sceneManagerScene.MetaData.IncludedScenes) {
                        this.m_scenes.Add(moonGuid);
                    }
                }
            }
        }
        for (int j = 0; j < this.ActiveScenes.Count; j++) {
            SceneManagerScene sceneManagerScene2 = this.ActiveScenes[j];
            if (sceneManagerScene2.MetaData.DependantScene && this.m_scenes.Contains(sceneManagerScene2.MetaData.SceneMoonGuid) && !sceneManagerScene2.IsLoadingComplete) {
                this.m_scenes.Clear();
                return true;
            }
        }
        this.m_scenes.Clear();
        return false;
    }

    public bool PositionInsideSceneStillLoading(Vector3 position) {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (!sceneManagerScene.MetaData.DependantScene) {
                if (sceneManagerScene.CurrentState == SceneManagerScene.State.Loading && sceneManagerScene.MetaData.IsInsideSceneBounds(position)) {
                    return true;
                }
            }
        }
        return false;
    }

    public bool ResourcesNeedUnloading {
        get {
            return this.m_resourcesNeedUnloading != 0;
        }
    }

    public void DrawScenesManagerDebugData() {
        GUILayout.BeginArea(new Rect(8f, 16f, 550f, 500f));
        foreach (SceneManagerScene sceneManagerScene in this.ActiveScenes) {
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            switch (sceneManagerScene.CurrentState) {
                case SceneManagerScene.State.Disabling:
                    GUI.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                    break;
                case SceneManagerScene.State.Disabled:
                    GUI.color = new Color(0.2f, 0.2f, 0.5f, 1f);
                    break;
                case SceneManagerScene.State.Loading:
                    GUI.color = Color.yellow;
                    break;
                case SceneManagerScene.State.LoadingCancelled:
                    GUI.color = Color.red;
                    break;
                case SceneManagerScene.State.Loaded:
                    GUI.color = Color.white;
                    break;
            }
            GUILayout.Label(sceneManagerScene.MetaData.Scene, new GUILayoutOption[0]);
            GUILayout.Label("Loading Time: " + sceneManagerScene.LoadingTime, new GUILayoutOption[0]);
            if (sceneManagerScene.KeepLoadedForCheckpoint) {
                GUILayout.Label("(checkpoint)", new GUILayoutOption[0]);
            }
            if (sceneManagerScene.PreventUnloading) {
                GUILayout.Label("(preloaded)", new GUILayoutOption[0]);
            }
            GUILayout.EndHorizontal();
        }
        GUI.color = Color.white;
        GUILayout.EndArea();
    }

    public RuntimeSceneMetaData GetSceneInformation(string sceneName) {
        for (int i = 0; i < this.AllScenes.Count; i++) {
            RuntimeSceneMetaData runtimeSceneMetaData = this.AllScenes[i];
            if (runtimeSceneMetaData.Scene == sceneName) {
                return runtimeSceneMetaData;
            }
        }
        return null;
    }

    public SceneManagerScene GetSceneManagerScene(string sceneName) {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            if (this.ActiveScenes[i].MetaData.Scene == sceneName) {
                return this.ActiveScenes[i];
            }
        }
        return null;
    }

    public override void Awake() {
        base.Awake();
        Scenes.Manager = this;
        this.GenerateGuidToRuntimeSceneMetaDataDictionary();
        GameController.Instance.GameScheduler.OnPassThroughScrollLock.Add(new Action(this.OnPassThroughScrollLock));
        global::Game.Checkpoint.Events.OnPostCreate.Add(new Action(this.OnCreateCheckpoint));
        Events.Scheduler.OnGameReset.Add(new Action(this.OnGameReset));
        AspectRatioManager.OnAspectChanged.Add(new Action(this.OnAspectRatioChanged));
    }

    public void OnGameReset() {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            sceneManagerScene.KeepLoadedForCheckpoint = false;
            sceneManagerScene.PreventUnloading = false;
        }
    }

    private void GenerateGuidToRuntimeSceneMetaDataDictionary() {
        for (int i = 0; i < this.AllScenes.Count; i++) {
            RuntimeSceneMetaData runtimeSceneMetaData = this.AllScenes[i];
            this.m_guidToRuntimeSceneMetaDatas[runtimeSceneMetaData.SceneMoonGuid] = runtimeSceneMetaData;
        }
    }

    public override void OnDestroy() {
        GameController.Instance.GameScheduler.OnPassThroughScrollLock.Remove(new Action(this.OnPassThroughScrollLock));
        global::Game.Checkpoint.Events.OnPostCreate.Remove(new Action(this.OnCreateCheckpoint));
        Events.Scheduler.OnGameReset.Remove(new Action(this.OnGameReset));
        AspectRatioManager.OnAspectChanged.Remove(new Action(this.OnAspectRatioChanged));
    }

    public void OnAspectRatioChanged() {
        this.UpdatePaddingWidthExtension();
    }

    public override void Serialize(Archive ar) {
        this.CurrentCameraTargetPosition = ar.Serialize(this.CurrentCameraTargetPosition);
        if (ar.Reading) {
            this.CurrentCameraTargetPositionExtrapolated = this.CurrentCameraTargetPosition;
        }
    }

    public void MarkLoadingScenesAsCancel() {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (!sceneManagerScene.MetaData.DependantScene && sceneManagerScene.CurrentState == SceneManagerScene.State.Loading) {
                sceneManagerScene.ChangeState(SceneManagerScene.State.LoadingCancelled);
                if (this.CancelScene(sceneManagerScene)) {
                    i--;
                }
            }
        }
    }

    public void OnCreateCheckpoint() {
        this.MarkActiveScenesAsKeepLoaded();
    }

    public void MarkActiveScenesAsKeepLoaded() {
        Rect rect = default(Rect);
        Rect rect2 = rect;
        rect2.width = 48f;
        rect2.height = 48f;
        rect2.center = this.CurrentCameraTargetPosition;
        rect = rect2;
        Rect rect3;
        if (this.GetSceneBoundaryAtPosition(rect.center, out rect3)) {
            rect.xMin = Mathf.Max(rect.xMin, rect3.xMin + 0.1f);
            rect.yMin = Mathf.Max(rect.yMin, rect3.yMin + 0.1f);
            rect.xMax = Mathf.Max(rect.xMax, rect3.xMax - 0.1f);
            rect.yMax = Mathf.Max(rect.yMin, rect3.yMax - 0.1f);
        } else {
            rect.width = 0f;
            rect.height = 0f;
        }
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (sceneManagerScene.MetaData.IsInsideSceneBounds(rect)) {
                sceneManagerScene.KeepLoadedForCheckpoint = true;
            } else if (sceneManagerScene.MetaData.IsInsideSceneLoadingZone(rect)) {
                sceneManagerScene.KeepLoadedForCheckpoint = true;
            } else if (sceneManagerScene.MetaData.IsInsideScenePaddingBounds(rect)) {
                sceneManagerScene.KeepLoadedForCheckpoint = true;
            } else {
                sceneManagerScene.KeepLoadedForCheckpoint = false;
            }
        }
    }

    public void ClearKeepLoadedForCheckpoint() {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            sceneManagerScene.KeepLoadedForCheckpoint = false;
        }
    }

    public bool HasReportedScenesLoading { get; set; }

    public void ReportScenesThatAreStillLoading() {
        this.HasReportedScenesLoading = true;
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (sceneManagerScene.CurrentState == SceneManagerScene.State.Loading) {
            }
        }
    }

    private void DetectScenesNotLoadedInTime() {
        if (this.ScenesNotLoadedOnTime) {
            if (!this.AnyMissingScenesAtCurrentPosition()) {
                this.ScenesNotLoadedOnTime = false;
            }
        } else if (this.AnyMissingScenesAtCurrentPosition()) {
            this.ScenesNotLoadedOnTime = true;
        }
    }

    private string SceneToLoad {
        get {
            if (this.m_scenesToLoad.Count > 0) {
                return this.m_scenesToLoad[0];
            }
            if (this.m_backgroundsToLoad.Count > 0) {
                return this.m_backgroundsToLoad[0];
            }
            return string.Empty;
        }
    }

    private string PopSceneToLoad() {
        if (this.m_scenesToLoad.Count > 0) {
            string text = this.m_scenesToLoad[0];
            this.m_scenesToLoad.Remove(text);
            return text;
        }
        if (this.m_backgroundsToLoad.Count > 0) {
            string text2 = this.m_backgroundsToLoad[0];
            this.m_backgroundsToLoad.Remove(text2);
            return text2;
        }
        return string.Empty;
    }

    private void UpdateLoadingScenes() {
        if (this.m_currentLoad != null && this.m_currentLoad.isDone) {
            this.m_currentLoad = null;
        }
        if (this.m_currentLoad == null && this.SceneToLoad != string.Empty && this.CanLoadScenes) {
            this.m_currentLoad = Application.LoadLevelAdditiveAsync(this.PopSceneToLoad());
        }
    }

    public void TestForFallOutOfWorld() {
        if (this.m_testDelayTime <= 0f) {
            this.m_testDelayTime = 1f;
            if (!this.IsInsideASceneBoundary(this.CurrentCameraTargetPosition)) {
                GameController.Instance.RestoreCheckpoint(null);
            }
        }
        this.m_testDelayTime -= Time.deltaTime;
    }

    private IEnumerator ShowFellOutOfWorldMessage() {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        MessageBox message = UI.MessageController.ShowHintMessage(Scenes.Manager.FellOutOfWorldMessage, OnScreenPositions.TopCenter, 3f);
        yield break;
    }

    public void ForceTestForOutOfWorld() {
        this.m_testDelayTime = 0f;
        this.TestForFallOutOfWorld();
    }

    public void FixedUpdate() {
        if (UI.Cameras.Current.ScrollLockIsFadingOut) {
            return;
        }
        if (this.AutoLoadingUnloading) {
            this.DetectScenesNotLoadedInTime();
            this.UpdateScenes();
            this.UpdateExtrapolatedPosition();
            this.EnableDisabledScenesAtPosition(true);
            this.TestForFallOutOfWorld();
        }
    }

    private void UpdatePaddingWidthExtension() {
        GameplayCamera current = UI.Cameras.Current;
        if (current) {
            float cameraWidthWorldUnits = UI.Cameras.Current.CameraWidthWorldUnits;
            float num = cameraWidthWorldUnits - cameraWidthWorldUnits * 1.7777778f / AspectRatioManager.AspectRatio;
            this.PaddingWidthExtension = num * 0.5f;
        }
    }

    public void UpdatePosition() {
        this.m_cameraPositions.Clear();
        for (int i = 0; i < UI.Cameras.Manager.Cameras.Count; i++) {
            CameraController cameraController = UI.Cameras.Manager.Cameras[i];
            if (cameraController.PuppetController.Tween > 0.5f) {
                this.m_cameraPositions.Add(cameraController.Position);
            }
        }
        if (UI.Cameras.Current.Target) {
            if (!Scenes.Manager.ScenesNotLoadedOnTime) {
                UI.Cameras.Current.CameraTarget.UpdateTargetPosition();
            }
            this.CurrentCameraTargetPosition = UI.Cameras.Current.CameraTarget.TargetPosition;
            this.CurrentCameraTargetPositionExtrapolated = this.CurrentCameraTargetPosition;
            this.UpdateExtrapolatedPosition();
        }
    }

    public void ClearCameraPuppetPositions() {
        this.m_cameraPositions.Clear();
    }

    public void UpdateExtrapolatedPosition() {
        Rect rect;
        if (Characters.Sein && this.GetSceneBoundaryAtPosition(this.CurrentCameraTargetPosition, out rect)) {
            Vector2 vector = this.CurrentCameraTargetPosition + Vector2.ClampMagnitude(Characters.Sein.PhysicsSpeed * 2f, 24f);
            this.CurrentCameraTargetPositionExtrapolated = new Vector2(Mathf.Clamp(vector.x, rect.xMin + 0.1f, rect.xMax - 0.1f), Mathf.Clamp(vector.y, rect.yMin + 0.1f, rect.yMax - 0.1f));
            Vector3 vector2 = this.CurrentCameraTargetPosition;
            Vector3 vector3 = this.CurrentCameraTargetPositionExtrapolated;
            bool flag = Mathf.Abs(vector3.x - vector2.x) > Mathf.Abs(vector3.y - vector2.y);
            for (int i = 0; i < 6; i++) {
                Debug.DrawLine(vector2, vector3, Color.gray);
                RaycastHit raycastHit;
                if (!Physics.Linecast(vector2, vector3, out raycastHit, this.RaycastMask)) {
                    this.CurrentCameraTargetPositionExtrapolated = vector3;
                    break;
                }
                vector2 = raycastHit.point - (vector3 - vector2).normalized * 0.02f;
                if (i == 5) {
                    this.CurrentCameraTargetPositionExtrapolated = vector2;
                    break;
                }
                Vector3 vector4 = vector2;
                vector4 += 4f * ((!flag) ? ((raycastHit.normal.x <= 0f) ? Vector3.left : Vector3.right) : ((raycastHit.normal.y <= 0f) ? Vector3.down : Vector3.up));
                Debug.DrawLine(vector2, vector4, Color.gray);
                vector2 = ((!Physics.Linecast(vector2, vector4, out raycastHit, this.RaycastMask)) ? vector4 : (raycastHit.point - (vector4 - vector2).normalized * 0.02f));
            }
        }
    }

    public bool GetSceneBoundaryAtPosition(Vector3 position, out Rect bound) {
        for (int i = 0; i < this.AllScenes.Count; i++) {
            RuntimeSceneMetaData runtimeSceneMetaData = this.AllScenes[i];
            if (!runtimeSceneMetaData.DependantScene) {
                if (runtimeSceneMetaData.IsInTotal(position)) {
                    if (runtimeSceneMetaData.CanBeLoaded) {
                        for (int j = 0; j < runtimeSceneMetaData.SceneBoundaries.Count; j++) {
                            Rect rect = runtimeSceneMetaData.SceneBoundaries[j];
                            if (rect.Contains(position)) {
                                bound = rect;
                                return true;
                            }
                        }
                    }
                }
            }
        }
        bound = new Rect(0f, 0f, 0f, 0f);
        return false;
    }

    public bool IsInsideASceneBoundary(Vector3 position) {
        List<RuntimeSceneMetaData> allScenes = this.AllScenes;
        for (int i = 0; i < allScenes.Count; i++) {
            RuntimeSceneMetaData runtimeSceneMetaData = allScenes[i];
            if (!runtimeSceneMetaData.DependantScene) {
                if (runtimeSceneMetaData.IsInTotal(position) && runtimeSceneMetaData.IsInsideSceneBounds(position)) {
                    return true;
                }
            }
        }
        return false;
    }

    public bool IsInsideActiveSceneBoundary(Vector3 position) {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (!sceneManagerScene.MetaData.DependantScene) {
                if ((sceneManagerScene.CurrentState == SceneManagerScene.State.Loaded || sceneManagerScene.CurrentState == SceneManagerScene.State.Disabling) && sceneManagerScene.MetaData.IsInsideSceneBounds(position)) {
                    return true;
                }
            }
        }
        return false;
    }

    public bool IsInsideAScenePaddingBoundary(Vector3 position) {
        Rect rect;
        this.GetSceneBoundaryAtPosition(position, out rect);
        List<RuntimeSceneMetaData> allScenes = this.AllScenes;
        for (int i = 0; i < allScenes.Count; i++) {
            if (allScenes[i].IsInsideScenePaddingBounds(position, rect)) {
                return true;
            }
        }
        return false;
    }

    public void Update() {
        if (this.m_resourcesNeedUnloading == 1) {
            SaveSceneManager.Master.ReleaseNullReferences();
            SuspensionManager.CleanupSuspendables();
        }
        if (this.m_resourcesNeedUnloading > 0) {
            this.m_resourcesNeedUnloading--;
        }
        this.DestroyManager.Update();
    }

    public SceneRoot FindLoadedSceneRootFromPosition(Vector3 position) {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (sceneManagerScene.CurrentState == SceneManagerScene.State.Loaded || sceneManagerScene.CurrentState == SceneManagerScene.State.Disabled || sceneManagerScene.CurrentState == SceneManagerScene.State.Disabling) {
                if (sceneManagerScene.SceneRoot && sceneManagerScene.SceneRoot.MetaData) {
                    if (!sceneManagerScene.SceneRoot.MetaData.DependantScene) {
                        if (sceneManagerScene.MetaData.IsInsideSceneBounds(position)) {
                            if (!sceneManagerScene.MetaData.LoadingCondition || sceneManagerScene.MetaData.LoadingCondition.Validate(null)) {
                                return sceneManagerScene.SceneRoot;
                            }
                        }
                    }
                }
            }
        }
        return null;
    }

    public SceneManagerScene GetFromCurrentScenes(RuntimeSceneMetaData sceneMetaData) {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (sceneManagerScene.MetaData == sceneMetaData) {
                return sceneManagerScene;
            }
        }
        return null;
    }

    public RuntimeSceneMetaData FindRuntimeSceneMetaData(MoonGuid sceneGuid) {
        RuntimeSceneMetaData runtimeSceneMetaData;
        if (this.m_guidToRuntimeSceneMetaDatas.TryGetValue(sceneGuid, out runtimeSceneMetaData)) {
            return runtimeSceneMetaData;
        }
        return null;
    }

    public void PreloadScene(RuntimeSceneMetaData sceneMetaData) {
        this.AdditivelyLoadScenesAtPosition(sceneMetaData.PlaceholderPosition, true, false, true);
    }

    public void PreloadScene(SceneMetaData sceneMetaData) {
        this.AdditivelyLoadScenesAtPosition(sceneMetaData.SeinPlaceholderPosition, true, false, true);
    }

    private void RemoveScene(SceneManagerScene scene) {
        this.ActiveScenes.Remove(scene);
    }

    private bool CanLevelBeLoaded(string sceneName) {
        bool flag;
        if (this.m_canBeStreamed.TryGetValue(sceneName, out flag)) {
            return flag;
        }
        flag = Application.CanStreamedLevelBeLoaded(sceneName);
        this.m_canBeStreamed[sceneName] = flag;
        return flag;
    }

    public void AdditivelyLoadScenesAtPosition(Vector3 position, bool async, bool loadingZones = true, bool keepPreloaded = false) {
        if (Time.timeScale > 2f) {
            async = false;
        }
        List<RuntimeSceneMetaData> allScenes = this.AllScenes;
        int count = allScenes.Count;
        Rect rect;
        this.GetSceneBoundaryAtPosition(position, out rect);
        for (int i = 0; i < count; i++) {
            RuntimeSceneMetaData runtimeSceneMetaData = allScenes[i];
            if (!runtimeSceneMetaData.DependantScene) {
                if (runtimeSceneMetaData.IsInTotal(position)) {
                    if (runtimeSceneMetaData.IsInsideSceneBounds(position)) {
                        if (runtimeSceneMetaData.CanBeLoaded) {
                            this.AdditivelyLoadScene(runtimeSceneMetaData, async, keepPreloaded);
                        }
                    } else if (runtimeSceneMetaData.IsInsideScenePaddingBounds(position, rect)) {
                        if (runtimeSceneMetaData.CanBeLoaded) {
                            this.AdditivelyLoadScene(runtimeSceneMetaData, async, keepPreloaded);
                        }
                    } else if (runtimeSceneMetaData.IsInsideSceneLoadingZone(position) && runtimeSceneMetaData.CanBeLoaded && loadingZones) {
                        this.AdditivelyLoadScene(runtimeSceneMetaData, true, keepPreloaded);
                    }
                }
            }
        }
    }

    public void AdditivelyLoadScenesInsideRect(Rect rect, bool async, bool loadingZones = true, bool keepPreloaded = false) {
        if (Time.timeScale > 2f) {
            async = false;
        }
        List<RuntimeSceneMetaData> allScenes = this.AllScenes;
        int count = allScenes.Count;
        Rect rect2;
        this.GetSceneBoundaryAtPosition(rect.center, out rect2);
        for (int i = 0; i < count; i++) {
            RuntimeSceneMetaData runtimeSceneMetaData = allScenes[i];
            if (!runtimeSceneMetaData.DependantScene) {
                if (runtimeSceneMetaData.IsInTotal(rect)) {
                    if (runtimeSceneMetaData.IsInsideSceneBounds(rect)) {
                        if (runtimeSceneMetaData.CanBeLoaded) {
                            this.AdditivelyLoadScene(runtimeSceneMetaData, async, keepPreloaded);
                        }
                    } else if (runtimeSceneMetaData.IsInsideScenePaddingBounds(rect, rect2)) {
                        if (runtimeSceneMetaData.CanBeLoaded) {
                            this.AdditivelyLoadScene(runtimeSceneMetaData, async, keepPreloaded);
                        }
                    } else if (runtimeSceneMetaData.IsInsideSceneLoadingZone(rect) && runtimeSceneMetaData.CanBeLoaded && loadingZones) {
                        this.AdditivelyLoadScene(runtimeSceneMetaData, true, keepPreloaded);
                    }
                }
            }
        }
    }

    private void AdditivelyLoadScene(RuntimeSceneMetaData sceneMetaData, bool async, bool keepPreloaded = false) {
        SceneManagerScene fromCurrentScenes = this.GetFromCurrentScenes(sceneMetaData);
        if (fromCurrentScenes != null) {
            if (fromCurrentScenes.CurrentState == SceneManagerScene.State.LoadingCancelled) {
                fromCurrentScenes.ChangeState(SceneManagerScene.State.Loading);
                this.LoadDependantScenes(fromCurrentScenes.MetaData, true);
                if (keepPreloaded) {
                    fromCurrentScenes.PreventUnloading = true;
                }
            }
        } else if (this.CanLevelBeLoaded(sceneMetaData.Scene)) {
            if (this.CanLoadScenes) {
                if (async) {
                    AsyncOperation asyncOperation = Application.LoadLevelAdditiveAsync(sceneMetaData.Scene);
                    asyncOperation.priority = 2;
                } else {
                    Application.LoadLevelAdditive(sceneMetaData.Scene);
                }
            }
            SceneManagerScene sceneManagerScene = new SceneManagerScene(sceneMetaData);
            this.ActiveScenes.Add(sceneManagerScene);
            sceneManagerScene.PreventUnloading = keepPreloaded;
            this.LoadDependantScenes(sceneMetaData, async);
        }
    }

    private void LoadDependantScenes(RuntimeSceneMetaData sceneMetaData, bool async) {
        for (int i = 0; i < sceneMetaData.IncludedScenes.Count; i++) {
            RuntimeSceneMetaData runtimeSceneMetaData = this.FindRuntimeSceneMetaData(sceneMetaData.IncludedScenes[i]);
            if (runtimeSceneMetaData != null && runtimeSceneMetaData.CanBeLoaded) {
                this.AdditivelyLoadScene(runtimeSceneMetaData, async, false);
            }
        }
    }

    public void UnloadScenesAtPosition(bool instant) {
        Rect clampedRect = this.GetClampedRect(this.CurrentCameraTargetPosition);
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            RuntimeSceneMetaData metaData = sceneManagerScene.MetaData;
            if (metaData != null) {
                if (!metaData.DependantScene) {
                    bool flag = metaData.IsInsideSceneBounds(this.CurrentCameraTargetPosition) || metaData.IsInsideScenePaddingBounds(this.CurrentCameraTargetPosition);
                    for (int j = 0; j < this.m_cameraPositions.Count; j++) {
                        Vector3 vector = this.m_cameraPositions[j];
                        if (metaData.IsInsideSceneBounds(vector) || metaData.IsInsideScenePaddingBounds(vector)) {
                            flag = true;
                        }
                    }
                    if (!flag || !metaData.CanBeLoaded) {
                        bool flag2 = (metaData.CanBeLoaded && (metaData.IsInsideSceneLoadingZone(clampedRect) || metaData.IsInsideSceneBounds(clampedRect) || metaData.IsInsideScenePaddingBoundsExpanded(clampedRect))) || sceneManagerScene.PreventUnloading || sceneManagerScene.KeepLoadedForCheckpoint || sceneManagerScene.IsTitleScreen;
                        if (this.UnloadScene(sceneManagerScene, flag2, instant || !metaData.CanBeLoaded)) {
                            i--;
                        }
                    }
                }
            }
        }
        this.UnloadDependantScenes();
    }

    public void OnPassThroughScrollLock() {
        this.UpdateScenes();
    }

    public void OnDisableSceneRoot(SceneRoot sceneRoot) {
        try {
            Events.Scheduler.OnSceneRootDisabled.Call(sceneRoot);
        } catch (Exception ex) {
        }
    }

    public bool UnloadScene(SceneManagerScene scene, bool keepInMemory, bool instant) {
        if (!this.AllowDestroying) {
            keepInMemory = true;
        }
        if (keepInMemory) {
            switch (scene.CurrentState) {
                case SceneManagerScene.State.Disabling:
                    if (Time.time > scene.UnloadTime || instant) {
                        this.OnDisableSceneRoot(scene.SceneRoot);
                        scene.ChangeState(SceneManagerScene.State.Disabled);
                        scene.SceneRoot.Save();
                        scene.SceneRoot.DisableScene();
                    }
                    return false;
                case SceneManagerScene.State.LoadingCancelled:
                    scene.ChangeState(SceneManagerScene.State.Loading);
                    return false;
                case SceneManagerScene.State.Loaded:
                    if (instant) {
                        scene.ChangeState(SceneManagerScene.State.Disabled);
                        scene.SceneRoot.Save();
                        this.OnDisableSceneRoot(scene.SceneRoot);
                        scene.SceneRoot.DisableScene();
                    } else {
                        scene.ChangeState(SceneManagerScene.State.Disabling);
                        scene.UnloadTime = Time.time + this.UnloadDelay;
                    }
                    return false;
            }
        } else {
            switch (scene.CurrentState) {
                case SceneManagerScene.State.Disabling:
                    if (Time.time > scene.UnloadTime) {
                        this.OnDisableSceneRoot(scene.SceneRoot);
                        scene.SceneRoot.SaveAndUnload();
                        this.RemoveScene(scene);
                        return true;
                    }
                    return false;
                case SceneManagerScene.State.Disabled:
                    scene.SceneRoot.Unload();
                    this.RemoveScene(scene);
                    return true;
                case SceneManagerScene.State.Loading:
                    scene.ChangeState(SceneManagerScene.State.LoadingCancelled);
                    return this.CancelScene(scene);
                case SceneManagerScene.State.Loaded:
                    if (instant) {
                        this.OnDisableSceneRoot(scene.SceneRoot);
                        scene.SceneRoot.SaveAndUnload();
                        this.RemoveScene(scene);
                        return true;
                    }
                    scene.ChangeState(SceneManagerScene.State.Disabling);
                    scene.UnloadTime = Time.time + this.UnloadDelay;
                    return false;
            }
        }
        return false;
    }

    public void ReleaseUnusedResources() {
        this.m_resourcesNeedUnloading = 3;
    }

    public void UnloadDependantScenes() {
        Vector3 vector = this.CurrentCameraTargetPosition;
        this.m_scenesToDisable.Clear();
        this.m_scenesToInclude.Clear();
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (!sceneManagerScene.MetaData.DependantScene) {
                if (sceneManagerScene.CurrentState == SceneManagerScene.State.Disabled || sceneManagerScene.CurrentState == SceneManagerScene.State.Loading) {
                    for (int j = 0; j < sceneManagerScene.MetaData.IncludedScenes.Count; j++) {
                        RuntimeSceneMetaData runtimeSceneMetaData = this.FindRuntimeSceneMetaData(sceneManagerScene.MetaData.IncludedScenes[j]);
                        if (runtimeSceneMetaData != null) {
                            this.m_scenesToDisable.Add(runtimeSceneMetaData);
                        }
                    }
                }
                if (sceneManagerScene.CurrentState == SceneManagerScene.State.Loaded || sceneManagerScene.CurrentState == SceneManagerScene.State.Disabling) {
                    if (sceneManagerScene.MetaData.IsInsideSceneBounds(vector) || sceneManagerScene.MetaData.IsInsideScenePaddingBounds(vector)) {
                        for (int k = 0; k < sceneManagerScene.MetaData.IncludedScenes.Count; k++) {
                            RuntimeSceneMetaData runtimeSceneMetaData2 = this.FindRuntimeSceneMetaData(sceneManagerScene.MetaData.IncludedScenes[k]);
                            if (runtimeSceneMetaData2 != null) {
                                this.m_scenesToInclude.Add(runtimeSceneMetaData2);
                            }
                        }
                    } else {
                        for (int l = 0; l < sceneManagerScene.MetaData.IncludedScenes.Count; l++) {
                            RuntimeSceneMetaData runtimeSceneMetaData3 = this.FindRuntimeSceneMetaData(sceneManagerScene.MetaData.IncludedScenes[l]);
                            if (runtimeSceneMetaData3 != null) {
                                this.m_scenesToDisable.Add(runtimeSceneMetaData3);
                            }
                        }
                    }
                }
            }
        }
        for (int m = 0; m < this.ActiveScenes.Count; m++) {
            SceneManagerScene sceneManagerScene2 = this.ActiveScenes[m];
            RuntimeSceneMetaData metaData = sceneManagerScene2.MetaData;
            if (metaData.DependantScene && !this.m_scenesToInclude.Contains(metaData) && this.UnloadScene(sceneManagerScene2, this.m_scenesToDisable.Contains(metaData), true)) {
                m--;
            }
        }
        this.m_scenesToDisable.Clear();
        this.m_scenesToInclude.Clear();
    }

    public void UpdateScenes() {
        this.UpdatePosition();
        if (this.IsInsideASceneBoundary(this.CurrentCameraTargetPosition) && !this.ScenesNotLoadedOnTime) {
            this.UnloadScenesAtPosition(false);
        }
        this.AdditivelyLoadScenesAtPosition(this.CurrentCameraTargetPositionExtrapolated, true, true, false);
    }

    public void OnSceneStartCompleted(SceneRoot sceneRoot) {
        RuntimeSceneMetaData runtimeSceneMetaData = this.FindRuntimeSceneMetaData(sceneRoot.MetaData.SceneMoonGuid);
        SceneManagerScene fromCurrentScenes = this.GetFromCurrentScenes(runtimeSceneMetaData);
        if (fromCurrentScenes != null) {
            fromCurrentScenes.HasStartBeenCalled = true;
        }
    }

    public void Register(SceneRoot sceneRoot) {
        if (sceneRoot.name == "worldMapScene") {
            WorldMapUI.OnFinishedLoading(sceneRoot);
            return;
        }
        RuntimeSceneMetaData runtimeSceneMetaData = this.FindRuntimeSceneMetaData(sceneRoot.MetaData.SceneMoonGuid);
        SceneManagerScene sceneManagerScene = this.GetFromCurrentScenes(runtimeSceneMetaData);
        SceneMetaData metaData = sceneRoot.MetaData;
        if (sceneManagerScene == null) {
            sceneManagerScene = new SceneManagerScene(sceneRoot, runtimeSceneMetaData);
            this.UpdatePosition();
            if (sceneRoot.MetaData.IsInsideSceneBounds(this.CurrentCameraTargetPosition) || sceneRoot.MetaData.IsInsideScenePaddingBounds(this.CurrentCameraTargetPosition)) {
                this.ActiveScenes.Add(sceneManagerScene);
                this.EnableDisabledScene(sceneManagerScene);
            } else {
                sceneManagerScene.CurrentState = SceneManagerScene.State.Disabled;
                this.ActiveScenes.Add(sceneManagerScene);
                sceneRoot.DisableScene();
            }
        } else {
            if (sceneManagerScene.SceneRoot == sceneRoot) {
                return;
            }
            if (sceneManagerScene.CurrentState == SceneManagerScene.State.Loading) {
                sceneManagerScene.ChangeState(SceneManagerScene.State.Disabled);
                if (sceneRoot.MetaData.RootPosition != sceneRoot.transform.position) {
                    sceneRoot.transform.position = sceneRoot.MetaData.RootPosition;
                }
                sceneManagerScene.SceneRoot = sceneRoot;
                sceneRoot.DisableScene();
            } else if (sceneManagerScene.CurrentState == SceneManagerScene.State.LoadingCancelled) {
                sceneManagerScene.SceneRoot = sceneRoot;
                sceneRoot.Unload();
                this.RemoveScene(sceneManagerScene);
            } else {
                sceneRoot.Unload();
            }
        }
        sceneManagerScene.LoadingTime = Time.realtimeSinceStartup - sceneManagerScene.TimeOfLoad;
        SceneFrameworkPerformanceMonitor.AddSceneLoadItem(sceneManagerScene);
    }

    public bool AnyMissingScenesAtCurrentPosition() {
        Vector3 vector = this.CurrentCameraTargetPosition;
        Bounds cameraBoundingBox = UI.Cameras.Current.CameraBoundingBox;
        cameraBoundingBox.Expand(2f);
        cameraBoundingBox.center = vector;
        Rect rect = Utility.RectFromBounds(cameraBoundingBox);
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            RuntimeSceneMetaData metaData = sceneManagerScene.MetaData;
            if (!metaData.DependantScene) {
                bool flag = metaData.IsInsideSceneBounds(rect) && (metaData.IsInsideSceneBounds(vector) || metaData.IsInsideScenePaddingBounds(vector));
                if (flag && sceneManagerScene.UnityIsLoading) {
                    return true;
                }
            }
        }
        return false;
    }

    public void EnableDisabledScenesAtPosition(bool limitOnce = false) {
        Vector3 vector = this.CurrentCameraTargetPosition;
        this.m_scenesToEnable.Clear();
        Rect rect;
        this.GetSceneBoundaryAtPosition(vector, out rect);
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            if (!sceneManagerScene.UnityIsLoading) {
                if (sceneManagerScene.MetaData != null) {
                    RuntimeSceneMetaData metaData = sceneManagerScene.MetaData;
                    if (!metaData.DependantScene) {
                        if (sceneManagerScene.CurrentState == SceneManagerScene.State.Disabled || sceneManagerScene.CurrentState == SceneManagerScene.State.Disabling) {
                            bool flag = metaData.IsInsideSceneBounds(vector) || metaData.IsInsideScenePaddingBounds(vector, rect);
                            for (int j = 0; j < this.m_cameraPositions.Count; j++) {
                                Vector3 vector2 = this.m_cameraPositions[j];
                                if (metaData.IsInsideSceneBounds(vector2) || metaData.IsInsideScenePaddingBounds(vector2, rect)) {
                                    flag = true;
                                }
                            }
                            if (flag && metaData.CanBeLoaded) {
                                if (sceneManagerScene.CurrentState == SceneManagerScene.State.Disabled) {
                                    this.EnableDisabledScene(sceneManagerScene);
                                    if (limitOnce) {
                                        this.m_scenesToEnable.Clear();
                                        return;
                                    }
                                } else {
                                    sceneManagerScene.CurrentState = SceneManagerScene.State.Loaded;
                                }
                            }
                        }
                        if ((sceneManagerScene.CurrentState == SceneManagerScene.State.Loaded || sceneManagerScene.CurrentState == SceneManagerScene.State.Disabling) && (sceneManagerScene.MetaData.IsInsideSceneBounds(vector) || metaData.IsInsideScenePaddingBounds(vector))) {
                            for (int k = 0; k < sceneManagerScene.MetaData.IncludedScenes.Count; k++) {
                                MoonGuid moonGuid = sceneManagerScene.MetaData.IncludedScenes[k];
                                RuntimeSceneMetaData runtimeSceneMetaData = this.FindRuntimeSceneMetaData(moonGuid);
                                if (runtimeSceneMetaData != null) {
                                    this.m_scenesToEnable.Add(runtimeSceneMetaData);
                                }
                            }
                        }
                    }
                }
            }
        }
        for (int l = 0; l < this.ActiveScenes.Count; l++) {
            SceneManagerScene sceneManagerScene2 = this.ActiveScenes[l];
            if (sceneManagerScene2.CurrentState == SceneManagerScene.State.Disabled) {
                RuntimeSceneMetaData metaData2 = sceneManagerScene2.MetaData;
                if (metaData2.DependantScene && this.m_scenesToEnable.Contains(sceneManagerScene2.MetaData)) {
                    this.EnableDisabledScene(sceneManagerScene2);
                    if (limitOnce) {
                        this.m_scenesToEnable.Clear();
                        return;
                    }
                }
            }
        }
        this.m_scenesToEnable.Clear();
    }

    private void EnableDisabledScene(SceneManagerScene scene) {
        scene.ChangeState(SceneManagerScene.State.Loaded);
        Events.Scheduler.OnSceneRootPreEnabled.Call(scene.SceneRoot);
        scene.PreventUnloading = false;
        scene.SceneRoot.EnableScene();
        if (!scene.HasStartBeenCalled) {
            scene.SceneRoot.EarlyStart();
        }
        LateStartHook.AddLateStartMethod(new Action(scene.SceneRoot.RegisterSceneRootEnabledAfterSerialize));
    }

    public void CheckForScenesFinishedLoading() {
        InstantLoadScenesController.Instance.OnScenesManagerFixedUpdate();
        GoToSceneController.Instance.OnScenesManagerFixedUpdate();
    }

    public void UnloadAllScenes() {
        foreach (SceneManagerScene sceneManagerScene in this.ActiveScenes.ToArray()) {
            if (!sceneManagerScene.IsTitleScreen) {
                switch (sceneManagerScene.CurrentState) {
                    case SceneManagerScene.State.Disabling:
                        sceneManagerScene.SceneRoot.SaveAndUnload();
                        this.RemoveScene(sceneManagerScene);
                        break;
                    case SceneManagerScene.State.Disabled:
                        sceneManagerScene.SceneRoot.Unload();
                        this.RemoveScene(sceneManagerScene);
                        break;
                    case SceneManagerScene.State.Loading:
                        sceneManagerScene.ChangeState(SceneManagerScene.State.LoadingCancelled);
                        this.CancelScene(sceneManagerScene);
                        break;
                    case SceneManagerScene.State.Loaded:
                        sceneManagerScene.SceneRoot.SaveAndUnload();
                        this.RemoveScene(sceneManagerScene);
                        break;
                }
            }
        }
    }

    private bool CancelScene(SceneManagerScene scene) {
        return false;
    }

    public void AllowUnloadingOnAllScenes() {
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            sceneManagerScene.PreventUnloading = false;
            sceneManagerScene.KeepLoadedForCheckpoint = false;
        }
    }

    public void AllowUnloadingOnScenes(Vector3 position) {
        Rect clampedRect = this.GetClampedRect(position);
        for (int i = 0; i < this.ActiveScenes.Count; i++) {
            SceneManagerScene sceneManagerScene = this.ActiveScenes[i];
            RuntimeSceneMetaData metaData = sceneManagerScene.MetaData;
            if (!metaData.DependantScene) {
                if (metaData.CanBeLoaded && (metaData.IsInsideSceneLoadingZone(clampedRect) || metaData.IsInsideSceneBounds(clampedRect) || metaData.IsInsideScenePaddingBounds(clampedRect))) {
                    sceneManagerScene.PreventUnloading = false;
                }
            }
        }
    }

    public bool SceneIsLoaded(MoonGuid sceneGuid) {
        foreach (SceneManagerScene sceneManagerScene in this.ActiveScenes) {
            if (sceneManagerScene.MetaData.SceneMoonGuid == sceneGuid) {
                if (sceneManagerScene.CurrentState == SceneManagerScene.State.Loading || sceneManagerScene.CurrentState == SceneManagerScene.State.LoadingCancelled) {
                    return false;
                }
                return true;
            }
        }
        return false;
    }

    public void OnFinishedStreamingInstall() {
        this.m_canBeStreamed.Clear();
    }

    public string GetSceneNameAtPosition(Vector3 position) {
        for (int i = 0; i < AllScenes.Count; i++) {
            var runtimeSceneMetaData = AllScenes[i];
            if (!runtimeSceneMetaData.DependantScene && runtimeSceneMetaData.IsInTotal(position) && runtimeSceneMetaData.IsInsideSceneBounds(position)) {
                return runtimeSceneMetaData.Scene;
            }
        }
        return null;
    }

    public float UnloadDelay = 1f;

    public List<SceneManagerScene> ActiveScenes = new List<SceneManagerScene>();

    public DestroyManager DestroyManager = new DestroyManager();

    public bool AutoLoadingUnloading = true;

    public MessageProvider FellOutOfWorldMessage;

    public bool AllowDestroying;

    public List<RuntimeSceneMetaData> AllScenes = new List<RuntimeSceneMetaData>();

    private readonly List<Vector3> m_cameraPositions = new List<Vector3>();

    private int m_resourcesNeedUnloading;

    private readonly HashSet<RuntimeSceneMetaData> m_scenesToDisable = new HashSet<RuntimeSceneMetaData>();

    private readonly HashSet<RuntimeSceneMetaData> m_scenesToInclude = new HashSet<RuntimeSceneMetaData>();

    private readonly HashSet<RuntimeSceneMetaData> m_scenesToEnable = new HashSet<RuntimeSceneMetaData>();

    private readonly Dictionary<string, bool> m_canBeStreamed = new Dictionary<string, bool>();

    private readonly Dictionary<MoonGuid, RuntimeSceneMetaData> m_guidToRuntimeSceneMetaDatas = new Dictionary<MoonGuid, RuntimeSceneMetaData>();

    public bool CanLoadScenes = true;

    private AsyncOperation m_currentLoad;

    private List<string> m_scenesToLoad = new List<string>();

    private List<string> m_backgroundsToLoad = new List<string>();

    private HashSet<MoonGuid> m_scenes = new HashSet<MoonGuid>();

    private float m_testDelayTime;

    public LayerMask RaycastMask;
}

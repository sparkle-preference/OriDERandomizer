using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Core;
using Game;
using UnityEngine;
using Object = UnityEngine.Object;
using Shader = Frameworks.Shader;

public class GameController : SaveSerialize, ISuspendable {
    public bool MainMenuCanBeOpened { get; set; }

    public int GameTimeInSeconds => Mathf.RoundToInt(Timer.CurrentTime);

    public void PerformSaveGameSequence() {
        RandomizerStatsManager.OnSave(false);
        if (GameSaveSequence) {
            GameSaveSequence.Perform(null);
        }
    }

    public bool IsPackageFullyInstalled => !DebugMenuB.IsFullyInstalledDebugOverride;

    public bool IsTrial => PCTrialValue;

    public bool IsDemo {
        get {
            WorldEventsRuntime worldEventsRuntime = World.Events.Find(DebugWorldEvents);
            return worldEventsRuntime.Value == DebugWorldEvents.GetIDFromName("Demo");
        }
    }

    public void ExitGame() {
        if (IsTrial) {
            Instance.GoToEndTrialScreen();
        } else {
            Instance.QuitApplication();
        }
    }

    public void ExitTrial() {
        Instance.RestartGame();
    }

    public void QuitApplication() {
        Application.Quit();
    }

    public void GoToEndTrialScreen() {
        MainMenuCanBeOpened = false;
        GameStateMachine.Instance.SetToTrialEnd();
        RuntimeSceneMetaData sceneInformation = Scenes.Manager.GetSceneInformation("trialEndScreen");
        GoToSceneController.Instance.GoToScene(sceneInformation, OnFinishedLoadingTrialEndScene, false);
    }

    public void OnFinishedLoadingTrialEndScene() {
        RemoveGameplayObjects();
    }

    public void OnGameReset() {
        SaveSlotsManager.BackupIndex = -1;
        TriggerByString.OnGameReset();
        SeinLevel.HasSpentSkillPoint = false;
        WorldEventsManager.Instance.OnGameReset();
        SoundPlayer.DestroyAll();
    }

    public void RemoveGameplayObjects() {
        CharacterFactory.Instance.DestroyCharacter();
        if (Characters.Sein) {
            InstantiateUtility.Destroy(Characters.Sein.gameObject);
        }

        if (Characters.Naru) {
            InstantiateUtility.Destroy(Characters.Naru.gameObject);
        }

        if (Characters.BabySein) {
            InstantiateUtility.Destroy(Characters.BabySein.gameObject);
        }

        if (Characters.Ori) {
            InstantiateUtility.Destroy(Characters.Ori.gameObject);
        }

        if (UI.SeinUI) {
            InstantiateUtility.Destroy(UI.SeinUI.gameObject);
        }

        Core.SoundComposition.Manager.StopMusic();
        UI.Cameras.Current.Target = null;
        if (UI.MainMenuVisible) {
            UI.Menu.HideMenuScreen();
        }

        UI.Menu.RemoveGameplayObjects();
        WorldMapUI.CancelLoading();
    }

    public void ResetStateForDebugMenuGoToScene() {
        RemoveGameplayObjects();
        RequireInitialValues = true;
    }

    public void RestartGame() {
        if (m_isRestartingGame) {
            return;
        }

        RuntimeSceneMetaData sceneInformation = Scenes.Manager.GetSceneInformation("titleScreenSwallowsNest");
        if (sceneInformation == null) {
            return;
        }

        Timer.Reset();
        MainMenuCanBeOpened = false;
        RequireInitialValues = true;
        Instance.IsLoadingGame = false;
        InstantLoadScenesController.Instance.OnGameReset();
        GoToSceneController.Instance.GoToScene(sceneInformation, OnFinishedRestarting, false);
    }

    private void OnFinishedRestarting() {
        StartCoroutine(RestartingCleanupNextFrame());
    }

    public IEnumerator RestartingCleanupNextFrame() {
        RemoveGameplayObjects();
        ResetInputLocks();
        if (UI.Fader.IsFadingInOrStay() || UI.Fader.IsTimelineFading()) {
            UI.Fader.FadeOut(2f);
        }

        XboxLiveController.Instance.Reset();
        XboxOneController.ResetCurrentGamepad();
        XboxOneFlow.Engage = false;
        XboxOneSession.EndSession();
        yield return new WaitForFixedUpdate();
        m_isRestartingGame = false;
        ActiveObjectives.Clear();
        Game.Checkpoint.SaveGameData = new SaveGameData();
        Events.Scheduler.OnGameSerializeLoad.Call();
        Events.Scheduler.OnGameReset.Call();
        if (UI.Fader.IsFadingInOrStay() || UI.Fader.IsTimelineFading()) {
            UI.Fader.FadeOut(2f);
        }

        TitleScreenManager.OnReturnToTitleScreen();
        CreateCheckpoint();
        yield break;
    }

    public bool GameplaySuspended { get; set; }

    public bool GameplaySuspendedForUI { get; set; }

    public bool InputLocked => LockInput || LockInputByAction;

    public bool LockInputByAction { get; set; }

    public bool LockInput { get; set; }

    [ContextMenu("Print out sizes of SaveSlot")]
    public void PrintOutSizesOfSaveSlot() {
        int num = 0;
        foreach (KeyValuePair<MoonGuid, SaveScene> keyValuePair in Game.Checkpoint.SaveGameData.Scenes) {
            foreach (SaveObject saveObject in keyValuePair.Value.SaveObjects) {
                num += saveObject.Data.MemoryStream.Capacity;
            }

            num += 16;
        }
    }

    public override void Awake() {
        if (Instance != null) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HandleTrialData();
        Randomizer.InitializeOnce();
        WarmUpResources();
        base.Awake();
        if (LoadingBootstrap.Instance) {
            Destroy(LoadingBootstrap.Instance.gameObject);
        }

        GameScheduler.OnGameAwake.Add(OnGameAwake);
        GameScheduler.OnGameAwake.Call();
        GameScheduler.OnGameReset.Add(OnGameReset);
        UberGCManager.OnGameStart();
        m_systemsGameObject = new GameObject("systems");
        Utility.DontAssociateWithAnyScene(m_systemsGameObject);
        transform.parent = m_systemsGameObject.transform;
        foreach (GameObject gameObject in Systems) {
            try {
                if (gameObject) {
                    GameObject gameObject2 = Instantiate(gameObject);
                    gameObject2.name = gameObject.name;
                    gameObject2.transform.SetParentMaintainingLocalTransform(m_systemsGameObject.transform);
                }
            } catch (Exception ex) {
            }
        }

        new Telemetry();
        UI.LoadMessageController();
        Systems.Clear();
        Application.targetFrameRate = 60;
        UberGCManager.CollectProactiveFull();
    }

    private void OnGameAwake() {
        m_restoreCheckpointController = new RestoreCheckpointController();
        Shader.Globals.FogGradientRange = 100f;
        Shader.Globals.FogGradientTexture = Shader.DefaultTextures.Transparent;
        FixedRandom.UpdateValues();
        if (ScenesToSkip.Instance == null) {
            new ScenesToSkip();
        }

        SaveSceneManager.Master = GetComponent<SaveSceneManager>();
    }

    public IEnumerator Start() {
        GameplayCamera currentCamera = UI.Cameras.Current;
        currentCamera.ChangeTargetToCurrentCharacter();
        Scenes.Manager.EnableDisabledScenesAtPosition();
        currentCamera.UpdateTargetHelperPosition();
        currentCamera.MoveCameraToTargetPosition();
        currentCamera.OffsetController.UpdateOffset(true);
        currentCamera.MoveCameraToTargetInstantly();
        yield return new WaitForFixedUpdate();
        GameSettings.Instance.LoadSettings();
        CreateCheckpoint();
        SaveSceneManager.Master.RegisterGameObject(m_systemsGameObject);
        SuspensionManager.Register(this);
        if (!IsTrial) {
            WaitForSaveGameLogic.OnCompletedStatic = (Action)Delegate.Combine(WaitForSaveGameLogic.OnCompletedStatic, new Action(AchievementsLogic.Instance.HandleTrialAchievements));
        }

        yield break;
    }

    private void OnApplicationFocus(bool focusStatus) {
        if (focusStatus) {
            m_setRunInBackgroundToFalse = false;
            Application.runInBackground = true;
            if (CurVsyncValue != 0) {
                QualitySettings.vSyncCount = CurVsyncValue;
                CurVsyncValue = 0;
            }
        } else if (QualitySettings.vSyncCount != 0) {
            CurVsyncValue = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
        }
    }

    private IEnumerator SetRunInBackgroundToTrue() {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        if (m_setRunInBackgroundToFalse && !PreventFocusPause) {
            m_setRunInBackgroundToFalse = false;
            Application.runInBackground = false;
        }

        yield break;
    }

    private IEnumerator LoadAssets(List<string> assetsToLoad) {
        foreach (string assetToLoad in assetsToLoad) {
            WWW www = new WWW(assetToLoad);
            yield return www;
            Instantiate(www.assetBundle.mainAsset);
        }

        yield break;
    }

    public override void OnDestroy() {
        InstantiateUtility.Destroy(m_systemsGameObject);
        SuspensionManager.Unregister(this);
        base.OnDestroy();
    }

    public void ResetInputLocks() {
        LockInputByAction = false;
        LockInput = false;
    }

    public override void Serialize(Archive ar) {
        if (ar.Reading) {
            ResetInputLocks();
        }

        WorldEventsManager.Instance.Serialize(ar);
        TriggerByString.SerializeStringTriggers(ar);
        ar.Serialize(0f);
        ar.Serialize(ref GameTime);
        ar.Serialize(0);
        ar.Serialize(0);
        ar.Serialize(ref RequireInitialValues);
        if (ar.Reading) {
            RequireInitialValues = false;
        }

        Game.Objectives.Serialize(ar);
    }

    public void WarmUpResources() {
        Timer timer = new Timer();
        UI.LoadMessageController();
        Orbs.OrbDisplayText.LoadOrbText();
        Attacking.DamageDisplayText.LoadDamageText();
        Sound.LoadAudioParent();
        UberGhostTrail.WarmUpResource();
        MixerManager.WarmUpResource();
        InteractionRotationModifier.WarmUpResource();
        Randomizer.initialize();
        timer.Report("Warming resources");
        Resources.Clear();
    }

    public void SetupGameplay(SceneRoot sceneRoot, WorldEventsOnAwake worldEventsOnAwake) {
        sceneRoot.MetaData.InitialValues.ApplyInitialValues();
        WarmUpResources();
        if (worldEventsOnAwake != null) {
            worldEventsOnAwake.Apply();
        }

        Randomizer.SetupNewGame();
        LateStartHook.AddLateStartMethod(CreateCheckpoint);
    }

    public void OnApplicationQuit() {
        IsClosing = true;
        if (m_logCallbackHandler != null) {
            m_logCallbackHandler.FlushEntriesToFile(m_logOutputFile);
        }

        MoonDebug.OnApplicationQuit();
        Randomizer.OnApplicationQuit();
    }

    public void Update() {
        Randomizer.Update();

        bool shiftHeld = MoonInput.GetKey(KeyCode.LeftShift) || MoonInput.GetKey(KeyCode.RightShift);
        bool altHeld = MoonInput.GetKey(KeyCode.LeftAlt) || MoonInput.GetKey(KeyCode.RightAlt);
        if (altHeld && !shiftHeld && MoonInput.GetKeyDown(KeyCode.U)) {
            UI.SeinUI.ShowUI = true;
            SeinUI.DebugHideUI = !SeinUI.DebugHideUI;
        }

        if (altHeld && shiftHeld && MoonInput.GetKeyDown(KeyCode.U) && RandomizerBonus.EnhancedSpiritFlame) {
            RandomizerBonus.SuppressEnhancedSpiritFlame = !RandomizerBonus.SuppressEnhancedSpiritFlame;
            Randomizer.printInfo("Enhanced Spirit Flame text " + (RandomizerBonus.SuppressEnhancedSpiritFlame ? "disabled :(" : "enabled :)"));
        }
    }

    private static void CheckPackageFullyInstalled() {
    }

    public void FixedUpdate() {
        if (Scenes.Manager) {
            RandomizerBootstrap.FixedUpdate();
            Scenes.Manager.CheckForScenesFinishedLoading();
        }

        if (!FreezeFixedUpdate) {
            FixedRandom.FixedUpdateIndex++;
            FixedRandom.UpdateValues();
        }

        Music.UpdateMusic();
        Ambience.UpdateAmbience();
        GameScheduler.OnGameFixedUpdate.Call();
        Respawner.UpdateRespawners();
        if (!GameStateMachine.Instance.IsInExtendedTitleScreen() && !UI.MainMenuVisible && (Screen.width != m_previousScreenWidth || Screen.height != m_previousScreenHeight)) {
            UI.Menu.ShowResumeScreen();
        }

        m_previousScreenWidth = Screen.width;
        m_previousScreenHeight = Screen.height;
        if (m_lastDebugControlsEnabledValue != DebugMenuB.DebugControlsEnabled) {
            m_lastDebugControlsEnabledValue = DebugMenuB.DebugControlsEnabled;
        }

        if (!IsSuspended) {
            GameTime += Time.deltaTime;
        }
    }

    public Objective GetObjectiveFromIndex(int index) {
        if (Objectives.Count > index && index >= 0) {
            return Objectives[index];
        }

        return null;
    }

    public int GetObjectiveIndex(Objective objective) {
        return Objectives.IndexOf(objective);
    }

    public void SuspendGameplay() {
        if (!GameplaySuspended) {
            Component[] suspendables = Characters.Sein.Controller.Suspendables;
            m_suspendablesToIgnoreForGameplay = new HashSet<ISuspendable>(suspendables.Cast<ISuspendable>());
            SuspensionManager.SuspendExcluding(m_suspendablesToIgnoreForGameplay);
            GameplaySuspended = true;
        }
    }

    public void ResumeGameplay() {
        if (GameplaySuspended) {
            SuspensionManager.ResumeExcluding(m_suspendablesToIgnoreForGameplay);
            m_suspendablesToIgnoreForGameplay.Clear();
            GameplaySuspended = false;
        }
    }

    public void SuspendGameplayForUI() {
        if (!GameplaySuspendedForUI) {
            SuspensionManager.SuspendAll();
            GameplaySuspendedForUI = true;
        }
    }

    public void ResumeGameplayForUI() {
        if (GameplaySuspendedForUI) {
            SuspensionManager.ResumeAll();
            GameplaySuspendedForUI = false;
        }
    }

    public void CreateCheckpoint() {
        SaveGameData saveGameData = Game.Checkpoint.SaveGameData;
        SaveSceneManager.Master.SaveWithoutClearing(saveGameData.Master);
        saveGameData.ApplyPendingScenes();
        if (Scenes.Manager) {
            foreach (SceneManagerScene sceneManagerScene in Scenes.Manager.ActiveScenes) {
                if (sceneManagerScene.IsVisible && sceneManagerScene.HasStartBeenCalled && sceneManagerScene.SceneRoot.SaveSceneManager) {
                    sceneManagerScene.SceneRoot.SaveSceneManager.Save(saveGameData.InsertScene(sceneManagerScene.MetaData.SceneMoonGuid));
                }
            }
        }

        Game.Checkpoint.Events.OnPostCreate.Call();
    }

    public void ClearCheckpointData() {
        Game.Checkpoint.SaveGameData.ClearAllData();
    }

    public void RestoreCheckpoint(Action onFinished = null) {
        IsLoadingGame = true;
        m_onRestoreCheckpointFinished = onFinished;
        LateStartHook.AddLateStartMethod(RestoreCheckpointImmediate);
    }

    public void RestoreCheckpointImmediate() {
        m_restoreCheckpointController.RestoreCheckpoint();
        if (m_onRestoreCheckpointFinished != null) {
            m_onRestoreCheckpointFinished();
            m_onRestoreCheckpointFinished = null;
        }
    }

    private void HandleTrialData() {
        if (IsTrial) {
            return;
        }

        if (OutputFolder.PlayerTrialDataFolderPath == OutputFolder.PlayerDataFolderPath) {
            return;
        }

        if (!Directory.Exists(OutputFolder.PlayerTrialDataFolderPath)) {
            return;
        }

        string[] files = Directory.GetFiles(OutputFolder.PlayerTrialDataFolderPath);
        for (int i = 0; i < files.Length; i++) {
            string fileName = Path.GetFileName(files[i]);
            string path = Path.Combine(OutputFolder.PlayerDataFolderPath, fileName);
            if (!File.Exists(path)) {
                File.Move(files[i], Path.Combine(OutputFolder.PlayerDataFolderPath, fileName));
            }
        }

        if (Directory.GetFiles(OutputFolder.PlayerTrialDataFolderPath).Length == 0) {
            Directory.Delete(OutputFolder.PlayerTrialDataFolderPath);
        }
    }

    [Conditional("NOT_FINAL_BUILD")]
    private void HandleBuildName() {
    }

    [Conditional("NOT_FINAL_BUILD")]
    private void HandleCommands() {
    }

    [Conditional("NOT_FINAL_BUILD")]
    private void HandleBuildIDString() {
    }

    public bool GameInTitleScreen => GameStateMachine.Instance.CurrentState == GameStateMachine.State.TitleScreen || GameStateMachine.Instance.CurrentState == GameStateMachine.State.StartScreen;

    public bool IsSuspended { get; set; }

    public bool PreventFocusPause { get; set; }

    public const string TitleScreenSceneName = "titleScreenSwallowsNest";

    public const string TrialEndScreenSceneName = "trialEndScreen";

    public const string IntroLogosSceneName = "introLogos";

    public const string TrailerSceneName = "trailerScene";

    public const string WorldMapSceneName = "worldMapScene";

    public const string EmptyTestSceneName = "emptyTestScene";

    public const string BootLoadSceneName = "loadBootstrap";

    public const string GameStartScene = "sunkenGladesRunaway";

    public GameTimer Timer;

    public static GameController Instance;

    public static bool FreezeFixedUpdate;

    public static bool IsClosing;

    public SaveGameController SaveGameController = new SaveGameController();

    public List<GameObject> Systems = new List<GameObject>();

    public GameScheduler GameScheduler = new GameScheduler();

    public AllContainer<Objective> ActiveObjectives = new AllContainer<Objective>();

    public List<Objective> Objectives = new List<Objective>();

    public string BuildIDString = string.Empty;

    public string BuildName = string.Empty;

    public UberAtlassingPlatform AtlasPlatform;

    private HashSet<ISuspendable> m_suspendablesToIgnoreForGameplay = new HashSet<ISuspendable>();

    private GameObject m_systemsGameObject;

    private LogCallbackHandler m_logCallbackHandler;

    private RestoreCheckpointController m_restoreCheckpointController = new RestoreCheckpointController();

    public int VSyncCount = 1;

    private string m_logOutputFile = string.Empty;

    public float GameTime;

    public ActionSequence GameSaveSequence;

    public static bool IsFocused = true;

    private static volatile bool m_isPackageFullyInstalled;

    public bool PCTrialValue;

    public bool EditorTrialValue;

    public WorldEvents DebugWorldEvents;

    private bool m_isRestartingGame;

    private bool m_setRunInBackgroundToFalse;

    public bool RequireInitialValues = true;

    public bool IsLoadingGame;

    public List<Object> Resources;

    private bool m_lastDebugControlsEnabledValue;

    private int m_previousScreenWidth;

    private int m_previousScreenHeight;

    private float m_isPackageFullyInstalledTimer;

    private Action m_onRestoreCheckpointFinished;

    private int CurVsyncValue;
}

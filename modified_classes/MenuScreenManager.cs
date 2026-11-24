using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;

public class MenuScreenManager : Suspendable {
    public static bool MenuOpenKeyPressed => Core.Input.Start.OnPressed && !Core.Input.Start.Used;

    public new void Awake() {
        base.Awake();
        UI.Menu = this;
        SetupOptionsScreen(OptionsPrefab);
        SetupPauseScreen(PausePrefab);
        Events.Scheduler.OnGameReset.Add(OnGameReset);
        UberPostProcess.Instance.SetDoBlur(false);
    }

    public new void OnDestroy() {
        base.OnDestroy();
        Events.Scheduler.OnGameReset.Remove(OnGameReset);
    }

    public void OnGameReset() {
        LockClosingMenu = false;
    }

    private bool CanOpenMenus() {
        return GameController.Instance.MainMenuCanBeOpened && !UI.Fader.IsFadingInOrStay();
    }

    public bool IsInventoryVisible() {
        return MainMenuVisible && CurrentScreen == Screens.Inventory;
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (MenuOpenKeyPressed) {
            DoMenuKeyPress();
        }

        if (Characters.Sein && Characters.Sein.Controller.CanMove && !Core.Input.Select.Used && Core.Input.Select.OnPressed) {
            Core.Input.Select.Used = true;
            Core.Input.Cancel.Used = true;
            if (MainMenuVisible) {
                HideMenuScreen(false);
            } else if (CanOpenMenus() && Characters.Sein && Characters.Sein.Controller.CanMove && Characters.Sein.Active && GameMapUI.Instance != null && WorldMapLogic.Instance != null && MapOpensHere() && World.CurrentArea != null && !GameMapUI.Instance.ShowingTeleporters && WorldMapUI.IsReady) {
                ShowAreaMap();
            }
        }
    }

    // MapEnabledArea leaves out the escapes; the map opens wherever warping works
    private static bool MapOpensHere() {
        return !RandomizerMapWarp.Disabled() || WorldMapLogic.Instance.MapEnabledArea.FindFaceAtPositionFaster(Characters.Sein.Position) != null;
    }

    public void DoMenuKeyPress() {
        if (MainMenuVisible) {
            HideMenuScreen(false);
            Core.Input.Cancel.Used = true;
            Core.Input.Start.Used = true;
        } else if (CanOpenMenus()) {
            Core.Input.Cancel.Used = true;
            Core.Input.Start.Used = true;
            ShowInventoryOrPauseMenu();
        }
    }

    public void ShowInventoryOrPauseMenu() {
        if (InventoryManager.Instance) {
            ShowInventory();
        } else {
            ShowMenuScreen(false);
        }
    }

    private void Show(MenuScreen menuScreen, Screens screen) {
        if (menuScreen) {
            if (CurrentScreen == screen) {
                menuScreen.Show();
            } else {
                menuScreen.Hide();
            }
        }
    }

    public void ChangeScreen(Screens screen) {
        CurrentScreen = screen;
        Show(SkillTreeManager.Instance, Screens.SkillTree);
        Show(GameMapUI.Instance, Screens.WorldMap);
        Show(OptionsScreen.Instance, Screens.Options);
        Show(InventoryManager.Instance, Screens.Inventory);
        Show(PauseScreen.Instance, Screens.Pause);
    }

    public void ShowMenuScreen(bool immediate = false) {
        UI.Hints.HideExistingHint();
        ShowMenuScreen(Screens.Pause, true);
    }

    public bool ResumeScreenVisible => m_resumeScreen != null;

    public void ShowResumeScreen() {
        if (ResumeScreenVisible) {
            return;
        }

        UI.Hints.HideExistingHint();
        var gameObject = (GameObject)InstantiateUtility.Instantiate(ResumeScreen, Vector3.zero, Quaternion.identity);
        m_resumeScreen = gameObject.GetComponent<ResumeGameController>();
        XboxOneSession.PauseSession();
        if (!MainMenuVisible) {
            HideWhenMainMenuOpen.OnMenuShow();
        }
    }

    public void HideResumeScreen() {
        if (m_resumeScreen) {
            XboxOneSession.ResumeSession();
            m_resumeScreen.Hide();
            m_resumeScreen = null;
            PlayOpenSound(CurrentScreen, false);
            if (!MainMenuVisible) {
                UberPostProcess.Instance.SetDoBlur(false);
                HideWhenMainMenuOpen.OnMenuHide();
            }
        }
    }

    public void ShowInventory() {
        LeaderboardsController.UploadScore();
        UI.Hints.HideExistingHint();
        ShowMenuScreen(Screens.Inventory, true);
    }

    public void ShowOptions() {
        UI.Hints.HideExistingHint();
        ShowMenuScreen(Screens.Options, true);
    }

    public void ShowAreaMap() {
        UI.Hints.HideExistingHint();
        ShowMenuScreen(Screens.WorldMap, true);
        AreaMapUI.Instance.Navigation.CenterMapOnWorldPosition(Characters.Sein.Position);
        GameMapTransitionManager.Instance.GoToAreaMapInstantly();
    }

    public void ShowWorldMap(bool playOpenSound = true) {
        UI.Hints.HideExistingHint();
        ShowMenuScreen(Screens.WorldMap, playOpenSound);
        GameMapTransitionManager.Instance.GoToWorldMapInstantly();
    }

    public void ShowObjective(Objective objective, Action method) {
        GameMapUI.Instance.ShowObjective.ShowObjective(objective, method);
    }

    public void ShowSkillTree() {
        UI.Hints.HideExistingHint();
        ShowMenuScreen(Screens.SkillTree, true);
        Core.Input.Cancel.Used = true;
    }

    private void ShowMenuScreen(Screens screen, bool playOpenSound = true) {
        if (!MainMenuVisible) {
            UberPostProcess.Instance.SetDoBlur(true);
            MainMenuVisible = true;
            m_suspendables = SuspensionManager.GetSuspendables(m_suspendables, true, gameObject);
            SuspensionManager.SuspendExcluding(m_suspendables);
            Events.Scheduler.OnMenuOpen.Call();
            XboxOneSession.PauseSession();
        }

        ChangeScreen(screen);
        if (playOpenSound) {
            PlayOpenSound(CurrentScreen, true);
        }

        HideWhenMainMenuOpen.OnMenuShow();
    }

    public void HideMenuScreen(bool immediate = false) {
        if (LockClosingMenu) {
            return;
        }

        if (CameraCrossFadeManager.CrossFadeMenuHack != MoonGuid.Empty) {
            if (!Scenes.Manager.SceneIsLoaded(CameraCrossFadeManager.CrossFadeMenuHack)) {
                return;
            }

            CameraCrossFadeManager.CrossFadeMenuHack = MoonGuid.Empty;
        }

        if (!MainMenuVisible) {
            return;
        }

        MainMenuVisible = false;
        Events.Scheduler.OnMenuClose.Call();
        try {
            if (immediate) {
                switch (CurrentScreen) {
                    case Screens.Pause:
                        PauseScreen.Instance.HideImmediate();
                        break;
                    case Screens.SkillTree:
                        SkillTreeManager.Instance.HideImmediate();
                        break;
                    case Screens.WorldMap:
                        GameMapUI.Instance.HideImmediate();
                        break;
                    case Screens.Options:
                        OptionsScreen.Instance.HideImmediate();
                        break;
                    case Screens.Inventory:
                        InventoryManager.Instance.HideImmediate();
                        break;
                }
            } else {
                switch (CurrentScreen) {
                    case Screens.Pause:
                        PauseScreen.Instance.Hide();
                        break;
                    case Screens.SkillTree:
                        SkillTreeManager.Instance.Hide();
                        break;
                    case Screens.WorldMap:
                        GameMapUI.Instance.Hide();
                        break;
                    case Screens.Options:
                        OptionsScreen.Instance.Hide();
                        break;
                    case Screens.Inventory:
                        InventoryManager.Instance.Hide();
                        break;
                }
            }

            XboxOneSession.ResumeSession();
        } catch (Exception) {
        }

        if (!immediate) {
            PlayOpenSound(CurrentScreen, false);
        }

        SuspensionManager.ResumeExcluding(m_suspendables);
        m_suspendables.Clear();
        UberPostProcess.Instance.SetDoBlur(false);
        HideWhenMainMenuOpen.OnMenuHide();
    }

    public void PlayOpenSound(Screens screen, bool open) {
        switch (screen) {
            case Screens.Pause:
                Sound.Play(!open ? CloseSound.GetSound(null) : OpenSound.GetSound(null), transform.position, null);
                break;
            case Screens.SkillTree:
                if (SkillTreeManager.Instance) {
                    Sound.Play(!open ? SkillTreeManager.Instance.CloseSound.GetSound(null) : SkillTreeManager.Instance.OpenSound.GetSound(null), transform.position, null);
                }

                break;
            case Screens.WorldMap:
                if (AreaMapUI.Instance) {
                    if (GameMapUI.Instance.ShowingTeleporters) {
                        if (!open) {
                            Sound.Play(GameMapUI.Instance.Teleporters.CloseWindowSound.GetSound(null), Vector3.zero, null);
                        }
                    } else {
                        Sound.Play(!open ? AreaMapUI.Instance.CloseSound.GetSound(null) : AreaMapUI.Instance.OpenSound.GetSound(null), transform.position, null);
                    }
                }

                break;
            case Screens.Options:
                if (OptionsScreen.Instance) {
                    Sound.Play(!open ? OptionsScreen.Instance.CloseSound.GetSound(null) : OptionsScreen.Instance.OpenSound.GetSound(null), transform.position, null);
                }

                break;
            case Screens.Inventory:
                if (InventoryManager.Instance) {
                    Sound.Play(!open ? InventoryManager.Instance.CloseSound.GetSound(null) : InventoryManager.Instance.OpenSound.GetSound(null), transform.position, null);
                }

                break;
        }
    }

    public override bool IsSuspended { get; set; }

    public bool MainMenuVisible {
        get {
            return m_isPaused;
        }
        set {
            m_isPaused = value;
        }
    }

    public GameObject Instantiate(GameObject prefab) {
        var gameObject = (GameObject)UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        gameObject.name = prefab.name;
        SaveSceneManager.Master.RegisterGameObject(gameObject);
        gameObject.transform.parent = transform;
        gameObject.SetActive(false);
        gameObject.AddComponent<LoadFromMasterAtStart>();
        return gameObject;
    }

    public void SetupWorldMapUI(GameObject worldMapUI) {
        if (GameMapUI.Instance == null) {
            var gameObject = Instantiate(worldMapUI);
            Utility.DontAssociateWithAnyScene(gameObject);
        }
    }

    public void SetupSkillTreeUI(GameObject skillTreeUIPrefab) {
        if (SkillTreeManager.Instance == null) {
            var gameObject = Instantiate(skillTreeUIPrefab);
            Utility.DontAssociateWithAnyScene(gameObject);
        }
    }

    public void SetupOptionsScreen(GameObject optionsPrefab) {
        if (OptionsScreen.Instance == null) {
            var gameObject = Instantiate(optionsPrefab);
            Utility.DontAssociateWithAnyScene(gameObject);
        }
    }

    public void SetupPauseScreen(GameObject pauseScreenPrefab) {
        if (PauseScreen.Instance == null) {
            var gameObject = Instantiate(pauseScreenPrefab);
            Utility.DontAssociateWithAnyScene(gameObject);
        }
    }

    public void SetupInventoryUI(GameObject inventoryManager) {
        if (InventoryManager.Instance == null) {
            var gameObject = Instantiate(inventoryManager);
            Utility.DontAssociateWithAnyScene(gameObject);
        }
    }

    public void RemoveGameplayObjects() {
        if (InventoryManager.Instance) {
            InstantiateUtility.Destroy(InventoryManager.Instance.gameObject);
        }

        if (SkillTreeManager.Instance) {
            InstantiateUtility.Destroy(SkillTreeManager.Instance.gameObject);
        }

        if (GameMapUI.Instance) {
            InstantiateUtility.Destroy(GameMapUI.Instance.gameObject);
        }
    }

    public GameObject ResumeScreen;

    public GameObject OptionsPrefab;

    public GameObject PausePrefab;

    public Screens CurrentScreen;

    public SoundProvider OpenSound;

    public SoundProvider CloseSound;

    private HashSet<ISuspendable> m_suspendables = new HashSet<ISuspendable>();

    private ResumeGameController m_resumeScreen;

    public bool LockClosingMenu;

    private bool m_isPaused;

    public enum Screens {
        Pause,
        SkillTree,
        WorldMap,
        Options,
        Inventory
    }
}

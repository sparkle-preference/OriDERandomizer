using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Core;
using Game;
using Sein.World;
using UnityEngine;
using Events = Sein.World.Events;
using Input = Core.Input;

public class DebugMenuB : SaveSerialize {
    public static void MakeDebugMenuExist() {
        if (Instance == null) {
            var gameObject = Resources.Load<GameObject>("debugMenu");
            var gameObject2 = Instantiate(gameObject);
            Utility.DontAssociateWithAnyScene(gameObject2);
            gameObject2.name = gameObject.name;
        }
    }

    public static void ToggleDebugMenu() {
        MakeDebugMenuExist();
        if (Active) {
            if (Instance) {
                Instance.HideDebugMenu();
            }
        } else if (Instance) {
            Instance.ShowDebugMenu();
        }
    }

    public void ShowDebugMenu() {
        if (Active) {
            return;
        }

        Active = true;
        SuspendGameplay();
    }

    public void HideDebugMenu() {
        if (!Active) {
            return;
        }

        Active = false;
        ResumeGameplay();
    }

    public void Start() {
    }

    private static void SuspendGameplay() {
        SuspensionManager.GetSuspendables(SuspendablesToIgnoreForGameplay, UI.Cameras.Current.GameObject);
        SuspensionManager.SuspendExcluding(SuspendablesToIgnoreForGameplay);
    }

    private static void ResumeGameplay() {
        SuspensionManager.ResumeExcluding(SuspendablesToIgnoreForGameplay);
        SuspendablesToIgnoreForGameplay.Clear();
    }

    public override void Awake() {
        Instance = this;
        if (ImportantLevels.Count > 0) {
        }

        base.Awake();
        Style = Skin.FindStyle("debugMenuItem");
        SelectedStyle = Skin.FindStyle("selectedDebugMenuItem");
        PressedStyle = Skin.FindStyle("pressedDebugMenuItem");
        DebugMenuStyle = Skin.FindStyle("debugMenu");
        StyleEnabled = Skin.FindStyle("debugMenuItemEnabled");
        StyleDisabled = Skin.FindStyle("debugMenuItemDisabled");
    }

    public bool ReinstantiateOri() {
        var position = Characters.Current.Position;
        CharacterFactory.Instance.DestroyCharacter();
        CharacterFactory.Instance.SpawnCharacter(CharacterFactory.Characters.Sein, null, position, null);
        LateStartHook.AddLateStartMethod(delegate { Characters.Sein.Position = position; });
        return true;
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref m_cursorIndex);
        ar.Serialize(ref m_showGumoSequences);
        ar.Serialize(ref m_gumoSequencesCursorIndex);
        ar.Serialize(ref DebugControlsEnabled);
        ar.Serialize(ref MuteMusic);
        ar.Serialize(ref MuteAmbience);
        ar.Serialize(ref MuteSoundEffects);
        if (ar.Reading) {
            var flag = false;
            ar.Serialize(ref flag);
            if (flag == Active) {
                return;
            }

            Active = flag;
            if (Active) {
                ShowDebugMenu();
            } else {
                HideDebugMenu();
            }
        } else {
            ar.Serialize(ref Active);
        }
    }

    public bool SendLeaderboard() {
        LeaderboardsController.UploadScores();
        return true;
    }

    public bool LoadTestScene() {
        GoToSceneController.Instance.GoToScene(TestScene, null, false);
        return true;
    }

    [Conditional("DEVELOPMENT_BUILD")]
    public void SendOneSteamTelemetry() {
        SendSteamTelemetry(1);
    }

    [Conditional("DEVELOPMENT_BUILD")]
    public void SendTenSteamTelemetry() {
        SendSteamTelemetry(10);
    }

    public void SendSteamTelemetry(int repetition) {
        for (var i = 0; i < repetition; i++) {
            var stringData = new SteamTelemetry.StringData("test #" + i);
            SteamTelemetry.Instance.Send(TelemetryEvent.Test, stringData.ToString());
        }
    }

    public bool DisableArt() {
        if (m_art == null) {
            m_art = (from a in FindObjectsOfType<GameObject>()
                where a.name == "art"
                select a).ToArray();
            foreach (var gameObject in m_art) {
                if (gameObject) {
                    gameObject.SetActive(false);
                }
            }
        } else {
            foreach (var gameObject2 in m_art) {
                if (gameObject2) {
                    gameObject2.SetActive(true);
                }
            }

            m_art = null;
        }

        return true;
    }

    public bool PrintReadableTextures() {
        using (var streamWriter = new StreamWriter("texturesYouCanWriteTo.txt")) {
            foreach (Texture2D texture2D in Resources.FindObjectsOfTypeAll(typeof(Texture2D))) {
                try {
                    texture2D.GetPixel(0, 0);
                    streamWriter.WriteLine(texture2D.name);
                } catch (Exception ex) {
                }
            }
        }

        return true;
    }

    public bool DisableEnemies() {
        if (m_enemies == null) {
            m_enemies = (from a in FindObjectsOfType<GameObject>()
                where a.name == "enemies"
                select a).ToArray();
            foreach (var gameObject in m_enemies) {
                if (gameObject) {
                    gameObject.SetActive(false);
                }
            }
        } else {
            foreach (var gameObject2 in m_enemies) {
                if (gameObject2) {
                    gameObject2.SetActive(true);
                }
            }

            m_enemies = null;
        }

        return true;
    }

    public bool DisableAllParticles() {
        if (m_particleSystems == null) {
            m_particleSystems = new List<GameObject>();
            var array = FindObjectsOfType<ParticleSystem>();
            foreach (var particleSystem in array) {
                if (particleSystem) {
                    particleSystem.gameObject.SetActive(false);
                    m_particleSystems.Add(particleSystem.gameObject);
                }
            }

            var array3 = FindObjectsOfType<ParticleEmitter>();
            foreach (var particleEmitter in array3) {
                if (particleEmitter) {
                    particleEmitter.gameObject.SetActive(false);
                    m_particleSystems.Add(particleEmitter.gameObject);
                }
            }

            InstantiateUtility.DisableParticles = true;
        } else {
            var array5 = FindObjectsOfType<ParticleSystem>();
            foreach (var particleSystem2 in array5) {
                if (particleSystem2) {
                    particleSystem2.gameObject.SetActive(true);
                    m_particleSystems.Add(particleSystem2.gameObject);
                }
            }

            var array7 = FindObjectsOfType<ParticleEmitter>();
            foreach (var particleEmitter2 in array7) {
                if (particleEmitter2) {
                    particleEmitter2.gameObject.SetActive(true);
                    m_particleSystems.Add(particleEmitter2.gameObject);
                }
            }

            m_particleSystems = null;
            InstantiateUtility.DisableParticles = false;
        }

        return true;
    }

    private void BuildMenu() {
        MenuWidth = Screen.width - MenuTopLeftX * 2f;
        MenuHeight = Screen.height - MenuTopLeftY * 2f - VerticalSpace - 30f;
        ShouldShowOnlySelectedItem = false;
        m_menuList.Clear();
        var list = new List<IDebugMenuItem>();
        list.Add(new ActionDebugMenuItem("Save", SaveGame));
        list.Add(new ActionDebugMenuItem("Load", LoadGame));
        list.Add(new ActionDebugMenuItem("Restore Checkpoint", RestoreCheckpoint));
        list.Add(new ActionDebugMenuItem("Instantiate Ori", ReinstantiateOri));
        list.Add(new ActionDebugMenuItem("Activate teleporters", TeleporterController.ActivateAll));
        list.Add(new ActionDebugMenuItem("Unlock Difficulties", UnlockDifficulties));
        if (SkipCutsceneController.Instance.SkippingAvailable) {
            list.Add(new ActionDebugMenuItem("Skipping Available", SkipAction));
        }

        list.Add(new BoolDebugMenuItem("Cheats", CheatsGetter, CheatsSetter));
        list.Add(new BoolDebugMenuItem("Disable Sound", () => Sound.AllSoundsDisabled, delegate(bool val) { Sound.AllSoundsDisabled = val; }));
        list.Add(new BoolDebugMenuItem("Unlock Cutscenes", () => UnlockAllCutscenes, delegate(bool value) { UnlockAllCutscenes = value; }));
        list.Add(
            new BoolDebugMenuItem(
                "Frame Performance Monitor",
                () => FramePerformanceMonitor.Enabled,
                delegate(bool val) {
                    SceneFrameworkPerformanceMonitor.Enabled = val;
                    FramePerformanceMonitor.Enabled = val;
                }
            )
        );
        list.Add(new BoolDebugMenuItem("Binary Profiler Log", () => BinaryProfilerLogMaker.Enabled, delegate(bool val) { BinaryProfilerLogMaker.Enabled = val; }));
        list.Add(new BoolDebugMenuItem("Leaked Objects Detector", () => LeakedSceneObjectDetector.Enabled, delegate(bool val) { LeakedSceneObjectDetector.Enabled = val; }));
        list.Add(new BoolDebugMenuItem("UberShader Detector", () => UberShaderDetector.Enabled, delegate(bool val) { UberShaderDetector.Enabled = val; }));
        list.Add(new BoolDebugMenuItem("Debug Controls", DebugControlsGetter, DebugControlsSetter));
        list.Add(new BoolDebugMenuItem("Debug text", DebugTextGetter, DebugTextSetter));
        list.Add(new BoolDebugMenuItem("Scene Framework", DebugSceneFrameworkGetter, DebugSceneFrameworkSetter));
        list.Add(new BoolDebugMenuItem("Xbox Controller", DebugXboxControllerGetter, DebugXboxControllerSetter));
        list.Add(new BoolDebugMenuItem("Visual Log", VisualLogGetter, VisualLogSetter));
        list.Add(new BoolDebugMenuItem("Log Callback Hook", LogCallbackHookGetter, LogCallbackHookSetter));
        list.Add(new BoolDebugMenuItem("Fixed Update Sync Debug", FixedUpdateSyncGetter, FixedUpdateSyncSetter));
        list.Add(new ActionDebugMenuItem("Print Object report", YouCanLeaveYourHatOn.DebugMenuPrintReport));
        list.Add(new ActionDebugMenuItem("Disable particles", DisableAllParticles));
        list.Add(new ActionDebugMenuItem("Disable art", DisableArt));
        list.Add(new ActionDebugMenuItem("Disable enemies", DisableEnemies));
        list.Add(new ActionDebugMenuItem("Print readable textures", PrintReadableTextures));
        list.Add(new GarbageRunner());
        list.Add(new ActionDebugMenuItem("Reset Steam Stats", ResetSteamStats));
        list.Add(new ActionDebugMenuItem("Reset Input Lock", ResetInputLock));
        if (Characters.Sein) {
            list.Add(new ActionDebugMenuItem("Reset berry position", ResetNightBerryPosition));
            list.Add(new ActionDebugMenuItem("Teleport Nightberry", TeleportNightberry));
            list.Add(new ActionDebugMenuItem("Visit all spots in current area", VisitAllAreas));
        }

        list.Add(new BoolDebugMenuItem("See Achievement Hint", AchievementHintGetter, AchievementHintSetter));
        list.Add(new ActionDebugMenuItem("Gumo Sequences", GumoSequencesAction));
        list.Add(new ActionDebugMenuItem("Quit", Quit));
        var list2 = new List<IDebugMenuItem>();
        list2.Add(new TimeScaleDebugMenuItem("Time Scale"));
        list2.Add(new ZoomDebugMenuItem("Zoom"));
        list2.Add(new GlobalDebugQuadScaleMenuItem("Quad scale"));
        list2.Add(
            new BoolDebugMenuItem(
                "Super Slow Motion",
                () => m_superSlowMotion,
                delegate(bool val) {
                    m_superSlowMotion = val;
                    Time.timeScale = !val ? 1f : 0.25f;
                }
            )
        );
        list2.Add(new BoolDebugMenuItem("Sync fixed update", () => SyncFramesTest.EnableSync, delegate(bool val) { SyncFramesTest.EnableSync = val; }));
        list2.Add(new BoolDebugMenuItem("force fixed update", () => SyncFramesTest.EnabledForceFixedUpdate, delegate(bool val) { SyncFramesTest.EnabledForceFixedUpdate = val; }));
        if (Characters.Sein) {
            list2.Add(new SeinLevelUpDownDebugMenuItem("Level"));
            list2.Add(new SeinSkillUpDownDebugMenuItem("Skill Points"));
            list2.Add(new LeafsDebugMenuItem("Door Leafs"));
            list2.Add(new MapStonesDebugMenuItem("Map Stones"));
            list2.Add(new HealthDebugMenuItem("Health"));
            list2.Add(new MaxHealthDebugMenuItem("Max Health"));
            list2.Add(new EnergyDebugMenuItem("Energy"));
            list2.Add(new MaxEnergyDebugMenuItem("Max Energy"));
        }

        var array = (MonoBehaviour[])FindObjectsOfType(typeof(MonoBehaviour));
        foreach (var monoBehaviour in array) {
            var debugMenuToggleable = monoBehaviour as IDebugMenuToggleable;
            if (debugMenuToggleable != null) {
                list2.Add(new DebugMenuTogglerItem(debugMenuToggleable));
            }
        }

        list2.Add(new BoolDebugMenuItem("Deactivate Darkness", DeactivateDarknessGetter, DeactivateDarknessSetter));
        list2.Add(new BoolDebugMenuItem("Camera", CameraEnabledGetter, CameraEnabledSetter));
        list2.Add(new BoolDebugMenuItem("Music", DebugMuteMusicGetter, DebugMuteMusicSetter));
        list2.Add(new BoolDebugMenuItem("Ambience", DebugMuteAmbienceGetter, DebugMuteAmbienceSetter));
        list2.Add(new BoolDebugMenuItem("Sound Effects", DebugMuteSoundEffectsGetter, DebugMuteSoundEffectsSetter));
        list2.Add(new BoolDebugMenuItem("Sound Log", ShowSoundLogGetter, ShowSoundLogSetter));
        list2.Add(new BoolDebugMenuItem("Pink Boxes", ShowPinkBoxesGetter, ShowPinkBoxesSetter));
        list2.Add(new BoolDebugMenuItem("UI", SeinUIGetter, SeinUISetter));
        list2.Add(new BoolDebugMenuItem("Damage Text", SeinDamageTextGetter, SeinDamageTextSetter));
        list2.Add(new BoolDebugMenuItem("120fps Physics", HighFPSPhysicsGetter, HighFPSPhysicsSetter));
        list2.Add(new BoolDebugMenuItem("Invincibility", SeinInvincibilityGetter, SeinInvincibilitySetter));
        list2.Add(new BoolDebugMenuItem("Replay Engine", ReplayEngineActiveGetter, ReplayEngineActiveSetter));
        list2.Add(new ActionDebugMenuItem("Send leaderboard", SendLeaderboard));
        list2.Add(new ActionDebugMenuItem("Load Test Scene", LoadTestScene));
        list2.Add(new BoolDebugMenuItem("Auto send leaderboard", () => LeaderboardsController.AutoUpload, delegate(bool v) { LeaderboardsController.AutoUpload = v; }));
        var list3 = new List<IDebugMenuItem>();
        foreach (var text in ImportantLevelsNames) {
            var flag = false;
            foreach (var runtimeSceneMetaData in Scenes.Manager.AllScenes) {
                if (runtimeSceneMetaData.Scene == text) {
                    flag = true;
                    break;
                }
            }

            if (flag) {
                list3.Add(
                    new ActionDebugMenuItem(text, GoToScene) {
                        HelpText = "Press X or A to invoke teleport"
                    }
                );
            }
        }

        var list4 = new List<IDebugMenuItem>();
        foreach (var worldEvent in m_worldEvents) {
            list4.Add(new DebugMenuWorldEventActionMenuItem(worldEvent));
        }

        var list5 = new List<IDebugMenuItem>();
        var list6 = new List<IDebugMenuItem>();
        if (Characters.Sein) {
            list5.Add(new ActionDebugMenuItem("Remove all skills/abilities", AbilityDebugMenuItems.RemoveAllSkillsAndAbilities));
            list5.Add(new ActionDebugMenuItem("Remove all skills", AbilityDebugMenuItems.RemoveAllSkills));
            list5.Add(new BoolDebugMenuItem("Spirit Flame", AbilityDebugMenuItems.SpiritFlameGetter, AbilityDebugMenuItems.SpiritFlameSetter));
            list5.Add(new BoolDebugMenuItem("Wall Jump", AbilityDebugMenuItems.WallJumpGetter, AbilityDebugMenuItems.WallJumpSetter));
            list5.Add(new BoolDebugMenuItem("Charge Flame", AbilityDebugMenuItems.ChargeFlameGetter, AbilityDebugMenuItems.ChargeFlameSetter));
            list5.Add(new BoolDebugMenuItem("Double Jump", AbilityDebugMenuItems.DoubleJumpGetter, AbilityDebugMenuItems.DoubleJumpSetter));
            list5.Add(new BoolDebugMenuItem("Bash", AbilityDebugMenuItems.BashGetter, AbilityDebugMenuItems.BashSetter));
            list5.Add(new BoolDebugMenuItem("Stomp", AbilityDebugMenuItems.StompGetter, AbilityDebugMenuItems.StompSetter));
            list5.Add(new BoolDebugMenuItem("Glide", AbilityDebugMenuItems.GlideGetter, AbilityDebugMenuItems.GlideSetter));
            list5.Add(new BoolDebugMenuItem("Climb", AbilityDebugMenuItems.ClimbGetter, AbilityDebugMenuItems.ClimbSetter));
            list5.Add(new BoolDebugMenuItem("Charge Jump", AbilityDebugMenuItems.ChargeJumpGetter, AbilityDebugMenuItems.ChargeJumpSetter));
            list5.Add(new BoolDebugMenuItem("Dash", AbilityDebugMenuItems.DashGetter, AbilityDebugMenuItems.DashSetter));
            list5.Add(new BoolDebugMenuItem("Grenade", AbilityDebugMenuItems.GrenadeGetter, AbilityDebugMenuItems.GrenadeSetter));
            list6.Add(new ActionDebugMenuItem("Remove all abilities", AbilityDebugMenuItems.RemoveAllAbilities));
            list6.Add(new ActionDebugMenuItem("Remove all blue abilities", AbilityDebugMenuItems.RemoveAllBlueAbilities));
            list6.Add(new BoolDebugMenuItem("Rekindle", AbilityDebugMenuItems.RekindleGetter, AbilityDebugMenuItems.RekindleSetter));
            list6.Add(new BoolDebugMenuItem("Regroup", AbilityDebugMenuItems.RegroupGetter, AbilityDebugMenuItems.RegroupSetter));
            list6.Add(new BoolDebugMenuItem("Charge Flame Efficiency", AbilityDebugMenuItems.ChargeFlameEfficiencyGetter, AbilityDebugMenuItems.ChargeFlameEfficiencySetter));
            list6.Add(new BoolDebugMenuItem("Air Dash", AbilityDebugMenuItems.AirDashGetter, AbilityDebugMenuItems.AirDashSetter));
            list6.Add(new BoolDebugMenuItem("Ultra Soul Link", AbilityDebugMenuItems.UltraSoulFlameGetter, AbilityDebugMenuItems.UltraSoulFlameSetter));
            list6.Add(new BoolDebugMenuItem("Charge Dash", AbilityDebugMenuItems.ChargeDashGetter, AbilityDebugMenuItems.ChargeDashSetter));
            list6.Add(new BoolDebugMenuItem("Water Breath", AbilityDebugMenuItems.WaterBreathGetter, AbilityDebugMenuItems.WaterBreathSetter));
            list6.Add(new BoolDebugMenuItem("Soul Link Efficiency", AbilityDebugMenuItems.SoulFlameEfficiencyGetter, AbilityDebugMenuItems.SoulFlameEfficiencySetter));
            list6.Add(new BoolDebugMenuItem("Triple Jump", AbilityDebugMenuItems.DoubleJumpUpgradeGetter, AbilityDebugMenuItems.DoubleJumpUpgradeSetter));
            list6.Add(new BoolDebugMenuItem("Ultra Defense", AbilityDebugMenuItems.UltraDefenseGetter, AbilityDebugMenuItems.UltraDefenseSetter));
            list6.Add(new ActionDebugMenuItem("Remove all purple abilities", AbilityDebugMenuItems.RemoveAllPurpleAbilities));
            list6.Add(new BoolDebugMenuItem("Magnet", AbilityDebugMenuItems.MagnetGetter, AbilityDebugMenuItems.MagnetSetter));
            list6.Add(new BoolDebugMenuItem("Map Markers", AbilityDebugMenuItems.MapMarkersGetter, AbilityDebugMenuItems.MapMarkersSetter));
            list6.Add(new BoolDebugMenuItem("Health Efficiency", AbilityDebugMenuItems.HealthEfficiencyGetter, AbilityDebugMenuItems.HealthEfficiencySetter));
            list6.Add(new BoolDebugMenuItem("Ultra Magnet", AbilityDebugMenuItems.UltraMagnetGetter, AbilityDebugMenuItems.UltraMagnetSetter));
            list6.Add(new BoolDebugMenuItem("Energy Efficiency", AbilityDebugMenuItems.EnergyEfficiencyGetter, AbilityDebugMenuItems.EnergyEfficiencySetter));
            list6.Add(new BoolDebugMenuItem("Spirit Efficiency", AbilityDebugMenuItems.AbilityMarkersGetter, AbilityDebugMenuItems.AbilityMarkersSetter));
            list6.Add(new BoolDebugMenuItem("Spirit Potency", AbilityDebugMenuItems.SoulEfficiencyGetter, AbilityDebugMenuItems.SoulEfficiencySetter));
            list6.Add(new BoolDebugMenuItem("Health Recovery", AbilityDebugMenuItems.HealthMarkersGetter, AbilityDebugMenuItems.HealthMarkersSetter));
            list6.Add(new BoolDebugMenuItem("Energy Recovery", AbilityDebugMenuItems.EnergyMarkersGetter, AbilityDebugMenuItems.EnergyMarkersSetter));
            list6.Add(new BoolDebugMenuItem("Sense Items", AbilityDebugMenuItems.SenseGetter, AbilityDebugMenuItems.SenseSetter));
            list6.Add(new ActionDebugMenuItem("Remove all red abilities", AbilityDebugMenuItems.RemoveAllRedAbilities));
            list6.Add(new BoolDebugMenuItem("Quick Flame", AbilityDebugMenuItems.QuickFlameGetter, AbilityDebugMenuItems.QuickFlameSetter));
            list6.Add(new BoolDebugMenuItem("Spark Flame", AbilityDebugMenuItems.SparkFlameGetter, AbilityDebugMenuItems.SparkFlameSetter));
            list6.Add(new BoolDebugMenuItem("Charge Flame Burn", AbilityDebugMenuItems.ChargeFlameBurnGetter, AbilityDebugMenuItems.ChargeFlameBurnSetter));
            list6.Add(new BoolDebugMenuItem("Split Flame", AbilityDebugMenuItems.SplitFlameUpgradeGetter, AbilityDebugMenuItems.SplitFlameUpgradeSetter));
            list6.Add(new BoolDebugMenuItem("Ultra Light Burst", AbilityDebugMenuItems.GrenadeUpgradeGetter, AbilityDebugMenuItems.GrenadeUpgradeSetter));
            list6.Add(new BoolDebugMenuItem("Cinder Flame", AbilityDebugMenuItems.CinderFlameGetter, AbilityDebugMenuItems.CinderFlameSetter));
            list6.Add(new BoolDebugMenuItem("Ultra Stomp", AbilityDebugMenuItems.StompUpgradeGetter, AbilityDebugMenuItems.StompUpgradeSetter));
            list6.Add(new BoolDebugMenuItem("Rapid Flame", AbilityDebugMenuItems.RapidFireGetter, AbilityDebugMenuItems.RapidFireSetter));
            list6.Add(new BoolDebugMenuItem("Charge Flame Blast", AbilityDebugMenuItems.ChargeFlameBlastGetter, AbilityDebugMenuItems.ChargeFlameBlastSetter));
            list6.Add(new BoolDebugMenuItem("Ultra Split Flame", AbilityDebugMenuItems.UltraSplitFlameGetter, AbilityDebugMenuItems.UltraSplitFlameSetter));
        }

        var list7 = new List<IDebugMenuItem>();
        if (!XboxLiveController.IsContentPackage) {
            list7.Add(new ActionDebugMenuItem("Start FPS Test 0", StartFPSTest0));
            list7.Add(new ActionDebugMenuItem("Start FPS Test 60", StartFPSTest60));
            list7.Add(new ActionDebugMenuItem("Start FPS Test 120", StartFPSTest120));
            list7.Add(new ActionDebugMenuItem("Start FPS Test 180", StartFPSTest180));
            list7.Add(new ActionDebugMenuItem("Start FPS Test 240", StartFPSTest240));
            list7.Add(new BoolDebugMenuItem("Override Misty Woods Conditions", () => SceneFPSTest.OVERRIDE_MISTYWOODS_CONDITION, delegate(bool val) { SceneFPSTest.OVERRIDE_MISTYWOODS_CONDITION = val; }));
            list7.Add(new BoolDebugMenuItem("FPS Test Reverse IsCutscene", () => SceneFPSTest.HACK_REVERSE_ISCUTSCENE, delegate(bool val) { SceneFPSTest.HACK_REVERSE_ISCUTSCENE = val; }));
            list7.Add(new BoolDebugMenuItem("Screenshot", () => SceneFPSTest.SHOULD_CREATE_SCREENSHOT, delegate(bool val) { SceneFPSTest.SHOULD_CREATE_SCREENSHOT = val; }));
            list7.Add(new BoolDebugMenuItem("Memory Report", () => SceneFPSTest.SHOULD_CREATE_MEMORY_REPORT, delegate(bool val) { SceneFPSTest.SHOULD_CREATE_MEMORY_REPORT = val; }));
            list7.Add(new BoolDebugMenuItem("Basic Sample", () => SceneFPSTest.SHOULD_RUN_SAMPLE, delegate(bool val) { SceneFPSTest.SHOULD_RUN_SAMPLE = val; }));
            list7.Add(new BoolDebugMenuItem("No Camera", () => SceneFPSTest.SHOULD_RUN_CPU_SAMPLE, delegate(bool val) { SceneFPSTest.SHOULD_RUN_CPU_SAMPLE = val; }));
            list7.Add(new BoolDebugMenuItem("Quad Scale 0", () => SceneFPSTest.SHOULD_RUN_CPU_B_SAMPLE, delegate(bool val) { SceneFPSTest.SHOULD_RUN_CPU_B_SAMPLE = val; }));
            list7.Add(new BoolDebugMenuItem("Draw Debug UI", () => SceneFPSTest.DRAW_DEBUG_UI, delegate(bool val) { SceneFPSTest.DRAW_DEBUG_UI = val; }));
        }

        list7.Add(new BoolDebugMenuItem("Streaming Install Debug Override", StreamingInstallDebugGetter, StreamingInstallDebugSetter));
        list7.Add(new ActionDebugMenuItem("Break Telem URL", BreakSteamTelemetryURL));
        list7.Add(new ActionDebugMenuItem("Set Telemetry to UPF", SetSteamTelemetryURLToUPF));
        var list8 = new List<IDebugMenuItem>();
        list8.Add(new BoolDebugMenuItem("Clean Water", CleanWaterGetter, CleanWaterSetter));
        list8.Add(new BoolDebugMenuItem("Wind Released", WindReleasedGetter, WindReleasedSetter));
        list8.Add(new BoolDebugMenuItem("Gumo Free", GumoFreeGetter, GumFreeSetter));
        list8.Add(new BoolDebugMenuItem("Forlorn Energy Restored", ForlornEnergyRestoredGetter, ForlornEnergyRestoredSetter));
        list8.Add(new BoolDebugMenuItem("Mist Lifted", MistLiftedGetter, MistLiftedSetter));
        list8.Add(new BoolDebugMenuItem("Ginso Key", GinsoKeyGetter, GinsoKeySetter));
        list8.Add(new BoolDebugMenuItem("Forlorn Ruins Key", ForlornRuinsKeyGetter, ForlornRuinsKeySetter));
        list8.Add(new BoolDebugMenuItem("Horu Key", HoruKeyGetter, HoruKeySetter));
        list8.Add(new BoolDebugMenuItem("Darkness Lifted", DarknessLiftedGetter, DarknessLiftedSetter));
        if (GameController.Instance.IsTrial) {
            list5.Clear();
            list6.Clear();
            list7.Clear();
            list8.Clear();
        }

        if (list.Count > 0) {
            m_menuList.Add(list);
        }

        if (list2.Count > 0) {
            m_menuList.Add(list2);
        }

        if (list3.Count > 0) {
            m_menuList.Add(list3);
        }

        if (list4.Count > 0) {
            m_menuList.Add(list4);
        }

        if (list5.Count > 0) {
            m_menuList.Add(list5);
        }

        if (list6.Count > 0) {
            m_menuList.Add(list6);
        }

        if (list7.Count > 0) {
            m_menuList.Add(list7);
        }

        if (list8.Count > 0) {
            m_menuList.Add(list8);
        }

        m_showGumoSequences = false;
        var num = 8;
        m_gumoSequencesMenuList.Clear();
        var list9 = new List<IDebugMenuItem>();
        var list10 = new List<IDebugMenuItem>();
        var list11 = new List<IDebugMenuItem>();
        var list12 = new List<IDebugMenuItem>();
        var list13 = new List<IDebugMenuItem>();
        foreach (var goToSequenceData in GumoSequence) {
            if (goToSequenceData.Scene) {
            }

            if (list9.Count < num) {
                list9.Add(new GoToSequenceMenuItem(goToSequenceData));
            } else if (list10.Count < num) {
                list10.Add(new GoToSequenceMenuItem(goToSequenceData));
            } else if (list11.Count < num) {
                list11.Add(new GoToSequenceMenuItem(goToSequenceData));
            } else if (list12.Count < num) {
                list12.Add(new GoToSequenceMenuItem(goToSequenceData));
            } else {
                list13.Add(new GoToSequenceMenuItem(goToSequenceData));
            }
        }

        if (list9.Count > 0) {
            m_gumoSequencesMenuList.Add(list9);
        }

        if (list10.Count > 0) {
            m_gumoSequencesMenuList.Add(list10);
        }

        if (list11.Count > 0) {
            m_gumoSequencesMenuList.Add(list11);
        }

        if (list12.Count > 0) {
            m_gumoSequencesMenuList.Add(list12);
        }

        if (list13.Count > 0) {
            m_gumoSequencesMenuList.Add(list13);
        }
    }

    private bool UnlockDifficulties() {
        GameSettings.Instance.OneLifeModeUnlocked = true;
        GameSettings.Instance.SaveSettings();
        return true;
    }

    private bool BreakSteamTelemetryURL() {
        SteamTelemetry.URL = "http://www.ssodifjsoifj.com";
        return true;
    }

    private bool SetSteamTelemetryURLToUPF() {
        SteamTelemetry.URL = "http://www.upf.co.il/steamTelemetryTest.php";
        return true;
    }

    private bool VisitAllAreas() {
        World.CurrentArea.VisitAllAreas();
        World.CurrentArea.UpdateCompletionAmount();
        return true;
    }

    private bool StreamingInstallDebugGetter() {
        return IsFullyInstalledDebugOverride;
    }

    private void StreamingInstallDebugSetter(bool value) {
        IsFullyInstalledDebugOverride = value;
    }

    private bool FixedUpdateSyncGetter() {
        return FixedUpdateSyncTracker.Enable;
    }

    private void FixedUpdateSyncSetter(bool value) {
        FixedUpdateSyncTracker.Enable = value;
    }

    private bool HighFPSPhysicsGetter() {
        return m_highFPSPhysics;
    }

    private void HighFPSPhysicsSetter(bool value) {
        m_highFPSPhysics = value;
        if (value) {
            Time.fixedDeltaTime = 0.008333334f;
        } else {
            Time.fixedDeltaTime = 0.016666668f;
        }
    }

    private bool LimitPhysicsIterationGetter() {
        return Mathf.Round(Time.maximumDeltaTime * 100f) == Mathf.Round(1.6666667f);
    }

    private void LimitPhysicsIterationSetter(bool obj) {
        Time.maximumDeltaTime = !obj ? 0.033333335f : 0.016666668f;
    }

    private bool StartFPSTest0() {
        SceneFPSTest.SetupTheTest();
        return true;
    }

    private bool StartFPSTest60() {
        SceneFPSTest.CurrentSceneMetaDataIndex = 60;
        SceneFPSTest.SetupTheTest();
        return true;
    }

    private bool StartFPSTest120() {
        SceneFPSTest.CurrentSceneMetaDataIndex = 120;
        SceneFPSTest.SetupTheTest();
        return true;
    }

    private bool StartFPSTest180() {
        SceneFPSTest.CurrentSceneMetaDataIndex = 180;
        SceneFPSTest.SetupTheTest();
        return true;
    }

    private bool StartFPSTest240() {
        SceneFPSTest.CurrentSceneMetaDataIndex = 240;
        SceneFPSTest.SetupTheTest();
        return true;
    }

    private bool ResetSteamStats() {
        if (Steamworks.Ready) {
            Steamworks.SteamInterface.Stats.ResetAllStats(true);
        }

        return true;
    }

    private void ForlornRuinsKeySetter(bool obj) {
        Keys.ForlornRuins = obj;
    }

    private bool ForlornRuinsKeyGetter() {
        return Keys.ForlornRuins;
    }

    private void GinsoKeySetter(bool obj) {
        Keys.GinsoTree = obj;
    }

    private bool GinsoKeyGetter() {
        return Keys.GinsoTree;
    }

    private void HoruKeySetter(bool obj) {
        Keys.MountHoru = obj;
    }

    private bool HoruKeyGetter() {
        return Keys.MountHoru;
    }

    public void AddWorldEvent(WorldEvents worldEvent) {
        if (m_worldEvents.Contains(worldEvent)) {
            return;
        }

        m_worldEvents.Add(worldEvent);
    }

    private bool WindReleasedGetter() {
        return Events.WindRestored;
    }

    private void WindReleasedSetter(bool released) {
        Events.WindRestored = released;
    }

    private bool DarknessLiftedGetter() {
        return Events.DarknessLifted;
    }

    private void DarknessLiftedSetter(bool isDarknessLifted) {
        Events.DarknessLifted = isDarknessLifted;
    }

    private bool LoadGame() {
        if (!GameController.Instance.SaveGameController.PerformLoad()) {
        }

        return true;
    }

    private bool SkipAction() {
        SkipCutsceneController.Instance.SkipCutscene();
        return true;
    }

    private bool SaveGame() {
        HideDebugMenu();
        GameController.Instance.CreateCheckpoint();
        GameController.Instance.SaveGameController.PerformSave();
        return true;
    }

    private void GumFreeSetter(bool obj) {
        Events.GumoFree = obj;
    }

    private bool GumoFreeGetter() {
        return Events.GumoFree;
    }

    private void ForlornEnergyRestoredSetter(bool obj) {
        Events.GravityActivated = obj;
    }

    private bool ForlornEnergyRestoredGetter() {
        return Events.GravityActivated;
    }

    private void MistLiftedSetter(bool value) {
        Events.MistLifted = value;
    }

    private bool MistLiftedGetter() {
        return Events.MistLifted;
    }

    private void SeinUISetter(bool obj) {
        SeinUI.DebugHideUI = obj;
    }

    private void SeinDamageTextSetter(bool obj) {
        GameSettings.Instance.DamageTextEnabled = obj;
    }

    private bool CameraEnabledGetter() {
        return UI.Cameras.Current.Camera.enabled;
    }

    private void CameraEnabledSetter(bool obj) {
        UI.Cameras.Current.Camera.enabled = obj;
        if (obj) {
            return;
        }

        HideDebugMenu();
        Graphics.SetRenderTarget(UI.Cameras.Current.GetComponent<Camera>().targetTexture);
        GL.Clear(true, true, Color.black);
        Graphics.SetRenderTarget(null);
    }

    private bool DebugMuteMusicGetter() {
        return MuteMusic;
    }

    private void DebugMuteMusicSetter(bool value) {
        MuteMusic = value;
    }

    private bool DebugMuteAmbienceGetter() {
        return MuteAmbience;
    }

    private void DebugMuteAmbienceSetter(bool value) {
        MuteAmbience = value;
    }

    private bool DebugMuteSoundEffectsGetter() {
        return MuteSoundEffects;
    }

    private void DebugMuteSoundEffectsSetter(bool value) {
        MuteSoundEffects = value;
    }

    private bool SeinUIGetter() {
        return SeinUI.DebugHideUI;
    }

    private bool SeinDamageTextGetter() {
        return GameSettings.Instance.DamageTextEnabled;
    }

    private bool SeinInvincibilityGetter() {
        return Characters.Sein && Characters.Sein.Mortality.DamageReciever && Characters.Sein.Mortality.DamageReciever.IsImmortal;
    }

    private void SeinInvincibilitySetter(bool newValue) {
        if (Characters.Sein) {
            Characters.Sein.Mortality.DamageReciever.IsImmortal = newValue;
        }
    }

    private bool ReplayEngineActiveGetter() {
        return Recorder.Instance && Recorder.Instance.Active;
    }

    private void ReplayEngineActiveSetter(bool newValue) {
        if (Recorder.Instance) {
            Recorder.Instance.Active = newValue;
        }
    }

    private bool GumoSequencesAction() {
        m_showGumoSequences = true;
        return false;
    }

    private bool AchievementHintGetter() {
        return ShowAchievementHint;
    }

    private void AchievementHintSetter(bool newValue) {
        ShowAchievementHint = newValue;
    }

    private bool CleanWaterGetter() {
        return Events.WaterPurified;
    }

    private void CleanWaterSetter(bool newValue) {
        Events.WaterPurified = newValue;
    }

    private void CheatsSetter(bool arg) {
        CheatsHandler.Instance.DebugEnabled = arg;
        if (!CheatsHandler.Instance.DebugEnabled) {
            ToggleDebugMenu();
        }
    }

    private bool CheatsGetter() {
        return CheatsHandler.Instance.DebugEnabled;
    }

    private void DebugControlsSetter(bool arg) {
        DebugControlsEnabled = arg;
    }

    private bool DebugControlsGetter() {
        return DebugControlsEnabled;
    }

    private void DebugTextSetter(bool arg) {
        DebugGUIText.Enabled = arg;
    }

    private bool DebugTextGetter() {
        return DebugGUIText.Enabled;
    }

    private void DebugSceneFrameworkSetter(bool arg) {
        m_showSceneFrameworkDebug = arg;
    }

    private bool DebugSceneFrameworkGetter() {
        return m_showSceneFrameworkDebug;
    }

    private bool VisualLogGetter() {
        return VisualLog.Instance != null;
    }

    private void VisualLogSetter(bool arg) {
        if (arg == (VisualLog.Instance != null)) {
            return;
        }

        if (arg) {
            gameObject.AddComponent<VisualLog>();
        } else {
            VisualLog.Disable();
        }
    }

    private bool LogCallbackHookGetter() {
        return LogCallbackHandler.Instance != null;
    }

    private void LogCallbackHookSetter(bool arg) {
        if (LogCallbackHandler.Instance == null) {
            LogCallbackHandler.Instance = new LogCallbackHandler();
        } else {
            LogCallbackHandler.Instance.RemoveHandler();
        }
    }

    private void DebugXboxControllerSetter(bool arg) {
        if (XboxLiveController.Instance) {
            XboxLiveController.Instance.IsDebugEnabled = arg;
        }
    }

    private bool DebugXboxControllerGetter() {
        return XboxLiveController.Instance && XboxLiveController.Instance.IsDebugEnabled;
    }

    private void UnloadUnusedSetter(bool arg) {
        Resources.UnloadUnusedAssets();
    }

    private bool UnloadUnusedGetter() {
        return true;
    }

    private bool ShowSoundLogGetter() {
        return Sound.IsSoundLogEnabled;
    }

    private void ShowSoundLogSetter(bool arg) {
        Sound.IsSoundLogEnabled = arg;
    }

    private bool ShowPinkBoxesGetter() {
        return Sound.IsPinkBoxesEnabled;
    }

    private void ShowPinkBoxesSetter(bool arg) {
        Sound.IsPinkBoxesEnabled = arg;
    }

    private bool DeactivateDarknessGetter() {
        return SpiritLightVisualAffectorManager.DeactivateLightMechanics;
    }

    private void DeactivateDarknessSetter(bool arg) {
        SpiritLightVisualAffectorManager.DeactivateLightMechanics = arg;
    }

    public bool ResetInputLock() {
        HideDebugMenu();
        GameController.Instance.ResetInputLocks();
        SuspensionManager.ResumeAll();
        return true;
    }

    public bool TeleportNightberry() {
        if (Items.NightBerry) {
            Items.NightBerry.transform.position = Characters.Sein.Position;
            Items.NightBerry.SetToDropMode();
        } else {
            var gameObject = Instantiate(NightberryPlaceholder, Characters.Sein.Position, Quaternion.identity) as GameObject;
            InstantiateUtility.Destroy(gameObject);
        }

        return true;
    }

    private void Initialize() {
        BuildMenu();
    }

    public void HandleQuickQuit() {
        if (DebugControlsEnabled && Input.ChargeJump.IsPressed && Input.Bash.IsPressed && Input.SoulFlame.IsPressed && !TestSetManager.IsPerformingTests) {
            try {
                InstantLoadScenesController.Instance.LogState();
            } catch (Exception ex) {
            }

            Application.Quit();
        }
    }

    public void Update() {
        if (CheatsHandler.Instance && !CheatsHandler.Instance.DebugEnabled && !CheatsHandler.DebugAlwaysEnabled) {
            return;
        }

        HandleQuickQuit();
        if (Active) {
            if (!m_lastDebugMenuActiveState) {
                Initialize();
            }

            m_menuList[(int)m_cursorIndex.x][(int)m_cursorIndex.y].OnSelectedUpdate();
        }
    }

    private void ResetHold() {
        m_holdRemainingTime = 0.4f;
        m_holdDelayDuration = 0.04f;
    }

    public void FixedUpdate() {
        if (CheatsHandler.Instance && !CheatsHandler.Instance.DebugEnabled && !CheatsHandler.DebugAlwaysEnabled) {
            return;
        }

        if (GameController.FreezeFixedUpdate) {
            return;
        }

        if (MoonInput.GetKeyDown(KeyCode.N)) {
            DisableEnemies();
        }

        if (!Active && !Recorder.IsPlaying && !DebugMenu.DashOrGrenadeEnabled) {
            if (Input.LeftShoulder.IsPressed && DebugControlsEnabled) {
                Time.timeScale = FastForwardTimeScale;
            } else if (Input.LeftShoulder.WasPressed) {
                Time.timeScale = 1f;
            }
        }

        if (Active) {
            if (m_showGumoSequences) {
                if (Input.SoulFlame.OnPressed) {
                    m_showGumoSequences = false;
                }

                if (Input.Down.OnPressed) {
                    m_gumoSequencesCursorIndex.y += 1f;
                }

                if (Input.Up.OnPressed) {
                    m_gumoSequencesCursorIndex.y -= 1f;
                }

                if (Input.Left.OnPressed) {
                    m_gumoSequencesCursorIndex.x -= 1f;
                }

                if (Input.Right.OnPressed) {
                    m_gumoSequencesCursorIndex.x += 1f;
                }

                if (m_gumoSequencesCursorIndex.x == -1f) {
                    m_gumoSequencesCursorIndex.x = m_gumoSequencesMenuList.Count - 1;
                }

                if (m_gumoSequencesCursorIndex.y == -1f) {
                    m_gumoSequencesCursorIndex.y = m_gumoSequencesMenuList[(int)m_gumoSequencesCursorIndex.x].Count - 1;
                }

                if (m_gumoSequencesCursorIndex.x == m_gumoSequencesMenuList.Count) {
                    m_gumoSequencesCursorIndex.x = 0f;
                }

                if (m_gumoSequencesCursorIndex.y == m_gumoSequencesMenuList[(int)m_gumoSequencesCursorIndex.x].Count) {
                    m_gumoSequencesCursorIndex.y = 0f;
                }

                if (m_gumoSequencesCursorIndex != m_lastGumoSequencesIndex) {
                    m_gumoSequencesMenuList[(int)m_gumoSequencesCursorIndex.x][(int)m_gumoSequencesCursorIndex.y].OnSelected();
                    m_lastGumoSequencesIndex = m_gumoSequencesCursorIndex;
                }

                m_gumoSequencesMenuList[(int)m_gumoSequencesCursorIndex.x][(int)m_gumoSequencesCursorIndex.y].OnSelectedFixedUpdate();
            } else {
                if (!m_lastDebugMenuActiveState) {
                    Initialize();
                }

                if (Input.Down.OnPressed) {
                    ResetHold();
                    m_cursorIndex.y += 1f;
                }

                if (Input.Up.OnPressed) {
                    ResetHold();
                    m_cursorIndex.y -= 1f;
                }

                if (Input.Left.OnPressed) {
                    ResetHold();
                    m_cursorIndex.x -= 1f;
                }

                if (Input.Right.OnPressed) {
                    ResetHold();
                    m_cursorIndex.x += 1f;
                }

                if (Input.Left.Pressed || Input.Right.Pressed || Input.Up.Pressed || Input.Down.Pressed) {
                    m_holdRemainingTime -= Time.deltaTime;
                    if (m_holdRemainingTime < 0f) {
                        m_holdRemainingTime = m_holdDelayDuration;
                        if (Input.Left.Pressed) {
                            m_cursorIndex.x -= 1f;
                        }

                        if (Input.Right.Pressed) {
                            m_cursorIndex.x += 1f;
                        }

                        if (Input.Down.Pressed) {
                            m_cursorIndex.y += 1f;
                        }

                        if (Input.Up.Pressed) {
                            m_cursorIndex.y -= 1f;
                        }
                    }
                }

                if (m_cursorIndex.x < 0f) {
                    m_cursorIndex.x = m_menuList.Count - 1;
                }

                if (m_cursorIndex.x > m_menuList.Count - 1) {
                    m_cursorIndex.x = 0f;
                }

                if (m_cursorIndex.y < 0f) {
                    m_cursorIndex.y = m_menuList[(int)m_cursorIndex.x].Count - 1;
                }

                if (Input.Left.OnPressed || Input.Right.OnPressed) {
                    if (m_cursorIndex.y > m_menuList[(int)m_cursorIndex.x].Count - 1) {
                        m_cursorIndex.y = m_menuList[(int)m_cursorIndex.x].Count - 1;
                    }
                } else if (m_cursorIndex.y > m_menuList[(int)m_cursorIndex.x].Count - 1) {
                    m_cursorIndex.y = 0f;
                }

                if (m_cursorIndex != m_lastIndex) {
                    m_menuList[(int)m_cursorIndex.x][(int)m_cursorIndex.y].OnSelected();
                    m_lastIndex = m_cursorIndex;
                    ShouldShowOnlySelectedItem = false;
                }

                m_menuList[(int)m_cursorIndex.x][(int)m_cursorIndex.y].OnSelectedFixedUpdate();
            }
        }

        m_lastDebugMenuActiveState = Active;
    }

    public void OnGUI() {
        if (Active) {
            GUILayout.BeginArea(new Rect(Screen.width - 150, Screen.height - 50, 150f, 50f));
            GUILayout.Label("BuildID: " + BuildID);
            GUILayout.EndArea();
            if (m_showGumoSequences) {
                GUILayout.BeginArea(new Rect(MenuTopLeftX, MenuTopLeftY, MenuWidth, MenuHeight), GUIContent.none, DebugMenuStyle);
                var num = 0;
                foreach (var list in m_gumoSequencesMenuList) {
                    var num2 = 0;
                    foreach (var debugMenuItem in list) {
                        var vector = new Vector2(HorizontalSpace * num, VerticalSpace * num2);
                        var b = new Vector2(num, num2) == m_gumoSequencesCursorIndex;
                        debugMenuItem.Draw(new Rect(vector.x, vector.y, HorizontalSpace, VerticalSpace), b);
                        num2++;
                    }

                    num++;
                }

                GUILayout.EndArea();
                GUI.Label(new Rect(MenuTopLeftX, MenuTopLeftY + MenuHeight, MenuWidth, 30f), m_gumoSequencesMenuList[(int)m_gumoSequencesCursorIndex.x][(int)m_gumoSequencesCursorIndex.y].HelpText, DebugMenuStyle);
            } else {
                if (m_menuList.Count == 0) {
                    return;
                }

                if (!ShouldShowOnlySelectedItem) {
                    GUILayout.BeginArea(new Rect(MenuTopLeftX, MenuTopLeftY, MenuWidth, MenuHeight), GUIContent.none, DebugMenuStyle);
                } else {
                    GUILayout.BeginArea(new Rect(MenuTopLeftX, MenuTopLeftY, MenuWidth, MenuHeight), GUIContent.none);
                }

                var num3 = 0;
                foreach (var list2 in m_menuList) {
                    var num4 = 0;
                    foreach (var debugMenuItem2 in list2) {
                        var vector2 = new Vector2(GetColPosition(num3), VerticalSpace * num4);
                        var flag = new Vector2(num3, num4) == m_cursorIndex;
                        if (!ShouldShowOnlySelectedItem || flag) {
                            debugMenuItem2.Draw(new Rect(vector2.x, vector2.y, ColumnsWidth[num3], VerticalSpace), flag);
                        }

                        num4++;
                    }

                    num3++;
                }

                GUILayout.EndArea();
                if (!ShouldShowOnlySelectedItem) {
                    GUI.Label(new Rect(MenuTopLeftX, MenuTopLeftY + MenuHeight, MenuWidth, 30f), m_menuList[(int)m_cursorIndex.x][(int)m_cursorIndex.y].HelpText, DebugMenuStyle);
                }
            }
        } else if (m_showSceneFrameworkDebug) {
            Scenes.Manager.DrawScenesManagerDebugData();
        }
    }

    private int GetColPosition(int index) {
        var num = 0;
        for (var i = 0; i < index; i++) {
            num += ColumnsWidth[i];
        }

        return num;
    }

    private bool CreateCheckpoint() {
        HideDebugMenu();
        GameController.Instance.CreateCheckpoint();
        return true;
    }

    private bool RestoreCheckpoint() {
        GameController.Instance.RestoreCheckpoint();
        return true;
    }

    private bool GoToScene() {
        StartCoroutine(GoToScene(m_menuList[(int)m_cursorIndex.x][(int)m_cursorIndex.y].Text));
        return true;
    }

    private bool FaderBAction() {
        UI.Fader.Fade(0.5f, 0.5f, 0.5f, null, null);
        return true;
    }

    public IEnumerator GoToScene(string sceneName) {
        var sceneInformation = Scenes.Manager.GetSceneInformation(sceneName);
        Scenes.Manager.AutoLoadingUnloading = false;
        Scenes.Manager.UnloadAllScenes();
        Scenes.Manager.DestroyManager.DestroyAll();
        SuspensionManager.SuspendAll();
        while (Scenes.Manager.ResourcesNeedUnloading) {
            yield return new WaitForFixedUpdate();
        }

        SuspensionManager.ResumeAll();
        GameController.Instance.ResetStateForDebugMenuGoToScene();
        GoToSceneController.Instance.GoToScene(sceneInformation, null, true);
        ToggleDebugMenu();
        yield break;
    }

    private bool ResetNightBerryPosition() {
        if (Items.NightBerry == null) {
            return false;
        }

        Items.NightBerry.transform.position = Characters.Sein.PlatformBehaviour.PlatformMovement.Position;
        return true;
    }

    private bool Quit() {
        Application.Quit();
        return true;
    }

    public const float HOLD_DELAY = 0.4f;

    public const float HOLD_FAST_DELAY = 0.04f;

    public static DebugMenuB Instance;

    private readonly List<WorldEvents> m_worldEvents = new List<WorldEvents>();

    private readonly List<List<IDebugMenuItem>> m_menuList = new List<List<IDebugMenuItem>>();

    private readonly List<List<IDebugMenuItem>> m_gumoSequencesMenuList = new List<List<IDebugMenuItem>>();

    public List<SceneMetaData> ImportantLevels = new List<SceneMetaData>();

    public List<string> ImportantLevelsNames = new List<string>();

    public GUISkin Skin;

    public static GUIStyle SelectedStyle;

    public static GUIStyle Style;

    public static GUIStyle PressedStyle;

    public static GUIStyle DebugMenuStyle;

    public static GUIStyle StyleEnabled;

    public static GUIStyle StyleDisabled;

    public SceneMetaData TestScene;

    public static bool UnlockAllCutscenes;

    public static bool MuteMusic;

    public static bool MuteAmbience;

    public static bool MuteSoundEffects;

    public float VerticalSpace = 25f;

    public float HorizontalSpace = 150f;

    private Vector2 m_cursorIndex;

    private Vector2 m_gumoSequencesCursorIndex;

    public MessageProvider ReplayGotResetMessageProvider;

    public int BuildID;

    public float MenuTopLeftX = 200f;

    public float MenuTopLeftY = 70f;

    public float MenuWidth = 900f;

    public float MenuHeight = 600f;

    private bool m_showSceneFrameworkDebug;

    public static bool ShowAchievementHint;

    private bool m_showGumoSequences;

    private bool m_superSlowMotion;

    public List<int> ColumnsWidth = new List<int>();

    public List<GoToSequenceData> GumoSequence = new List<GoToSequenceData>();

    public static bool IsFullyInstalledDebugOverride;

    private static readonly HashSet<ISuspendable> SuspendablesToIgnoreForGameplay = new HashSet<ISuspendable>();

    private List<GameObject> m_particleSystems;

    private GameObject[] m_art;

    private GameObject[] m_enemies;

    private bool m_highFPSPhysics;

    public GameObject NightberryPlaceholder;

    private bool m_lastDebugMenuActiveState;

    private Vector2 m_lastIndex;

    private Vector2 m_lastGumoSequencesIndex;

    public static bool Active;

    public static bool DebugControlsEnabled;

    public float FastForwardTimeScale = 3f;

    private float m_holdDelayDuration;

    private float m_holdRemainingTime;

    public static bool ShouldShowOnlySelectedItem;
}

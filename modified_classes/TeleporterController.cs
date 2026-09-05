using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;

public class TeleporterController : SaveSerialize, ISuspendable {
    private void Nullify() {
        m_teleportingStartSound = null;
    }

    public override void Serialize(Archive ar) {
        // By default we just serialize 12 booleans, one for each default teleporter.
        // So if we only get 12 bytes of information or only have default teleporters then
        // we stick to that.
        // If there are more than 12 teleporters immediately after the 12 default teleporters
        // we serialize the number of extra teleporters, and then for each teleporter
        // serialise the name, location, and activation status.
        if (ar.Reading) {
            var readLength = ar.MemoryStream.Length;
            if (readLength < 12) {
                return;
            }

            // Read default teleporters.
            for (var i = 0; i < 12; i++) {
                var gameMapTeleporter = Teleporters[i];
                ar.Serialize(ref gameMapTeleporter.Activated);
            }

            // Determine extra teleporter count.
            var requiredCustomTeleporterCount = 0;
            if (readLength > 12) {
                ar.Serialize(ref requiredCustomTeleporterCount);
            }

            // Remove excess teleporters.
            while (Teleporters.Count > 12 + requiredCustomTeleporterCount) {
                Teleporters.RemoveAt(Teleporters.Count - 1);
            }

            customWarps.Clear();
            // Create or modify teleporters.
            for (var i = 0; i < requiredCustomTeleporterCount; i++) {
                var name = "???";
                var position = new Vector3(0, 0, 0);
                var activated = false;
                ar.Serialize(ref name);
                ar.Serialize(ref position);
                ar.Serialize(ref activated);
                var currentTeleporterIndex = 12 + i;
                if (currentTeleporterIndex < Teleporters.Count) {
                    // Alter the existing teleporter.
                    Teleporters[currentTeleporterIndex].SetInfo(name, position, activated);
                } else {
                    // Create a new teleporter.
                    var gameMapTeleporter = new GameMapTeleporter(name, position, activated);
                    Teleporters.Add(gameMapTeleporter);
                }

                customWarps.Add(name);
            }
        } else {
            // Writing.
            if (Teleporters.Count < 12) {
                return;
            }

            // Default teleporters.
            for (var i = 0; i < 12; i++) {
                var gameMapTeleporter = Teleporters[i];
                ar.Serialize(ref gameMapTeleporter.Activated);
            }

            // Extra teleporters.
            var customTeleporterCount = Teleporters.Count - 12;
            if (customTeleporterCount > 0) {
                ar.Serialize(ref customTeleporterCount);
            }

            for (var i = 12; i < Teleporters.Count; i++) {
                var gameMapTeleporter = Teleporters[i];
                ar.Serialize(ref gameMapTeleporter.Identifier);
                ar.Serialize(ref gameMapTeleporter.WorldPosition);
                ar.Serialize(ref gameMapTeleporter.Activated);
            }
        }
    }

    public static bool CanTeleport(string ignoreIdentifier) {
        if (Instance) {
            for (var i = 0; i < Instance.Teleporters.Count; i++) {
                var gameMapTeleporter = Instance.Teleporters[i];
                if (!(gameMapTeleporter.Identifier == ignoreIdentifier)) {
                    if (gameMapTeleporter.Activated) {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public override void Awake() {
        base.Awake();
        Instance = this;
        SuspensionManager.Register(this);
        Events.Scheduler.OnGameReset.Add(OnGameReset);
        DontTeleportForAnimationTesting = false;
    }

    public override void OnDestroy() {
        base.OnDestroy();
        Instance = null;
        SuspensionManager.Unregister(this);
        Events.Scheduler.OnGameReset.Remove(OnGameReset);
    }

    public void OnGameReset() {
        for (var i = 0; i < Instance.Teleporters.Count; i++) {
            Instance.Teleporters[i].Activated = false;
        }

        CancelTeleport();
    }

    public void CancelTeleport() {
        Randomizer.IsUsingRandomizerTeleportAnywhere = false;
        m_isTeleporting = false;
        m_isBlooming = false;
        if (!InstantiateUtility.IsDestroyed(m_teleportingStartSound)) {
            m_teleportingStartSound.FadeOut(0.1f, true);
            m_teleportingStartSound = null;
        }
    }

    public static void Show(string identifier) {
        UI.Menu.ShowWorldMap(false);
        GameMapUI.Instance.SetShowingTeleporters();
        GameMapUI.Instance.Teleporters.Select(identifier);
        AreaMapUI.Instance.Navigation.ScrollPosition = GameMapUI.Instance.Teleporters.SelectedTeleporter.WorldPosition;
        WorldMapUI.Instance.HideAreaSelection();
        if (GameMapUI.Instance.Teleporters.OpenWindowSound) {
            Sound.Play(GameMapUI.Instance.Teleporters.OpenWindowSound.GetSound(null), Vector3.zero, null);
        }
    }

    public static void OnClose() {
        GameMapUI.Instance.SetNormal();
    }

    public static bool ActivateAll() {
        foreach (var gameMapTeleporter in Instance.Teleporters) {
            gameMapTeleporter.Activated = true;
        }

        return true;
    }

    public static void Activate(string identifier, bool natural) {
        // The teleporter is activated before anyone is told, because telling can fail: a server
        // that rejects the frame or a bingo board that never loaded must not cost a warp point.
        foreach (var gameMapTeleporter in Instance.Teleporters) {
            if (gameMapTeleporter.Identifier == identifier) {
                gameMapTeleporter.Activated = true;
            }
        }

        RandomizerStatsManager.TeleporterActivated(identifier);
        if (natural) {
            RandomizerSyncManager.FoundTP(identifier);
        }

        BingoController.OnActivateTeleporter(identifier);
    }

    public static void Activate(string identifier) {
        Activate(identifier, true);
    }

    public static void BeginTeleportation(GameMapTeleporter selectedTeleporter) {
        if (Vector3.Distance(selectedTeleporter.WorldPosition, Characters.Sein.Position) < 10f) {
            return;
        }

        BingoController.OnWarp();
        if (selectedTeleporter.Area.Area.AreaNameString == "Forlorn Ruins") {
            Randomizer.NightBerryWarpPosition = selectedTeleporter.WorldPosition;
            Characters.Sein.Inventory.SetRandomizerItem(82, 1);
        }

        RandomizerHints.ShowTip();
        if (Characters.Sein.Abilities.Swimming.CurrentState != SeinSwimming.State.OutOfWater) {
            Characters.Sein.Abilities.Swimming.ChangeState(SeinSwimming.State.OutOfWater);
            Characters.Sein.Abilities.Swimming.HideBreathingUI();
        }

        if (!Instance.DontTeleportForAnimationTesting) {
            Scenes.Manager.AdditivelyLoadScenesAtPosition(selectedTeleporter.WorldPosition, true, false, true);
            Instance.m_teleporterTargetPosition = selectedTeleporter.WorldPosition;
        }

        Instance.m_isTeleporting = true;
        Characters.Sein.Controller.PlayAnimation(Instance.TeleportingStartAnimation);
        if (GameMapUI.Instance.Teleporters.StartTeleportingSound) {
            Sound.Play(GameMapUI.Instance.Teleporters.StartTeleportingSound.GetSound(null), Vector3.zero, null);
        }

        if (Characters.Sein.Abilities.Carry && Characters.Sein.Abilities.Carry.CurrentCarryable != null) {
            Characters.Sein.Abilities.Carry.CurrentCarryable.Drop();
        }

        if (Instance.TeleportingStartSound != null) {
            Instance.m_teleportingStartSound = Sound.Play(Instance.TeleportingStartSound.GetSound(null), Characters.Sein.Position, Instance.Nullify);
        }

        Characters.Sein.Controller.OnTriggeredAnimationFinished += OnFinishedTeleportingStartAnimation;
        Instance.m_startTime = Time.time;
        foreach (var savePedestal in SavePedestal.All) {
            savePedestal.OnBeginTeleporting();
        }
    }

    public static void OnFinishedTeleportingStartAnimation() {
        Characters.Sein.Controller.OnTriggeredAnimationFinished -= OnFinishedTeleportingStartAnimation;
        if (Instance.m_isTeleporting) {
            Characters.Sein.Controller.PlayAnimation(Instance.TeleportingLoopAnimation);
            Instance.TeleportingTwirlAnimationSound.Play();
        }
    }

    public void FixedUpdate() {
        if (m_isTeleporting) {
            var time = Time.time;
            var num = 7f;
            if (DontTeleportForAnimationTesting) {
                if (time > m_startTime + NoTeleportAnimationTime) {
                    Characters.Sein.Controller.StopAnimation();
                    Characters.Sein.Controller.PlayAnimation(Instance.TeleportingFinishAnimation);
                    Instance.TeleportingTwirlAnimationSound.Stop();
                    m_isTeleporting = false;
                }
            } else if (!Scenes.Manager.IsLoadingScenes && time > m_startTime + num) {
                m_isTeleporting = false;
                if (BloomFade) {
                    InstantiateUtility.Instantiate(BloomFade);
                    m_bloomCurrentTime = 0f;
                    m_isBlooming = true;
                    if (TeleportingBloomSound) {
                        Sound.Play(TeleportingBloomSound.GetSound(null), Characters.Sein.Position, null);
                    }
                } else {
                    UI.Fader.Fade(0.5f, 0.05f, 0.2f, OnFadedToBlack, null);
                }
            }
        }

        if (m_isBlooming) {
            m_bloomCurrentTime += !IsSuspended ? Time.deltaTime : 0f;
            if (m_bloomCurrentTime > BloomFadeDuration) {
                OnFadedToBlack();
                m_isBlooming = false;
            }
        }
    }

    public void OnFadedToBlack() {
        foreach (var savePedestal in SavePedestal.All) {
            savePedestal.OnFinishedTeleporting();
        }

        if (!InstantiateUtility.IsDestroyed(m_teleportingStartSound)) {
            m_teleportingStartSound.FadeOut(0.5f, true);
            m_teleportingStartSound = null;
        }

        if (BloomFade) {
            UberGCManager.CollectResourcesIfNeeded();
        }

        if (Randomizer.IsUsingRandomizerTeleportAnywhere) {
            RandomizerBonusSkill.LastAltR = Characters.Sein.Position;
        }

        Characters.Sein.Position = m_teleporterTargetPosition + Vector3.up * 1.6f;
        CameraPivotZone.InstantUpdate();
        Scenes.Manager.UpdatePosition();
        Scenes.Manager.UnloadScenesAtPosition(true);
        Scenes.Manager.EnableDisabledScenesAtPosition();
        Characters.Sein.Controller.StopAnimation();
        UI.Cameras.Current.MoveCameraToTargetInstantly();
        if (Characters.Ori) {
            Characters.Ori.BackToPlayerController();
        }

        GameController.Instance.CreateCheckpoint();
        GameController.Instance.PerformSaveGameSequence();
        RandomizerStatsManager.UsedTeleporter();

        if (Randomizer.IsUsingRandomizerTeleportAnywhere) {
            var value = World.Events.Find(Randomizer.MistySim).Value;
            if (value != 1 && value != 8) {
                World.Events.Find(Randomizer.MistySim).Value = 10;
            }
        }

        LateStartHook.AddLateStartMethod(OnFinishedTeleporting);
    }

    public void OnFinishedTeleporting() {
        Randomizer.IsUsingRandomizerTeleportAnywhere = false;
        CameraFrustumOptimizer.ForceUpdate();
        Characters.Sein.Controller.PlayAnimation(Instance.TeleportingFinishAnimation);
        if (GameMapUI.Instance.Teleporters.ReachDestinationTeleporterSound) {
            Sound.Play(GameMapUI.Instance.Teleporters.ReachDestinationTeleporterSound.GetSound(null), transform.position, null);
        }

        TeleportingTwirlAnimationSound.Stop();
        if (TeleporterFinishEffect) {
            InstantiateUtility.Instantiate(TeleporterFinishEffect, m_teleporterTargetPosition, Quaternion.identity);
        }

        if (TeleportingEndSound) {
            Sound.Play(TeleportingEndSound.GetSound(null), Characters.Sein.Position, null);
        }

        // Disable any sein locks that we got from teleporting from a physical savePedestal.
        Characters.Ori.ChangeState(Ori.State.Hovering);
        Characters.Ori.EnableHoverWobbling = true;
        if (Characters.Sein.Abilities.SpiritFlame) {
            Characters.Sein.Abilities.SpiritFlame.RemoveLock("savePedestal");
        }
    }

    public static bool HasCustomWarp(string name) {
        if (Instance == null) {
            return false;
        }

        return Instance.customWarps.Contains(name);
    }

    public static void RemoveCustomTeleporters() {
        if (Instance != null) {
            Instance.Teleporters.RemoveAll(teleporter => teleporter.Name.GetType() == typeof(RandomizerMessageProvider));
            Instance.customWarps.Clear();
        }
    }

    public static void AddCustomTeleporter(string name, float warpX, float warpY) {
        if (Instance == null) {
            return;
        }

        // If we already have that teleporter don't add it.
        if (Instance.customWarps.Contains(name)) {
            return;
        }

        Instance.customWarps.Add(name);
        var teleporter = new GameMapTeleporter(name, warpX, warpY);
        Instance.Teleporters.Add(teleporter);
    }

    public static bool IsTeleporting {
        get {
            if (Instance == null) {
                return false;
            }

            return Instance.m_isTeleporting || Instance.m_isBlooming;
        }
    }

    public bool IsSuspended { get; set; }

    public static TeleporterController Instance;

    public TextureAnimationWithTransitions TeleportingStartAnimation;

    public TextureAnimationWithTransitions TeleportingLoopAnimation;

    public TextureAnimationWithTransitions TeleportingFinishAnimation;

    public SoundSource TeleportingTwirlAnimationSound;

    public SoundProvider TeleportingStartSound;

    public SoundProvider TeleportingBloomSound;

    public SoundProvider TeleportingEndSound;

    private SoundPlayer m_teleportingStartSound;

    private float m_startTime;

    public bool DontTeleportForAnimationTesting;

    public float NoTeleportAnimationTime = 6f;

    public List<GameMapTeleporter> Teleporters = new List<GameMapTeleporter>();

    private HashSet<string> customWarps = new HashSet<string>();

    public GameObject BloomFade;

    public float BloomFadeDuration;

    public GameObject TeleporterFinishEffect;

    private bool m_isTeleporting;

    private bool m_isBlooming;

    private float m_bloomCurrentTime;

    private Vector3 m_teleporterTargetPosition;
}

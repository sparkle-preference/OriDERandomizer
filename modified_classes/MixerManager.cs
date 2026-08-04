using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using UnityEngine.Audio;

public class MixerManager : MonoBehaviour {
    public void RegisterSnapshotZone(MixerSnapshotZone mixerSnapshotZone) {
        snapshotZones.Add(mixerSnapshotZone);
    }

    public void DeregisterSnapshotZone(MixerSnapshotZone mixerSnapshotZone) {
        snapshotZones.Remove(mixerSnapshotZone);
    }

    public void Awake() {
        manager = this;
    }

    public static MixerManager Instance => manager;

    public void RegisterActiveSnapshot(MixerSnapshot snapshot) {
        if (!currentlyActiveSnapshots.Contains(snapshot)) {
            currentlyActiveSnapshots.Add(snapshot);
        }
    }

    public void FixedUpdate() {
        var flag = UI.MainMenuVisible || ResumeGameController.IsGameSuspended;
        if (flag != wasInUI) {
            if (flag) {
                UISnapshot.FadeIn();
            } else {
                UISnapshot.FadeOut();
            }
        }

        wasInUI = flag;
        UpdateMixerSnapshotZones();
        UpdateMixerSettingsBasedOnActiveSnapshots();
    }

    private void UpdateMixerSettingsBasedOnActiveSnapshots() {
        settings.Reset();
        for (var i = 0; i < currentlyActiveSnapshots.Count; i++) {
            var mixerSnapshot = currentlyActiveSnapshots[i];
            mixerSnapshot.UpdateMixerSnapshotState(Time.fixedDeltaTime);
            settings.MultiplyBlendWith(mixerSnapshot.SnapshotSettings, mixerSnapshot.Weight);
        }

        settings.MultiplyBlendWith(ModulatingSnapshot.SnapshotSettings, 1f);
        currentlyActiveSnapshots.RemoveAll(CachedIsSnapshotInactivePredicate);
        settings.Music *= Mathf.Log10(GameSettings.Instance.MusicVolume * 9f + 1f);
        settings.SoundEffects *= Mathf.Log10(GameSettings.Instance.SoundEffectsVolume * 9f + 1f);
        ApplySoundCompression();
        var masterMixer = GetMasterMixer();
        settings.ApplyGroupSettingsToMixer(masterMixer);
    }

    private void UpdateMixerSnapshotZones() {
        var cameraPositionForSampling = UI.Cameras.Current.CameraPositionForSampling;
        var sceneRoot = Scenes.Manager.FindLoadedSceneRootFromPosition(cameraPositionForSampling);
        MixerSnapshot mixerSnapshot = null;
        if (sceneRoot != null) {
            mixerSnapshot = sceneRoot.SceneSettings.DefaultMixerSnapshot;
        }

        if (mixerSnapshot == null) {
            mixerSnapshot = DefaultSceneSnapshot;
        }

        if (mixerSnapshot != currentSceneMixerSnapshot) {
            if (currentSceneMixerSnapshot != null) {
                currentSceneMixerSnapshot.FadeOut();
            }

            if (mixerSnapshot != null) {
                mixerSnapshot.FadeIn();
            }
        }

        currentSceneMixerSnapshot = mixerSnapshot;
        for (var i = 0; i < snapshotZones.Count; i++) {
            var mixerSnapshotZone = snapshotZones[i];
            mixerSnapshotZone.UpdateSnapshotZoneState(mixerSnapshotZone.Bounds.Contains(cameraPositionForSampling));
        }
    }

    public static AudioMixer GetMasterMixer() {
        if (cachedMasterMixer == null) {
            cachedMasterMixer = (AudioMixer)Resources.Load("masterMixer", typeof(AudioMixer));
        }

        return cachedMasterMixer;
    }

    public static AudioMixerGroup GetMixerGroup(MixerGroupType group) {
        if (!typeToGroup.TryGetValue(group, out var audioMixerGroup)) {
            switch (group) {
                case MixerGroupType.Foley:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("foley")[0];
                    break;
                case MixerGroupType.Footsteps:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("footsteps")[0];
                    break;
                case MixerGroupType.EnemiesAttack:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("enemiesAttack")[0];
                    break;
                case MixerGroupType.EnemiesFoley:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("enemiesFoley")[0];
                    break;
                case MixerGroupType.AmbienceQuad:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("ambienceQuad")[0];
                    break;
                case MixerGroupType.AmbiencePoint:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("ambiencePoint")[0];
                    break;
                case MixerGroupType.Attacks:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("attacks")[0];
                    break;
                case MixerGroupType.Destruction:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("destruction")[0];
                    break;
                case MixerGroupType.UI:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("ui")[0];
                    break;
                case MixerGroupType.SpiritTree:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("spiritTree")[0];
                    break;
                case MixerGroupType.Sein:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("sein")[0];
                    break;
                case MixerGroupType.Doors:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("doors")[0];
                    break;
                case MixerGroupType.Cutscenes:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("cutscenes")[0];
                    break;
                case MixerGroupType.Props:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("props")[0];
                    break;
                case MixerGroupType.Collectibles:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("collectibles")[0];
                    break;
                case MixerGroupType.MusicStingers:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("stingers")[0];
                    break;
                case MixerGroupType.MusicLoops:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("loops")[0];
                    break;
                default:
                    audioMixerGroup = GetMasterMixer().FindMatchingGroups("Master")[0];
                    break;
            }

            typeToGroup.Add(group, audioMixerGroup);
        }

        return audioMixerGroup;
    }

    public static void WarmUpResource() {
        GetMasterMixer();
    }

    public void ApplySoundCompression() {
        if (RandomizerSettings.Accessibility.ApplySoundCompression) {
            var multiplier = 1f - RandomizerSettings.Accessibility.SoundCompressionFactor;
            settings.MusicLoops = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.MusicLoops));
            settings.MusicStingers = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.MusicStingers));
            settings.AmbienceQuad = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.AmbienceQuad));
            settings.AmbiencePoint = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.AmbiencePoint));
            settings.EnemiesAttack = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.EnemiesAttack));
            settings.EnemiesFoley = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.EnemiesFoley));
            settings.Foley = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.Foley));
            settings.Footsteps = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.Footsteps));
            settings.Attacks = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.Attacks));
            settings.Destruction = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.Destruction));
            settings.UI = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.UI));
            settings.SpiritTree = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.SpiritTree));
            settings.Sein = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.Sein));
            settings.Doors = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.Doors));
            settings.Cutscenes = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.Cutscenes));
            settings.Props = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.Props));
            settings.Collectibles = Mathf.Pow(10f, multiplier * Mathf.Log10(settings.Collectibles));
        }
    }

    public MixerSnapshot DefaultSceneSnapshot;

    public MixerSnapshot UISnapshot;

    public MixerSnapshot ModulatingSnapshot;

    private MixerGroupSettings currentMixerGroupSettings;

    private bool wasInUI;

    private static readonly Predicate<MixerSnapshot> CachedIsSnapshotInactivePredicate = snapshot => snapshot.State == MixerSnapshot.MixerSnapshotState.Inactive;

    private static Dictionary<MixerGroupType, AudioMixerGroup> typeToGroup = new Dictionary<MixerGroupType, AudioMixerGroup>();

    private List<MixerSnapshot> currentlyActiveSnapshots = new List<MixerSnapshot>(10);

    private static AudioMixer cachedMasterMixer;

    private static MixerManager manager;

    private MixerGroupSettings settings;

    private List<MixerSnapshotZone> snapshotZones = new List<MixerSnapshotZone>(5);

    private MixerSnapshot currentSceneMixerSnapshot;
}

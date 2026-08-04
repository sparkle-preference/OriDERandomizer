using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using UnityEngine.Audio;

public class MixerManager : MonoBehaviour {
    public void RegisterSnapshotZone(MixerSnapshotZone mixerSnapshotZone) {
        m_snapshotZones.Add(mixerSnapshotZone);
    }

    public void DeregisterSnapshotZone(MixerSnapshotZone mixerSnapshotZone) {
        m_snapshotZones.Remove(mixerSnapshotZone);
    }

    public void Awake() {
        s_manager = this;
    }

    public static MixerManager Instance => s_manager;

    public void RegisterActiveSnapshot(MixerSnapshot snapshot) {
        if (!m_currentlyActiveSnapshots.Contains(snapshot)) {
            m_currentlyActiveSnapshots.Add(snapshot);
        }
    }

    public void FixedUpdate() {
        var flag = UI.MainMenuVisible || ResumeGameController.IsGameSuspended;
        if (flag != m_wasInUI) {
            if (flag) {
                UISnapshot.FadeIn();
            } else {
                UISnapshot.FadeOut();
            }
        }

        m_wasInUI = flag;
        UpdateMixerSnapshotZones();
        UpdateMixerSettingsBasedOnActiveSnapshots();
    }

    private void UpdateMixerSettingsBasedOnActiveSnapshots() {
        m_settings.Reset();
        for (var i = 0; i < m_currentlyActiveSnapshots.Count; i++) {
            var mixerSnapshot = m_currentlyActiveSnapshots[i];
            mixerSnapshot.UpdateMixerSnapshotState(Time.fixedDeltaTime);
            m_settings.MultiplyBlendWith(mixerSnapshot.SnapshotSettings, mixerSnapshot.Weight);
        }

        m_settings.MultiplyBlendWith(ModulatingSnapshot.SnapshotSettings, 1f);
        m_currentlyActiveSnapshots.RemoveAll(CachedIsSnapshotInactivePredicate);
        m_settings.Music = m_settings.Music * Mathf.Log10(GameSettings.Instance.MusicVolume * 9f + 1f);
        m_settings.SoundEffects = m_settings.SoundEffects * Mathf.Log10(GameSettings.Instance.SoundEffectsVolume * 9f + 1f);
        ApplySoundCompression();
        var masterMixer = GetMasterMixer();
        m_settings.ApplyGroupSettingsToMixer(masterMixer);
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

        if (mixerSnapshot != m_currentSceneMixerSnapshot) {
            if (m_currentSceneMixerSnapshot != null) {
                m_currentSceneMixerSnapshot.FadeOut();
            }

            if (mixerSnapshot != null) {
                mixerSnapshot.FadeIn();
            }
        }

        m_currentSceneMixerSnapshot = mixerSnapshot;
        for (var i = 0; i < m_snapshotZones.Count; i++) {
            var mixerSnapshotZone = m_snapshotZones[i];
            mixerSnapshotZone.UpdateSnapshotZoneState(mixerSnapshotZone.Bounds.Contains(cameraPositionForSampling));
        }
    }

    public static AudioMixer GetMasterMixer() {
        if (s_cachedMasterMixer == null) {
            s_cachedMasterMixer = (AudioMixer)Resources.Load("masterMixer", typeof(AudioMixer));
        }

        return s_cachedMasterMixer;
    }

    public static AudioMixerGroup GetMixerGroup(MixerGroupType group) {
        AudioMixerGroup audioMixerGroup;
        if (!s_typeToGroup.TryGetValue(group, out audioMixerGroup)) {
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

            s_typeToGroup.Add(group, audioMixerGroup);
        }

        return audioMixerGroup;
    }

    public static void WarmUpResource() {
        GetMasterMixer();
    }

    public void ApplySoundCompression() {
        if (RandomizerSettings.Accessibility.ApplySoundCompression) {
            var multiplier = 1f - RandomizerSettings.Accessibility.SoundCompressionFactor;
            m_settings.MusicLoops = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.MusicLoops));
            m_settings.MusicStingers = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.MusicStingers));
            m_settings.AmbienceQuad = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.AmbienceQuad));
            m_settings.AmbiencePoint = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.AmbiencePoint));
            m_settings.EnemiesAttack = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.EnemiesAttack));
            m_settings.EnemiesFoley = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.EnemiesFoley));
            m_settings.Foley = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.Foley));
            m_settings.Footsteps = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.Footsteps));
            m_settings.Attacks = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.Attacks));
            m_settings.Destruction = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.Destruction));
            m_settings.UI = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.UI));
            m_settings.SpiritTree = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.SpiritTree));
            m_settings.Sein = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.Sein));
            m_settings.Doors = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.Doors));
            m_settings.Cutscenes = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.Cutscenes));
            m_settings.Props = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.Props));
            m_settings.Collectibles = Mathf.Pow(10f, multiplier * Mathf.Log10(m_settings.Collectibles));
        }
    }

    public MixerSnapshot DefaultSceneSnapshot;

    public MixerSnapshot UISnapshot;

    public MixerSnapshot ModulatingSnapshot;

    private MixerGroupSettings m_currentMixerGroupSettings;

    private bool m_wasInUI;

    private static readonly Predicate<MixerSnapshot> CachedIsSnapshotInactivePredicate = snapshot => snapshot.State == MixerSnapshot.MixerSnapshotState.Inactive;

    private static Dictionary<MixerGroupType, AudioMixerGroup> s_typeToGroup = new Dictionary<MixerGroupType, AudioMixerGroup>();

    private List<MixerSnapshot> m_currentlyActiveSnapshots = new List<MixerSnapshot>(10);

    private static AudioMixer s_cachedMasterMixer;

    private static MixerManager s_manager;

    private MixerGroupSettings m_settings = default;

    private List<MixerSnapshotZone> m_snapshotZones = new List<MixerSnapshotZone>(5);

    private MixerSnapshot m_currentSceneMixerSnapshot;
}

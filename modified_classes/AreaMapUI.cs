using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Game;
using RandoExts;
using UnityEngine;
using Input = Core.Input;

public class AreaMapUI : MonoBehaviour, ISuspendable {
    public GameObject PlayerPositionMarker { get; set; }

    public GameObject SoulFlamePositionMarker { get; set; }

    public AreaMapDebugNavigation DebugNavigation { get; set; }

    public AreaMapNavigation Navigation { get; set; }

    public AreaMapIconManager IconManager { get; set; }

    public Transform FadeOutGroup => FadeOutAnimator.transform;

    public void Hide() {
        gameObject.SetActive(false);
    }

    public void Show() {
        gameObject.SetActive(true);
    }

    public void ResetMaps() {
        foreach (var areaMapCanvas in Canvases) {
            areaMapCanvas.ResetMap();
        }

        foreach (var areaMapCanvasOverlay in GetComponentsInChildren<AreaMapCanvasOverlay>(true)) {
            areaMapCanvasOverlay.ApplyMasks();
        }
    }

    public void Awake() {
        Instance = this;
        DebugNavigation = GetComponent<AreaMapDebugNavigation>();
        Navigation = GetComponent<AreaMapNavigation>();
        IconManager = GetComponent<AreaMapIconManager>();
        SuspensionManager.Register(this);
        AreaMapLegend.HideSilently();
        if (PlayerPositionMarker == null) {
            PlayerPositionMarker = Instantiate(PlayerPositionMarkerPrefab);
            PlayerPositionMarker.transform.parent = FadeOutGroup;
            TransparencyAnimator.Register(PlayerPositionMarker.transform);
        }

        if (SoulFlamePositionMarker == null) {
            SoulFlamePositionMarker = Instantiate(SoulFlamePositionMarkerPrefab);
            SoulFlamePositionMarker.transform.parent = FadeOutGroup;
            TransparencyAnimator.Register(SoulFlamePositionMarker.transform);
        }

        if (RandomizerTooltip == null) {
            var obj = Instantiate(transform.FindChild("legend/player").gameObject);
            obj.transform.parent = transform.FindChild("legend");
            RandomizerTooltip = obj.GetComponent<MessageBox>();
            RandomizerTooltip.MessageProvider = null;
            RandomizerTooltip.OverrideText = "Unknown";
        }

        if (KeysanityDoorTooltips.Count == 0) {
            for (var i = 0; i < 12; i++) {
                var obj = Instantiate(transform.FindChild("legend/player").gameObject);
                obj.transform.parent = transform.FindChild("legend");
                var doorTTip = obj.GetComponent<MessageBox>();
                doorTTip.MessageProvider = null;
                doorTTip.OverrideText = "Unknown";
                KeysanityDoorTooltips.Add(doorTTip);
            }
        }
    }

    public void OnDestroy() {
        SuspensionManager.Unregister(this);
        Instance = null;
    }

    public AreaMapCanvas FindCanvas(GameWorldArea area) {
        return Canvases.FirstOrDefault(canvas => canvas.Area == area);
    }

    public void Init() {
        ResetMaps();
        IconManager.ShowAreaIcons();
        Navigation.Advance();
        Navigation.UpdateScrollLimits();
        PlayerPositionOffset = Vector2.zero;
        Navigation.Init();
        var fog = transform.FindChild("mapPivot/mistyWoodsFog");
        fog.gameObject.SetActive(false);
        foreach (var areaMapCanvas in Canvases) {
            areaMapCanvas.RuntimeArea.DiscoverAllAreas();
        }

        Navigation.UpdateScrollLimits();
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (!GameMapUI.Instance.IsVisible) {
            return;
        }

        Navigation.Advance();
        DebugNavigation.Advance();
        UpdatePlayerPositionMarker();
        UpdateSoulFlamePositionMarker();
        UpdateCurrentArea();

        if (!GameMapUI.Instance.ShowingObjective) {
            var msg = $"#{ObjectiveMessageProvider}#: {RandomizerText.GetObjectiveText()}\n{RandomizerText.MapFilterText}";
            if (msg.Count(c => c == '\n') > 1)
                msg = "\n" + msg; // paddingu paddingu...
            ObjectiveText.SetMessage(new MessageDescriptor(msg));
            ObjectiveText.gameObject.SetActive(true);
        } else {
            ObjectiveText.gameObject.SetActive(false);
        }

        if (GameMapTransitionManager.Instance.InAreaMapMode) {
            if (Input.Legend.OnPressed)
                AreaMapLegend.Toggle();
            if (RandomizerRebinding.ToggleMapMode.OnPressed) {
                RandomizerSettings.CurrentFilter = RandomizerSettings.CurrentFilter.Next();
                IconManager.ShowAreaIcons();
            }
        }
    }

    public void UpdateCurrentArea() {
        var scrollPosition = Navigation.ScrollPosition;
        foreach (RuntimeGameWorldArea runtimeGameWorldArea in GameWorld.Instance.RuntimeAreas) {
            if ((runtimeGameWorldArea.AreaDiscovered || DebugNavigation.UndiscoveredMapVisible) && runtimeGameWorldArea.Area.BoundaryCage.FindFaceAtPositionFaster(scrollPosition) != null) {
                if (GameMapUI.Instance.CurrentHighlightedArea != runtimeGameWorldArea && ChangeSelectedAreaSound) {
                    Sound.Play(ChangeSelectedAreaSound.GetSound(null), transform.position, null);
                }

                GameMapUI.Instance.CurrentHighlightedArea = runtimeGameWorldArea;
                break;
            }
        }
    }

    public Vector3 PlayerMarkerWorldPosition {
        get {
            var target = UI.Cameras.Current.Target;
            return target.position + PlayerPositionOffset + Vector3.up;
        }
    }

    public Vector3 SoulFlameMarkerWorldPosition => Characters.Sein.SoulFlame.SoulFlamePosition + PlayerPositionOffset + Vector3.up;

    private void UpdatePlayerPositionMarker() {
        if (PlayerPositionMarker) {
            PlayerPositionMarker.transform.localPosition = Navigation.WorldToMapPosition(PlayerMarkerWorldPosition);
        }
    }

    private void UpdateSoulFlamePositionMarker() {
        if (SoulFlamePositionMarker == null) {
            return;
        }

        if (Characters.Sein) {
            if (Characters.Sein.SoulFlame.SoulFlameExists) {
                SoulFlamePositionMarker.SetActive(true);
                SoulFlamePositionMarker.transform.localPosition = Navigation.WorldToMapPosition(SoulFlameMarkerWorldPosition);
            } else {
                SoulFlamePositionMarker.SetActive(false);
            }
        }
    }

    public bool IsSuspended { get; set; }

    public static AreaMapUI Instance;

    public List<AreaMapCanvas> Canvases = new List<AreaMapCanvas>();


    public GameObject PlayerPositionMarkerPrefab;

    public GameObject SoulFlamePositionMarkerPrefab;

    public GameObject TeleportPrefab;

    public GameObject ObjectivePrefab;

    public GameObject IconPrefab;

    public SoundProvider OpenSound;

    public SoundProvider CloseSound;

    public SoundProvider ChangeSelectedAreaSound;

    public MessageBox ObjectiveText;

    public TransparencyAnimator FadeOutAnimator;

    public AreaMapLegend AreaMapLegend;

    public MessageProvider ObjectiveMessageProvider;

    public MessageProvider CompletedMessageProvider;

    public Vector3 PlayerPositionOffset;

    [NonSerialized] public MessageBox RandomizerTooltip;

    [NonSerialized] public List<MessageBox> KeysanityDoorTooltips = new List<MessageBox>();
}

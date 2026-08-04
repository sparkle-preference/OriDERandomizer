using System;
using Game;
using UnityEngine;
using Input = Core.Input;

public class AreaMapNavigation : MonoBehaviour {
    public float ZoomTime => GameMapTransitionManager.Instance.ZoomTime;

    public float Zoom {
        get {
            if (ZoomTime < 1f) {
                return 1f / Mathf.Lerp(50f / WorldMapZoomLevel, 50f / AreaMapZoomLevel, Mathf.SmoothStep(0f, 1f, ZoomTime));
            }

            return 1f / Mathf.Lerp(50f / AreaMapZoomLevel, 50f / AreaMapCloseZoomLevel, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 2f, ZoomTime)));
        }
    }

    public void Awake() {
        areaMapUi = GetComponent<AreaMapUI>();
        AreaMapZoomLevel = 0.65f;
    }

    public void OnDisable() {
        ScrollSound.Stop();
    }

    public Bounds Bounds { get; set; }

    public Vector2 MapPlanePosition {
        get => MapPivot.localPosition;
        set => MapPivot.localPosition = value;
    }

    public Vector2 MapPlaneSize {
        get => MapPivot.localScale;
        set {
            var localScale = MapPivot.localScale;
            localScale.x = value.x;
            localScale.y = value.y;
            MapPivot.localScale = localScale;
        }
    }

    public void Advance() {
        HandleMapScrolling();
        UpdatePlane();
        HandleObjectiveFocus();
        HandleRandomizerTooltip();
    }

    public void HandleObjectiveFocus() {
        var isTransitioning = GameMapTransitionManager.Instance.IsTransitioning;
        focusTime = Mathf.Clamp01(focusTime - 2f * Time.deltaTime);
        if (focusTime > 0f) {
            ScrollPosition = Vector2.Lerp(fromPosition, toPosition, Mathf.SmoothStep(1f, 0f, focusTime));
            scrollTime = 0f;
        }

        if (!isTransitioning && focusTime == 0f && Input.Focus.OnPressed && !Input.Focus.Used) {
            Input.Focus.Used = true;
            focusTime = 1f;
            fromPosition = ScrollPosition;
            if (Objectives.All.Count == 0) {
                toggleToPlayer = true;
            }

            toPosition = !toggleToPlayer ? Objectives.All[0].Position : (Vector2)Characters.Current.Position;
            toggleToPlayer = !toggleToPlayer;
            if (FocusSound) {
                FocusSound.Play();
            }
        }
    }

    public void Init() {
        toggleToPlayer = false;
    }

    public void UpdatePlane() {
        MapPlaneSize = Vector2.one * Zoom;
        MapPivot.position = -ScrollPosition * Zoom;
    }

    public void CenterMapOnWorldPosition(Vector3 position) {
        scrollTime = 0f;
        ScrollPosition = position;
    }

    public Vector3 WorldToMapPosition(Vector2 position) {
        return MapPivot.TransformPoint(position);
    }

    public Vector3 MapToWorldPosition(Vector2 position) {
        var v = position - MapPlanePosition;
        v.x /= MapPlaneSize.x;
        v.y /= MapPlaneSize.y;
        return v;
    }

    private void HandleMapScrolling() {
        if (!GameMapTransitionManager.Instance.InAreaMapMode) {
            return;
        }

        if (GameMapUI.Instance.ShowingObjective || GameMapUI.Instance.RevealingMap) {
            return;
        }

        var vector = Vector2.zero;
        var cursorPositionUI = Input.CursorPositionUI;
        cursorPositionUI.x /= MapPlaneSize.x;
        cursorPositionUI.y /= MapPlaneSize.y;
        if (Input.LeftClick.OnPressed) {
            lastDragPosition = cursorPositionUI;
        }

        if (Input.LeftClick.Pressed && Input.CursorMoved) {
            vector += lastDragPosition - cursorPositionUI;
            lastDragPosition = cursorPositionUI;
        }

        if (Input.Axis.magnitude < 0.02) {
            scrollTime = 0f;
        } else {
            scrollTime = Mathf.Clamp01(scrollTime + Time.deltaTime * 4f);
            vector = Input.Axis.normalized * ScrollingSensitivityCurve.Evaluate(Input.Axis.magnitude) * scrollTime;
            vector *= Time.deltaTime * 150f / Zoom;
        }

        if (vector.magnitude > 0f) {
            if (vector.x < 0f && ScrollPosition.x <= scrollAreaLimit.xMin) {
                vector.x = 0f;
            }

            if (vector.x > 0f && ScrollPosition.x >= scrollAreaLimit.xMax) {
                vector.x = 0f;
            }

            if (vector.y < 0f && ScrollPosition.y <= scrollAreaLimit.yMin) {
                vector.y = 0f;
            }

            if (vector.y > 0f && ScrollPosition.y >= scrollAreaLimit.yMax) {
                vector.y = 0f;
            }

            ScrollPosition += vector;
            if (ScrollSound && !ScrollSound.IsPlaying && vector.magnitude >= 0.3) {
                ScrollSound.Play();
                return;
            }

            if (ScrollSound && ScrollSound.IsPlaying && vector.magnitude < 0.3) {
                ScrollSound.StopAndFadeOut(0f);
            }
        } else if (ScrollSound && ScrollSound.IsPlaying) {
            ScrollSound.StopAndFadeOut(0f);
        }
    }

    public void UpdateScrollLimits() {
        var flag = false;
        var num = 0f;
        var num2 = 0f;
        var num3 = 0f;
        var num4 = 0f;
        foreach (RuntimeGameWorldArea runtimeGameWorldArea in GameWorld.Instance.RuntimeAreas) {
            var area = runtimeGameWorldArea.Area;
            var facesAsRectangles = area.CageStructureTool.FacesAsRectangles;
            for (var i = 0; i < area.CageStructureTool.Faces.Count; i++) {
                var rect = facesAsRectangles[i];
                if (flag) {
                    num = Mathf.Min(num, rect.xMin);
                    num2 = Mathf.Min(num2, rect.yMin);
                    num3 = Mathf.Max(num3, rect.xMax);
                    num4 = Mathf.Max(num4, rect.yMax);
                } else {
                    flag = true;
                    num = rect.xMin;
                    num2 = rect.yMin;
                    num3 = rect.xMax;
                    num4 = rect.yMax;
                }
            }
        }

        for (var j = 0; j < Objectives.All.Count; j++) {
            var position = Objectives.All[j].Position;
            num = Mathf.Min(num, position.x);
            num2 = Mathf.Min(num2, position.y);
            num3 = Mathf.Max(num3, position.x);
            num4 = Mathf.Max(num4, position.y);
        }

        if (Characters.Sein) {
            Vector2 vector = Characters.Sein.Position;
            num = Mathf.Min(num, vector.x);
            num2 = Mathf.Min(num2, vector.y);
            num3 = Mathf.Max(num3, vector.x);
            num4 = Mathf.Max(num4, vector.y);
        }

        scrollAreaLimit.xMin = num;
        scrollAreaLimit.yMin = num2;
        scrollAreaLimit.xMax = num3;
        scrollAreaLimit.yMax = num4;
    }

    public void HandleRandomizerTooltip() {
        try {
            Vector2 cursorPositionWorld = MapToWorldPosition(Input.CursorPositionUI);
            RuntimeWorldMapIcon candidate = null;
            var candidateDistance = Mathf.Infinity;
            var doorCount = 0;
            var zoomScaleFactor = (float)Math.Pow(Zoom / 0.04f, .45f); // please don't ask me how i got these numbers
            var offset = .45f * (float)Math.Pow(zoomScaleFactor, 1.5f); // it's kind of a dumb story
            var textScale = new Vector3(0.3f * zoomScaleFactor, 0.3f * zoomScaleFactor, 0.3f); // but they work well i prommy

            foreach (RuntimeGameWorldArea runtimeArea in GameWorld.Instance.RuntimeAreas)
            foreach (var runtimeIcon in runtimeArea.Icons) {
                if (!runtimeIcon.IsVisible(areaMapUi) || runtimeIcon.Icon == WorldMapIconType.Invisible) {
                    continue;
                }

                if (RandomizerSettings.Customization.AlwaysShowDoorHints.Value && RandomizerLocationManager.KeystoneDoorMapGuidToMoonGuid.ContainsKey(runtimeIcon.Guid)) {
                    var text = Randomizer.Keysanity.MapHintForDoor(RandomizerLocationManager.KeystoneDoorMapGuidToMoonGuid[runtimeIcon.Guid]).Replace("\n(Touch door to get hint!)", "");
                    var pos = WorldToMapPosition(runtimeIcon.Position);
                    pos.y -= text.Contains("\n") ? offset : offset * 0.6f; // smaller offset for 1 liners
                    AreaMapUI.Instance.KeysanityDoorTooltips[doorCount].transform.localScale = textScale;
                    AreaMapUI.Instance.KeysanityDoorTooltips[doorCount].transform.position = pos;
                    AreaMapUI.Instance.KeysanityDoorTooltips[doorCount].OverrideText = text;
                    AreaMapUI.Instance.KeysanityDoorTooltips[doorCount].gameObject.SetActive(true);
                    doorCount++;
                    continue;
                }

                if (Mathf.Abs(runtimeIcon.Position.x - cursorPositionWorld.x) > 12f || Mathf.Abs(runtimeIcon.Position.y - cursorPositionWorld.y) > 12f) {
                    continue;
                }

                if (!RandomizerLocationManager.LocationsByWorldMapGuid.ContainsKey(runtimeIcon.Guid) && !RandomizerLocationManager.KeystoneDoorMapGuidToMoonGuid.ContainsKey(runtimeIcon.Guid)) {
                    continue;
                }

                var distance = Vector2.Distance(runtimeIcon.Position, cursorPositionWorld);

                if (distance > 12f || distance > candidateDistance) {
                    continue;
                }

                candidateDistance = distance;
                candidate = runtimeIcon;
            }

            if (candidate == null) {
                AreaMapUI.Instance.RandomizerTooltip.gameObject.SetActive(false);
                return;
            }

            var candidatePosition = WorldToMapPosition(candidate.Position);
            candidatePosition.y -= offset;
            AreaMapUI.Instance.RandomizerTooltip.transform.position = candidatePosition;
            AreaMapUI.Instance.RandomizerTooltip.transform.localScale = textScale;

            if (RandomizerLocationManager.LocationsByWorldMapGuid.TryGetValue(candidate.Guid, out var pickupLocation)) {
                AreaMapUI.Instance.RandomizerTooltip.OverrideText = pickupLocation.FriendlyName;
                if (DebugMenuB.DebugControlsEnabled && (MoonInput.GetKey(KeyCode.LeftShift) || MoonInput.GetKey(KeyCode.RightShift)) && Input.RightClick.OnPressed) {
                    candidate.Hide();
                    RandomizerLocationManager.GivePickupByWorldMapGuid(candidate.Guid);
                }
            } else {
                AreaMapUI.Instance.RandomizerTooltip.OverrideText = Randomizer.Keysanity.MapHintForDoor(RandomizerLocationManager.KeystoneDoorMapGuidToMoonGuid[candidate.Guid]);
            }

            AreaMapUI.Instance.RandomizerTooltip.gameObject.SetActive(true);
        } catch (Exception e) {
            Randomizer.Log("HandleRandomizerTooltip: " + e.Message);
        }
    }

    public Transform MapPivot;

    public float AreaMapZoomLevel = 1f;

    public float WorldMapZoomLevel = 0.5f;

    public float AreaMapCloseZoomLevel = 3f;

    private float scrollTime;

    public AnimationCurve ScrollingSensitivityCurve;

    private Vector2 lastDragPosition;

    public SoundSource ScrollSound;

    public SoundSource FocusSound;

    public Vector2 ScrollPosition;

    private AreaMapUI areaMapUi;

    private Vector2 fromPosition;

    private Vector2 toPosition;

    private float focusTime;

    private bool toggleToPlayer;

    private Rect scrollAreaLimit;
}

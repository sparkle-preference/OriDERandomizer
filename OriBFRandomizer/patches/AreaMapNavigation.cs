using System;
using Core;
using Game;
using HarmonyLib;
using OriBFRandomizer.patches;
using UnityEngine;


[HarmonyPatch(typeof(AreaMapNavigation))]
public static class AreaMapNavigationAdvancePatches
{
    [HarmonyPostfix]
    [HarmonyPatch("Awake")]
    public static void AwakePatch(AreaMapNavigation __instance)
    {
        __instance.AreaMapZoomLevel = 1f;
    }

    [HarmonyPostfix]
    [HarmonyPatch("UpdatePlane")]
    public static void UpdatePlanePatch(AreaMapNavigation __instance)
    {
        __instance.MapPivot.position = -__instance.ScrollPosition * __instance.Zoom;
    }

    [HarmonyPostfix]
    [HarmonyPatch("Advance")]
    public static void AdvancePatch(AreaMapNavigation __instance, AreaMapUI ___m_areaMapUi)
    {
        Vector2 cursorPositionWorld = (Vector2) __instance.MapToWorldPosition(Core.Input.CursorPositionUI);
        RuntimeWorldMapIcon candidate = null;
        string candidateArea = null;
        float candidateDistance = Mathf.Infinity;

        foreach (RuntimeGameWorldArea runtimeArea in GameWorld.Instance.RuntimeAreas)
        {
            foreach (RuntimeWorldMapIcon runtimeIcon in runtimeArea.Icons)
            {
                if (!runtimeIcon.IsVisible(___m_areaMapUi) || runtimeIcon.Icon == WorldMapIconType.Invisible)
                {
                    continue;
                }

                if (Mathf.Abs(runtimeIcon.Position.x - cursorPositionWorld.x) > 12f ||
                    Mathf.Abs(runtimeIcon.Position.y - cursorPositionWorld.y) > 12f)
                {
                    continue;
                }

                if (!RandomizerLocationManager.LocationsByWorldMapGuid.ContainsKey(runtimeIcon.Guid))
                {
                    continue;
                }

                float distance = Vector2.Distance(runtimeIcon.Position, cursorPositionWorld);

                if (distance > 12f || distance > candidateDistance)
                {
                    continue;
                }

                candidateDistance = distance;
                candidateArea = runtimeArea.Area.AreaIdentifier;
                candidate = runtimeIcon;
            }
        }

        if (candidate == null)
        {
            AreaMapUIExt.RandomizerTooltips[AreaMapUI.Instance].gameObject.SetActive(false);
            return;
        }

        Vector3 candidatePosition = __instance.WorldToMapPosition(candidate.Position);
        candidatePosition.y -= 0.20f;
        AreaMapUIExt.RandomizerTooltips[AreaMapUI.Instance].transform.position = candidatePosition;

        RandomizerLocationManager.Location pickupLocation =
            RandomizerLocationManager.LocationsByWorldMapGuid[candidate.Guid];
        AreaMapUIExt.RandomizerTooltips[AreaMapUI.Instance].OverrideText = pickupLocation.FriendlyName;
        AreaMapUIExt.RandomizerTooltips[AreaMapUI.Instance].gameObject.SetActive(true);

        if (DebugMenuB.DebugControlsEnabled &&
            (MoonInput.GetKey(KeyCode.LeftShift) || MoonInput.GetKey(KeyCode.RightShift)) &&
            Core.Input.RightClick.OnPressed)
        {
            candidate.Hide();
            RandomizerLocationManager.GivePickupByWorldMapGuid(candidate.Guid);
        }
    }
}
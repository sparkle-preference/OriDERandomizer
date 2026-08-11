using System.Collections.Generic;
using Game;
using UnityEngine;

public class RuntimeGameWorldArea {
    public RuntimeGameWorldArea(GameWorldArea area) {
        Area = area;
        Initialize();
    }

    public Vector2 FindCenterPositionOnDiscoveredAreas() {
        var num = 0;
        var a = Vector2.zero;
        var facesAsRectangles = Area.CageStructureTool.FacesAsRectangles;
        for (var i = 0; i < Area.CageStructureTool.Faces.Count; i++) {
            var face = Area.CageStructureTool.Faces[i];
            if (IsDiscovered(face)) {
                var rect = facesAsRectangles[i];
                a += rect.center;
                num++;
            }
        }

        if (num > 0) {
            return a / num;
        }

        return Area.BoundingRect.center;
    }

    public Vector2 FindCenterPositionOnUndiscoveredAreas() {
        var num = 0;
        var a = Vector2.zero;
        var facesAsRectangles = Area.CageStructureTool.FacesAsRectangles;
        for (var i = 0; i < Area.CageStructureTool.Faces.Count; i++) {
            var face = Area.CageStructureTool.Faces[i];
            if (!IsDiscovered(face)) {
                var rect = facesAsRectangles[i];
                a += rect.center;
                num++;
            }
        }

        if (num > 0) {
            return a / num;
        }

        return Area.BoundingRect.center;
    }

    public void Initialize() {
        m_dirtyCompletionAmount = true;
        Icons.Clear();
        Icons.Capacity = Area.Icons.Count;
        foreach (var icon in Area.Icons) {
            Icons.Add(new RuntimeWorldMapIcon(icon, this));
        }

        m_worldAreaStates.Clear();
    }

    public bool AreaDiscovered => m_worldAreaStates.Count > 0;

    public float CompletionAmount {
        get {
            if (m_dirtyCompletionAmount) {
                m_dirtyCompletionAmount = false;
                UpdateCompletionAmount();
            }

            return m_completionAmount;
        }
    }

    public void DirtyCompletionAmount() {
        m_dirtyCompletionAmount = true;
    }

    public int CompletionPercentage => Mathf.RoundToInt(CompletionAmount * 100f);

    private bool IconIsCompletionType(WorldMapIconType type) {
        switch (type) {
            case WorldMapIconType.HealthUpgrade:
            case WorldMapIconType.EnergyUpgrade:
            case WorldMapIconType.AbilityPoint:
            case WorldMapIconType.Experience:
            case WorldMapIconType.MapstonePickup:
                break;
            default:
                if (type != WorldMapIconType.Keystone) {
                    return false;
                }

                break;
        }

        return true;
    }

    public void UpdateCompletionAmount() {
        var total = RandomizerStatsManager.PickupCounts[Area.AreaIdentifier];
        var collected = RandomizerStatsManager.GetObtainedPickupCount(Area.AreaIdentifier);

        if (RandomizerTrackedDataManager.MapBitsByArea.ContainsKey(Area.AreaIdentifier)) {
            total++;
            if (RandomizerTrackedDataManager.GetMapstone(Area.AreaIdentifier)) {
                collected++;
            }
        }

        m_completionAmount = collected / (float)total;
    }

    public void VisitMapAreaAtPosition(Vector3 worldPosition) {
        var position = Area.CageStructureTool.transform.InverseTransformPoint(worldPosition);
        var face = Area.CageStructureTool.FindFaceAtPositionFaster(position);
        if (face != null) {
            if (m_worldAreaStates.TryGetValue(face.ID, out var worldMapAreaState)) {
                if (worldMapAreaState != WorldMapAreaState.Visited) {
                    m_dirtyCompletionAmount = true;
                    m_worldAreaStates[face.ID] = WorldMapAreaState.Visited;
                }
            } else {
                m_dirtyCompletionAmount = true;
                m_worldAreaStates[face.ID] = WorldMapAreaState.Visited;
            }
        }
    }

    private bool HasSenseAbility => Characters.Sein && Characters.Sein.PlayerAbilities.Sense.HasAbility;

    public bool IsHidden(Vector3 worldPosition) {
        if (HasSenseAbility) {
            return false;
        }

        var position = Area.CageStructureTool.transform.InverseTransformPoint(worldPosition);
        var face = Area.CageStructureTool.FindFaceAtPositionFaster(position);
        return face == null || IsHidden(face);
    }

    public bool IsDiscovered(Vector3 worldPosition) {
        var position = Area.CageStructureTool.transform.InverseTransformPoint(worldPosition);
        var face = Area.CageStructureTool.FindFaceAtPositionFaster(position);
        return face != null && IsDiscovered(face);
    }

    public bool IsHidden(CageStructureTool.Face face) {
        return !m_worldAreaStates.ContainsKey(face.ID) || m_worldAreaStates[face.ID] == WorldMapAreaState.Hidden;
    }

    public bool IsDiscovered(CageStructureTool.Face face) {
        return m_worldAreaStates.ContainsKey(face.ID) && m_worldAreaStates[face.ID] == WorldMapAreaState.Discovered;
    }

    public void Serialize(Archive ar) {
        if (ar.Reading) {
            m_dirtyCompletionAmount = true;
            m_worldAreaStates.Clear();
            var num = ar.Serialize(0);
            for (var i = 0; i < num; i++) {
                var key = ar.Serialize(0);
                var value = (WorldMapAreaState)ar.Serialize(0);
                m_worldAreaStates.Add(key, value);
            }

            num = ar.Serialize(0);
            for (var j = 0; j < num; j++) {
                var guid = MoonGuid.Empty;
                guid.Serialize(ar);
                var icon = (WorldMapIconType)ar.Serialize(0);
                var runtimeWorldMapIcon = Icons.Find(a => a.Guid == guid);
                if (runtimeWorldMapIcon != null) {
                    runtimeWorldMapIcon.Icon = icon;
                }
            }
        } else {
            ar.Serialize(m_worldAreaStates.Count);
            foreach (var keyValuePair in m_worldAreaStates) {
                ar.Serialize(keyValuePair.Key);
                ar.Serialize((int)keyValuePair.Value);
            }

            ar.Serialize(Icons.Count);
            foreach (var runtimeWorldMapIcon2 in Icons) {
                runtimeWorldMapIcon2.Guid.Serialize(ar);
                ar.Serialize((int)runtimeWorldMapIcon2.Icon);
            }
        }
    }

    public void DiscoverAllAreas() {
        var cageStructureTool = Area.CageStructureTool;
        foreach (var face in cageStructureTool.Faces) {
            if (!m_worldAreaStates.ContainsKey(face.ID)) {
                m_worldAreaStates[face.ID] = WorldMapAreaState.Discovered;
            }
        }
    }

    public void VisitAllAreas() {
        m_worldAreaStates.Clear();
        var cageStructureTool = Area.CageStructureTool;
        foreach (var face in cageStructureTool.Faces) {
            m_worldAreaStates[face.ID] = WorldMapAreaState.Visited;
        }
    }

    public bool FaceIsDiscoveredOrVisited(int id) {
        return m_worldAreaStates.TryGetValue(id, out var worldMapAreaState) && (worldMapAreaState == WorldMapAreaState.Discovered || worldMapAreaState == WorldMapAreaState.Visited);
    }

    public WorldMapAreaState GetFaceState(int id) {
        if (m_worldAreaStates.TryGetValue(id, out var result)) {
        }

        return result;
    }

    public GameWorldArea Area;

    public List<RuntimeWorldMapIcon> Icons = new List<RuntimeWorldMapIcon>();

    private readonly Dictionary<int, WorldMapAreaState> m_worldAreaStates = new Dictionary<int, WorldMapAreaState>();

    private float m_completionAmount;

    private bool m_dirtyCompletionAmount;
}

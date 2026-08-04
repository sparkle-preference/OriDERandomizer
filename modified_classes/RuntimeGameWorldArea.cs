using System.Collections.Generic;
using Game;
using UnityEngine;

public class RuntimeGameWorldArea
{
	public RuntimeGameWorldArea(GameWorldArea area)
	{
		Area = area;
		Initialize();
	}

	public Vector2 FindCenterPositionOnDiscoveredAreas()
	{
		int num = 0;
		Vector2 a = Vector2.zero;
		Rect[] facesAsRectangles = Area.CageStructureTool.FacesAsRectangles;
		for (int i = 0; i < Area.CageStructureTool.Faces.Count; i++)
		{
			CageStructureTool.Face face = Area.CageStructureTool.Faces[i];
			if (IsDiscovered(face))
			{
				Rect rect = facesAsRectangles[i];
				a += rect.center;
				num++;
			}
		}
		if (num > 0)
		{
			return a / num;
		}
		return Area.BoundingRect.center;
	}

	public Vector2 FindCenterPositionOnUndiscoveredAreas()
	{
		int num = 0;
		Vector2 a = Vector2.zero;
		Rect[] facesAsRectangles = Area.CageStructureTool.FacesAsRectangles;
		for (int i = 0; i < Area.CageStructureTool.Faces.Count; i++)
		{
			CageStructureTool.Face face = Area.CageStructureTool.Faces[i];
			if (!IsDiscovered(face))
			{
				Rect rect = facesAsRectangles[i];
				a += rect.center;
				num++;
			}
		}
		if (num > 0)
		{
			return a / num;
		}
		return Area.BoundingRect.center;
	}

	public void Initialize()
	{
		m_dirtyCompletionAmount = true;
		Icons.Clear();
		Icons.Capacity = Area.Icons.Count;
		foreach (GameWorldArea.WorldMapIcon icon in Area.Icons)
		{
			Icons.Add(new RuntimeWorldMapIcon(icon, this));
		}
		m_worldAreaStates.Clear();
	}

	public bool AreaDiscovered => m_worldAreaStates.Count > 0;

	public float CompletionAmount
	{
		get
		{
			if (m_dirtyCompletionAmount)
			{
				m_dirtyCompletionAmount = false;
				UpdateCompletionAmount();
			}
			return m_completionAmount;
		}
	}

	public void DirtyCompletionAmount()
	{
		m_dirtyCompletionAmount = true;
	}

	public int CompletionPercentage => Mathf.RoundToInt(CompletionAmount * 100f);

	private bool IconIsCompletionType(WorldMapIconType type)
	{
		switch (type)
		{
		case WorldMapIconType.HealthUpgrade:
		case WorldMapIconType.EnergyUpgrade:
		case WorldMapIconType.AbilityPoint:
		case WorldMapIconType.Experience:
		case WorldMapIconType.MapstonePickup:
			break;
		default:
			if (type != WorldMapIconType.Keystone)
			{
				return false;
			}
			break;
		}
		return true;
	}

	public void UpdateCompletionAmount()
	{
		int total = RandomizerStatsManager.PickupCounts[Area.AreaIdentifier];
		int collected = RandomizerStatsManager.GetObtainedPickupCount(Area.AreaIdentifier);

		if (RandomizerTrackedDataManager.MapBitsByArea.ContainsKey(Area.AreaIdentifier))
		{
			total++;
			if (RandomizerTrackedDataManager.GetMapstone(Area.AreaIdentifier))
				collected++;
		}

		m_completionAmount = collected / (float)total;
	}

	public void VisitMapAreaAtPosition(Vector3 worldPosition)
	{
		Vector3 position = Area.CageStructureTool.transform.InverseTransformPoint(worldPosition);
		CageStructureTool.Face face = Area.CageStructureTool.FindFaceAtPositionFaster(position);
		if (face != null)
		{
			WorldMapAreaState worldMapAreaState;
			if (m_worldAreaStates.TryGetValue(face.ID, out worldMapAreaState))
			{
				if (worldMapAreaState != WorldMapAreaState.Visited)
				{
					m_dirtyCompletionAmount = true;
					m_worldAreaStates[face.ID] = WorldMapAreaState.Visited;
				}
			}
			else
			{
				m_dirtyCompletionAmount = true;
				m_worldAreaStates[face.ID] = WorldMapAreaState.Visited;
			}
		}
	}

	private bool HasSenseAbility => Characters.Sein && Characters.Sein.PlayerAbilities.Sense.HasAbility;

	public bool IsHidden(Vector3 worldPosition)
	{
		if (HasSenseAbility)
		{
			return false;
		}
		Vector3 position = Area.CageStructureTool.transform.InverseTransformPoint(worldPosition);
		CageStructureTool.Face face = Area.CageStructureTool.FindFaceAtPositionFaster(position);
		return face == null || IsHidden(face);
	}

	public bool IsDiscovered(Vector3 worldPosition)
	{
		Vector3 position = Area.CageStructureTool.transform.InverseTransformPoint(worldPosition);
		CageStructureTool.Face face = Area.CageStructureTool.FindFaceAtPositionFaster(position);
		return face != null && IsDiscovered(face);
	}

	public bool IsHidden(CageStructureTool.Face face)
	{
		return !m_worldAreaStates.ContainsKey(face.ID) || m_worldAreaStates[face.ID] == WorldMapAreaState.Hidden;
	}

	public bool IsDiscovered(CageStructureTool.Face face)
	{
		return m_worldAreaStates.ContainsKey(face.ID) && m_worldAreaStates[face.ID] == WorldMapAreaState.Discovered;
	}

	public void Serialize(Archive ar)
	{
		if (ar.Reading)
		{
			m_dirtyCompletionAmount = true;
			m_worldAreaStates.Clear();
			int num = ar.Serialize(0);
			for (int i = 0; i < num; i++)
			{
				int key = ar.Serialize(0);
				WorldMapAreaState value = (WorldMapAreaState)ar.Serialize(0);
				m_worldAreaStates.Add(key, value);
			}
			num = ar.Serialize(0);
			for (int j = 0; j < num; j++)
			{
				MoonGuid guid = MoonGuid.Empty;
				guid.Serialize(ar);
				WorldMapIconType icon = (WorldMapIconType)ar.Serialize(0);
				RuntimeWorldMapIcon runtimeWorldMapIcon = Icons.Find(a => a.Guid == guid);
				if (runtimeWorldMapIcon != null)
				{
					runtimeWorldMapIcon.Icon = icon;
				}
			}
		}
		else
		{
			ar.Serialize(m_worldAreaStates.Count);
			foreach (KeyValuePair<int, WorldMapAreaState> keyValuePair in m_worldAreaStates)
			{
				ar.Serialize(keyValuePair.Key);
				ar.Serialize((int)keyValuePair.Value);
			}
			ar.Serialize(Icons.Count);
			foreach (RuntimeWorldMapIcon runtimeWorldMapIcon2 in Icons)
			{
				runtimeWorldMapIcon2.Guid.Serialize(ar);
				ar.Serialize((int)runtimeWorldMapIcon2.Icon);
			}
		}
	}

	public void DiscoverAllAreas()
	{
		CageStructureTool cageStructureTool = Area.CageStructureTool;
		foreach (CageStructureTool.Face face in cageStructureTool.Faces)
		{
			if (!m_worldAreaStates.ContainsKey(face.ID))
			{
				m_worldAreaStates[face.ID] = WorldMapAreaState.Discovered;
			}
		}
	}

	public void VisitAllAreas()
	{
		m_worldAreaStates.Clear();
		CageStructureTool cageStructureTool = Area.CageStructureTool;
		foreach (CageStructureTool.Face face in cageStructureTool.Faces)
		{
			m_worldAreaStates[face.ID] = WorldMapAreaState.Visited;
		}
	}

	public bool FaceIsDiscoveredOrVisited(int id)
	{
		WorldMapAreaState worldMapAreaState;
		return m_worldAreaStates.TryGetValue(id, out worldMapAreaState) && (worldMapAreaState == WorldMapAreaState.Discovered || worldMapAreaState == WorldMapAreaState.Visited);
	}

	public WorldMapAreaState GetFaceState(int id)
	{
		WorldMapAreaState result;
		if (m_worldAreaStates.TryGetValue(id, out result))
		{
		}
		return result;
	}

	public GameWorldArea Area;

	public List<RuntimeWorldMapIcon> Icons = new List<RuntimeWorldMapIcon>();

	private readonly Dictionary<int, WorldMapAreaState> m_worldAreaStates = new Dictionary<int, WorldMapAreaState>();

	private float m_completionAmount;

	private bool m_dirtyCompletionAmount;
}

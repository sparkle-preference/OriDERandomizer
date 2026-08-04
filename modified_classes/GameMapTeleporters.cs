using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class GameMapTeleporters : MonoBehaviour
{
	public List<GameMapTeleporter> Teleporters => TeleporterController.Instance.Teleporters;

	[ContextMenu("Show teleporters")]
	public void ShowTeleporters()
	{
		foreach (GameMapTeleporter gameMapTeleporter in Teleporters)
		{
			if (gameMapTeleporter.Activated)
			{
				gameMapTeleporter.Show();
			}
		}
	}

	public void HideTeleporters()
	{
		foreach (GameMapTeleporter gameMapTeleporter in Teleporters)
		{
			gameMapTeleporter.Hide();
		}
	}

	private void ChangeSelection(int index)
	{
		if (SelectedIndex == index)
		{
			return;
		}
		SetIndex(index);
		if (SwitchTeleporterSelectionSound)
		{
			Sound.Play(SwitchTeleporterSelectionSound.GetSound(null), transform.position, null);
		}
		if (GameMapTransitionManager.Instance.InWorldMapMode)
		{
			AreaMapUI.Instance.Navigation.ScrollPosition = Teleporters[index].WorldPosition;
		}
	}

	private int TeleporterUnderMouse()
	{
		int result = -1;
        if (Teleporters.Count <= 12) {
            // There are no custom teleporters, so use the default behaviour.
            if (GameMapTransitionManager.Instance.InWorldMapMode)
            {
                for (int i = 0; i < Teleporters.Count; i++)
                {
                    GameMapTeleporter gameMapTeleporter = Teleporters[i];
                    if (gameMapTeleporter.Activated && Vector3.Distance(Input.CursorPositionUI, gameMapTeleporter.WorldMapIconPosition) < 1f)
                    {
                        result = i;
                    }
                }
            }
            if (GameMapTransitionManager.Instance.InAreaMapMode)
            {
                for (int j = 0; j < Teleporters.Count; j++)
                {
                    GameMapTeleporter gameMapTeleporter2 = Teleporters[j];
                    if (gameMapTeleporter2.Activated && Vector3.Distance(Input.CursorPositionUI, gameMapTeleporter2.AreaMapIconPosition) < 1f)
                    {
                        result = j;
                    }
                }
            }
        } 
        else
        {
            // There are custom teleporters, so use our mouse algorithm.
            // The default algorithm only finds the *last* teleporter within 1f, we find the closest.
            // The gameMapTeleporter.WorldMapIconPosition is centered left and right, but is at the
            // top of the teleporter icon, so we remove roughly half the height to centre it.
			float minimum = 1f;
			for (int k = 0; k < Teleporters.Count; k++)
			{
				GameMapTeleporter gameMapTeleporter3 = Teleporters[k];
				if (gameMapTeleporter3.Activated)
				{
					Vector2 teleporterCenter;
					if (GameMapTransitionManager.Instance.InWorldMapMode)
					{
						teleporterCenter = new Vector2(gameMapTeleporter3.WorldMapIconPosition.x, gameMapTeleporter3.WorldMapIconPosition.y - 0.125f);
					}
					else
					{
						teleporterCenter = new Vector2(gameMapTeleporter3.AreaMapIconPosition.x, gameMapTeleporter3.AreaMapIconPosition.y - 0.125f);
					}
					float distance = Vector3.Distance(Input.CursorPositionUI, teleporterCenter);
					if (distance < minimum)
					{
						result = k;
						minimum = distance;
					}
				}
			}
        }
		return result;
	}

	private void AdvanceWorldMap()
	{
		m_flyBackTime = 0f;
		if (Input.Axis.magnitude < 0.5f)
		{
			m_released = true;
		}
		if (Input.CursorMoved)
		{
			int num = TeleporterUnderMouse();
			if (num != -1)
			{
				ChangeSelection(num);
			}
		}
		if (Input.Axis.magnitude > 0.5f && m_released)
		{
			Vector2 normalized = Input.Axis.normalized;
			Vector2 worldMapIconPosition = SelectedTeleporter.WorldMapIconPosition;
			int num2 = -1;
			float num3 = float.MaxValue;
			for (int i = 0; i < Teleporters.Count; i++)
			{
				GameMapTeleporter gameMapTeleporter = Teleporters[i];
				if (gameMapTeleporter.Activated)
				{
					Vector2 vector = gameMapTeleporter.WorldMapIconPosition - worldMapIconPosition;
					if (vector.magnitude < num3 && Vector3.Dot(vector.normalized, normalized) > 0.707f)
					{
						num3 = vector.magnitude;
						num2 = i;
					}
				}
			}
			if (num2 != -1)
			{
				m_released = false;
				ChangeSelection(num2);
			}
		}
	}

	private void AdvanceAreaMap()
	{
		if (Input.CursorMoved)
		{
			int num = TeleporterUnderMouse();
			if (num != -1)
			{
				ChangeSelection(num);
			}
		}
		if (AreaMapUI.Instance.Navigation.ScrollingSensitivityCurve.Evaluate(Input.Axis.magnitude) > 0f)
		{
			m_flyBackTime = 1.1f;
			m_previousScrollPosition = AreaMapUI.Instance.Navigation.ScrollPosition;
			float num2 = 9f;
			int index = SelectedIndex;
			for (int i = 0; i < Teleporters.Count; i++)
			{
				GameMapTeleporter gameMapTeleporter = Teleporters[i];
				if (gameMapTeleporter.Activated)
				{
					float magnitude = gameMapTeleporter.AreaMapIconPosition.magnitude;
					if (magnitude < num2)
					{
						index = i;
						num2 = magnitude;
					}
				}
			}
			ChangeSelection(index);
		}
		else
		{
			m_flyBackTime -= Time.deltaTime;
			if (m_flyBackTime < 1f && m_flyBackTime > 0f)
			{
				AreaMapUI.Instance.Navigation.ScrollPosition = Vector2.Lerp(m_previousScrollPosition, Teleporters[SelectedIndex].WorldPosition, 1f - Mathf.SmoothStep(0f, 1f, m_flyBackTime));
			}
		}
	}

	public void Advance()
	{
		if (!GameMapUI.Instance.ShowingTeleporters)
		{
			return;
		}
		foreach (GameMapTeleporter gameMapTeleporter in Teleporters)
		{
			gameMapTeleporter.Update();
		}
		if (GameMapTransitionManager.Instance.InWorldMapMode)
		{
			AdvanceWorldMap();
		}
		if (GameMapTransitionManager.Instance.InAreaMapMode)
		{
			AdvanceAreaMap();
		}
		if (Input.LeftClick.OnPressed)
		{
			m_clickedPosition = Input.CursorPositionUI;
		}
		bool flag = Input.LeftClick.OnReleased && Vector2.Distance(Input.CursorPositionUI, m_clickedPosition) < 0.01f && TeleporterUnderMouse() != -1;
		if (Input.ActionButtonA.OnPressed || flag)
		{
			UI.Menu.HideMenuScreen();
			if (SelectTeleporterSound)
			{
				Sound.Play(SelectTeleporterSound.GetSound(null), transform.position, null);
			}
			TeleporterController.BeginTeleportation(SelectedTeleporter);
		}
	}

	public void OnDisable()
	{
		HideTeleporters();
		if (GameMapUI.Instance.ShowingTeleporters)
		{
			TeleporterController.OnClose();
		}
	}

	public GameMapTeleporter SelectedTeleporter => Teleporters[SelectedIndex];

	public void Select(string identifier)
	{
		int num = Teleporters.FindIndex(a => a.Identifier == identifier);
		if (num != -1)
		{
			SetIndex(num);
		}
	}

	public void SetIndex(int index)
	{
		SelectedTeleporter.Dehighlight();
		SelectedIndex = index;
		SelectedTeleporter.Highlight();
		GameWorldArea area = GameWorld.Instance.FindAreaFromPosition(SelectedTeleporter.WorldPosition);
		GameMapUI.Instance.CurrentHighlightedArea = GameWorld.Instance.FindRuntimeArea(area);
	}

	public SoundProvider SelectTeleporterSound;

	public SoundProvider SwitchTeleporterSelectionSound;

	public SoundProvider StartTeleportingSound;

	public SoundProvider ReachDestinationTeleporterSound;

	public SoundProvider OpenWindowSound;

	public SoundProvider CloseWindowSound;

	public int SelectedIndex;

	private bool m_released = true;

	private Vector2 m_previousScrollPosition;

	private float m_flyBackTime;

	private Vector2 m_clickedPosition;
}

using System;
using System.Collections.Generic;
using UnityEngine;

public class HierarchyDebugMenu : MonoBehaviour
{
	public void Awake()
	{
		Style = Skin.FindStyle("debugMenuItem");
		SelectedStyle = Skin.FindStyle("selectedDebugMenuItem");
		PressedStyle = Skin.FindStyle("pressedDebugMenuItem");
		DebugMenuStyle = Skin.FindStyle("debugMenu");
		StyleEnabled = Skin.FindStyle("debugMenuItemEnabled");
		StyleDisabled = Skin.FindStyle("debugMenuItemDisabled");
	}

	public void OnEnable()
	{
		m_selectionIndex = 0;
		SuspensionManager.SuspendAll();
		m_items.Clear();
		foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
		{
			if (gameObject.hideFlags == HideFlags.None && gameObject.transform.parent == null && gameObject.activeInHierarchy == gameObject.activeSelf)
			{
				m_items.Add(new GameObjectItem(gameObject));
			}
		}
		m_items.Sort((a, b) => string.Compare(a.Target.name, b.Target.name, StringComparison.Ordinal));
	}

	public void OnDisable()
	{
		SuspensionManager.ResumeAll();
	}

	public void OnGUI()
	{
		int num = 0;
		int depth = 0;
		GUILayout.BeginArea(new Rect(Screen.width / 2 - 200f, 0f, 400f, Screen.height), GUI.skin.box);
		GUILayout.BeginVertical(GUI.skin.box);
		GUILayout.FlexibleSpace();
		foreach (GameObjectItem item in m_items)
		{
			OnItemGUI(item, ref num, depth);
		}
		m_maxIndex = num - 1;
		GUILayout.FlexibleSpace();
		GUILayout.EndVertical();
		GUILayout.EndArea();
	}

	public void MoveSelectionDown()
	{
		m_selectionIndex = Mathf.Min(m_maxIndex, m_selectionIndex + 1);
	}

	public void MoveSelectionUp()
	{
		m_selectionIndex = Mathf.Max(0, m_selectionIndex - 1);
	}

	private void ResetHold()
	{
		m_holdSpeed = 2f;
		m_holdAccumulation = 0f;
	}

	public void FixedUpdate()
	{
		if (Input.GetKeyDown(KeyCode.Backslash))
		{
			bool verbose = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
			DebugWidget.Instance.TargetObject(m_selected.Target, verbose);
		}
		if (Input.GetKeyDown(KeyCode.Home))
		{
			Debug.Log(m_selected.Label + "(" + m_selectionIndex + ")");
			int num = m_items.FindIndex(g => g.Label == "systems");
			GameObjectItem gameObjectItem = m_items[num];
			gameObjectItem.Expanded = true;
			int num2 = gameObjectItem.Children.FindIndex(g => g.Label == "menuScreenManager");
			m_selected = m_items[num2];
			m_selectionIndex = num + num2 + 1;
		}
		if (Core.Input.Up.OnPressed)
		{
			MoveSelectionUp();
			ResetHold();
		}
		if (Core.Input.Down.OnPressed)
		{
			MoveSelectionDown();
			ResetHold();
		}
		if (Core.Input.ActionButtonA.OnPressed)
		{
			m_selected.Target.SetActive(!m_selected.Target.activeSelf);
		}
		if (Core.Input.Right.OnPressed)
		{
			m_selected.Expanded = true;
		}
		if (Core.Input.Left.OnPressed)
		{
			m_selected.Expanded = false;
		}
		if (Core.Input.Cancel.OnPressed)
		{
			enabled = false;
		}
		if (!Core.Input.Up.Pressed && !Core.Input.Down.Pressed)
		{
			return;
		}
		m_holdSpeed += Time.deltaTime * 4f;
		m_holdAccumulation += m_holdSpeed * Time.deltaTime;
		while (m_holdAccumulation > 1f)
		{
			m_holdAccumulation -= 1f;
			if (Core.Input.Up.Pressed)
			{
				MoveSelectionUp();
			}
			if (Core.Input.Down.Pressed)
			{
				MoveSelectionDown();
			}
		}
	}

	public void OnItemGUI(GameObjectItem item, ref int index, int depth)
	{
		if (item.Target == null)
		{
			return;
		}
		int num = index - m_selectionIndex;
		if (num == 0)
		{
			m_selected = item;
		}
		int num2 = ShowAboveCount;
		int num3 = ShowBelowCount;
		if (m_selectionIndex < num2)
		{
			num3 += num2 - m_selectionIndex;
		}
		if (m_selectionIndex > m_maxIndex - num3)
		{
			num2 += num3 - (m_maxIndex - m_selectionIndex);
		}
		if (num > -num2 && num < num3)
		{
			GUI.color = !item.Target.activeInHierarchy ? Color.gray : Color.white;
			GUILayout.BeginHorizontal(m_selected != item ? Style : SelectedStyle);
			GUILayout.Space(depth * 16);
			GUILayout.Label(!item.HasChildren ? string.Empty : !item.Expanded ? "»" : "«", Style,
			GUILayout.Width(16f)
			);
			GUILayout.Label(item.Label, Style);
			GUILayout.EndHorizontal();
		}
		index++;
		if (!item.Expanded)
		{
			return;
		}
		foreach (GameObjectItem child in item.Children)
		{
			OnItemGUI(child, ref index, depth + 1);
		}
	}

	private float m_holdSpeed;

	private float m_holdAccumulation;

	public static GUIStyle SelectedStyle;

	public static GUIStyle Style;

	public static GUIStyle PressedStyle;

	public static GUIStyle DebugMenuStyle;

	public static GUIStyle StyleEnabled;

	public static GUIStyle StyleDisabled;

	public GUISkin Skin;

	public int ShowAboveCount = 10;

	public int ShowBelowCount = 10;

	private readonly List<GameObjectItem> m_items = new List<GameObjectItem>();

	private int m_selectionIndex;

	private int m_maxIndex;

	private GameObjectItem m_selected;

	public class GameObjectItem
	{
		public bool HasChildren
		{
			get
			{
				return Children.Count > 0;
			}
		}

		public GameObjectItem(GameObject go)
		{
			Target = go;
			Label = go.name;
			foreach (Transform item in go.transform)
			{
				Children.Add(new GameObjectItem(item.gameObject));
			}
			Children.Sort((a, b) => string.Compare(a.Target.name, b.Target.name, StringComparison.Ordinal));
		}

		public GameObject Target;

		public string Label;

		public List<GameObjectItem> Children = new List<GameObjectItem>();

		public bool Expanded;
	}
}

using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

public class HierarchyDebugMenu : MonoBehaviour
{
	public void Awake()
	{
		HierarchyDebugMenu.Style = this.Skin.FindStyle("debugMenuItem");
		HierarchyDebugMenu.SelectedStyle = this.Skin.FindStyle("selectedDebugMenuItem");
		HierarchyDebugMenu.PressedStyle = this.Skin.FindStyle("pressedDebugMenuItem");
		HierarchyDebugMenu.DebugMenuStyle = this.Skin.FindStyle("debugMenu");
		HierarchyDebugMenu.StyleEnabled = this.Skin.FindStyle("debugMenuItemEnabled");
		HierarchyDebugMenu.StyleDisabled = this.Skin.FindStyle("debugMenuItemDisabled");
	}

	public void OnEnable()
	{
		this.m_selectionIndex = 0;
		SuspensionManager.SuspendAll();
		this.m_items.Clear();
		foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
		{
			if (gameObject.hideFlags == HideFlags.None && gameObject.transform.parent == null && gameObject.activeInHierarchy == gameObject.activeSelf)
			{
				this.m_items.Add(new HierarchyDebugMenu.GameObjectItem(gameObject));
			}
		}
		this.m_items.Sort((HierarchyDebugMenu.GameObjectItem a, HierarchyDebugMenu.GameObjectItem b) => string.Compare(a.Target.name, b.Target.name, StringComparison.Ordinal));
	}

	public void OnDisable()
	{
		SuspensionManager.ResumeAll();
	}

	public void OnGUI()
	{
		int num = 0;
		int depth = 0;
		GUILayout.BeginArea(new Rect((float)(Screen.width / 2) - 200f, 0f, 400f, (float)Screen.height), GUI.skin.box);
		GUILayout.BeginVertical(GUI.skin.box, new GUILayoutOption[0]);
		GUILayout.FlexibleSpace();
		foreach (HierarchyDebugMenu.GameObjectItem item in this.m_items)
		{
			this.OnItemGUI(item, ref num, depth);
		}
		this.m_maxIndex = num - 1;
		GUILayout.FlexibleSpace();
		GUILayout.EndVertical();
		GUILayout.EndArea();
	}

	public void MoveSelectionDown()
	{
		this.m_selectionIndex = Mathf.Min(this.m_maxIndex, this.m_selectionIndex + 1);
	}

	public void MoveSelectionUp()
	{
		this.m_selectionIndex = Mathf.Max(0, this.m_selectionIndex - 1);
	}

	private void ResetHold()
	{
		this.m_holdSpeed = 2f;
		this.m_holdAccumulation = 0f;
	}

	public void FixedUpdate()
	{
		if (UnityEngine.Input.GetKeyDown(KeyCode.Backslash))
		{
			bool verbose = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
			DebugWidget.Instance.TargetObject(this.m_selected.Target, verbose);
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.Home))
		{
			Debug.Log(this.m_selected.Label + "(" + this.m_selectionIndex + ")");
			int num = this.m_items.FindIndex((HierarchyDebugMenu.GameObjectItem g) => g.Label == "systems");
			HierarchyDebugMenu.GameObjectItem gameObjectItem = this.m_items[num];
			gameObjectItem.Expanded = true;
			int num2 = gameObjectItem.Children.FindIndex((HierarchyDebugMenu.GameObjectItem g) => g.Label == "menuScreenManager");
			this.m_selected = this.m_items[num2];
			this.m_selectionIndex = num + num2 + 1;
		}
		if (Core.Input.Up.OnPressed)
		{
			this.MoveSelectionUp();
			this.ResetHold();
		}
		if (Core.Input.Down.OnPressed)
		{
			this.MoveSelectionDown();
			this.ResetHold();
		}
		if (Core.Input.ActionButtonA.OnPressed)
		{
			this.m_selected.Target.SetActive(!this.m_selected.Target.activeSelf);
		}
		if (Core.Input.Right.OnPressed)
		{
			this.m_selected.Expanded = true;
		}
		if (Core.Input.Left.OnPressed)
		{
			this.m_selected.Expanded = false;
		}
		if (Core.Input.Cancel.OnPressed)
		{
			base.enabled = false;
		}
		if (!Core.Input.Up.Pressed && !Core.Input.Down.Pressed)
		{
			return;
		}
		this.m_holdSpeed += Time.deltaTime * 4f;
		this.m_holdAccumulation += this.m_holdSpeed * Time.deltaTime;
		while (this.m_holdAccumulation > 1f)
		{
			this.m_holdAccumulation -= 1f;
			if (Core.Input.Up.Pressed)
			{
				this.MoveSelectionUp();
			}
			if (Core.Input.Down.Pressed)
			{
				this.MoveSelectionDown();
			}
		}
	}

	public void OnItemGUI(HierarchyDebugMenu.GameObjectItem item, ref int index, int depth)
	{
		if (item.Target == null)
		{
			return;
		}
		int num = index - this.m_selectionIndex;
		if (num == 0)
		{
			this.m_selected = item;
		}
		int num2 = this.ShowAboveCount;
		int num3 = this.ShowBelowCount;
		if (this.m_selectionIndex < num2)
		{
			num3 += num2 - this.m_selectionIndex;
		}
		if (this.m_selectionIndex > this.m_maxIndex - num3)
		{
			num2 += num3 - (this.m_maxIndex - this.m_selectionIndex);
		}
		if (num > -num2 && num < num3)
		{
			GUI.color = (!item.Target.activeInHierarchy) ? Color.gray : Color.white;
			GUILayout.BeginHorizontal((this.m_selected != item) ? HierarchyDebugMenu.Style : HierarchyDebugMenu.SelectedStyle, new GUILayoutOption[0]);
			GUILayout.Space((float)(depth * 16));
			GUILayout.Label((!item.HasChildren) ? string.Empty : ((!item.Expanded) ? "»" : "«"), HierarchyDebugMenu.Style, new GUILayoutOption[]
			{
				GUILayout.Width(16f)
			});
			GUILayout.Label(item.Label, HierarchyDebugMenu.Style, new GUILayoutOption[0]);
			GUILayout.EndHorizontal();
		}
		index++;
		if (!item.Expanded)
		{
			return;
		}
		foreach (HierarchyDebugMenu.GameObjectItem child in item.Children)
		{
			this.OnItemGUI(child, ref index, depth + 1);
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

	private readonly List<HierarchyDebugMenu.GameObjectItem> m_items = new List<HierarchyDebugMenu.GameObjectItem>();

	private int m_selectionIndex;

	private int m_maxIndex;

	private HierarchyDebugMenu.GameObjectItem m_selected;

	public class GameObjectItem
	{
		public bool HasChildren
		{
			get
			{
				return this.Children.Count > 0;
			}
		}

		public GameObjectItem(GameObject go)
		{
			this.Target = go;
			this.Label = go.name;
			foreach (Transform item in go.transform)
			{
				this.Children.Add(new HierarchyDebugMenu.GameObjectItem(item.gameObject));
			}
			this.Children.Sort((HierarchyDebugMenu.GameObjectItem a, HierarchyDebugMenu.GameObjectItem b) => string.Compare(a.Target.name, b.Target.name, StringComparison.Ordinal));
		}

		public GameObject Target;

		public string Label;

		public List<HierarchyDebugMenu.GameObjectItem> Children = new List<HierarchyDebugMenu.GameObjectItem>();

		public bool Expanded;
	}
}

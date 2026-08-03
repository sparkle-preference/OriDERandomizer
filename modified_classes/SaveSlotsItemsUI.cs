using System;
using System.Collections.Generic;
using UnityEngine;

public class SaveSlotsItemsUI : MonoBehaviour
{
	public float TargetScroll
	{
		get
		{
			return this.m_targetScroll;
		}
		set
		{
			this.m_targetScroll = value;
			this.m_targetScroll = Mathf.Clamp(this.m_targetScroll, 0f, (float)(this.Items.Count - 2));
		}
	}

	public void Awake()
	{
		for (int i = 0; i < 50; i++)
		{
			this.Items.Add(null);
		}
	}

	public void OnEnable()
	{
		this.Refresh();
	}

	public void Refresh()
	{
		if (this.Items.Count == 0)
		{
			return;
		}
		for (int i = 0; i < 50; i++)
		{
			this.RefreshItem(i);
		}
	}

	public void RefreshItem(int index)
	{
		SaveSlotUI saveSlotUI = SaveSlotsManager.Instance.SaveSlotCompleted(index) ? this.SaveSlotCompletedUI : this.SaveSlotUI;
		if (this.Items[index] && this.Items[index].name != saveSlotUI.name)
		{
			UnityEngine.Object.Destroy(this.Items[index].gameObject);
			this.Items[index] = null;
		}
		if (this.Items[index] == null)
		{
			SaveSlotUI saveSlotUI2 = UnityEngine.Object.Instantiate<SaveSlotUI>(saveSlotUI);
			saveSlotUI2.name = saveSlotUI.name;
			saveSlotUI2.transform.parent = base.transform;
			saveSlotUI2.transform.localScale = this.SaveSlotUI.transform.localScale;
			saveSlotUI2.transform.localPosition = Vector3.right * this.Spacing * (float)index;
			saveSlotUI2.SaveSlotIndex = index;
			this.Items[index] = saveSlotUI2;
			TransparencyAnimator.Register(saveSlotUI2.transform);
		}
		this.Items[index].Apply();
	}

	public void UpdateScroll()
	{
		this.m_scroll = Mathf.Lerp(this.m_scroll, this.m_targetScroll, 0.3f);
		this.Scroll.localPosition = Vector3.left * this.m_scroll * this.Spacing;
	}

	public void SetScrollFromIndex(int index)
	{
		this.TargetScroll = (float)(index - 1);
	}

	public SaveSlotUI SaveSlotUI;

	public SaveSlotUI SaveSlotCompletedUI;

	public Transform Scroll;

	public float Spacing;

	public List<SaveSlotUI> Items = new List<SaveSlotUI>();

	private float m_scroll;

	private float m_targetScroll;
}

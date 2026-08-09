using System.Collections.Generic;
using UnityEngine;

public class SaveSlotsItemsUI : MonoBehaviour {
    public float TargetScroll {
        get { return m_targetScroll; }
        set {
            m_targetScroll = value;
            m_targetScroll = Mathf.Clamp(m_targetScroll, 0f, Items.Count - 2);
        }
    }

    public void Awake() {
        for (int i = 0; i < 50; i++) {
            Items.Add(null);
        }
    }

    public void OnEnable() {
        Refresh();
    }

    public void Refresh() {
        if (Items.Count == 0) {
            return;
        }

        for (int i = 0; i < 50; i++) {
            RefreshItem(i);
        }
    }

    public void RefreshItem(int index) {
        SaveSlotUI saveSlotUI = SaveSlotsManager.Instance.SaveSlotCompleted(index) ? SaveSlotCompletedUI : SaveSlotUI;
        if (Items[index] && Items[index].name != saveSlotUI.name) {
            Destroy(Items[index].gameObject);
            Items[index] = null;
        }

        if (Items[index] == null) {
            SaveSlotUI saveSlotUI2 = Instantiate(saveSlotUI);
            saveSlotUI2.name = saveSlotUI.name;
            saveSlotUI2.transform.parent = transform;
            saveSlotUI2.transform.localScale = SaveSlotUI.transform.localScale;
            saveSlotUI2.transform.localPosition = Vector3.right * Spacing * index;
            saveSlotUI2.SaveSlotIndex = index;
            Items[index] = saveSlotUI2;
            TransparencyAnimator.Register(saveSlotUI2.transform);
        }

        Items[index].Apply();
    }

    public void UpdateScroll() {
        m_scroll = Mathf.Lerp(m_scroll, m_targetScroll, 0.3f);
        Scroll.localPosition = Vector3.left * m_scroll * Spacing;
    }

    public void SetScrollFromIndex(int index) {
        TargetScroll = index - 1;
    }

    public SaveSlotUI SaveSlotUI;

    public SaveSlotUI SaveSlotCompletedUI;

    public Transform Scroll;

    public float Spacing;

    public List<SaveSlotUI> Items = new List<SaveSlotUI>();

    private float m_scroll;

    private float m_targetScroll;
}

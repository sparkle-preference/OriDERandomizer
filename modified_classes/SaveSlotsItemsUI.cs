using System.Collections.Generic;
using UnityEngine;

public class SaveSlotsItemsUI : MonoBehaviour {
    public float TargetScroll {
        get => m_targetScroll;
        set {
            m_targetScroll = value;
            // three cards fit the screen; a short list has nowhere to scroll to
            var practice = PracticeSelect.Choosing || PracticeController.Active;
            var last = practice ? Mathf.Max(0, Items.Count - 3) : Items.Count - 2;
            m_targetScroll = Mathf.Clamp(m_targetScroll, 0f, last);
        }
    }

    // Fifty save files, the segments on offer, or the practice triple.
    public static int Count {
        get {
            if (PracticeSelect.Choosing) {
                return PracticeSelect.Count;
            }

            return PracticeController.Active ? PracticeController.LastSlot - PracticeController.FirstSlot + 1 : 50;
        }
    }

    // a card's slot on disk, which is only its position outside a practice session
    public static int Slot(int index) {
        return PracticeController.Active && !PracticeSelect.Choosing ? PracticeController.FirstSlot + index : index;
    }

    public void Awake() {
        for (var i = 0; i < 50; i++) {
            Items.Add(null);
        }
    }

    public void OnEnable() {
        Refresh();
    }

    public void Refresh() {
        var count = Count;
        while (Items.Count > count) {
            var last = Items[Items.Count - 1];
            if (last) {
                Destroy(last.gameObject);
            }

            Items.RemoveAt(Items.Count - 1);
        }

        while (Items.Count < count) {
            Items.Add(null);
        }

        for (var i = 0; i < count; i++) {
            RefreshItem(i);
        }
    }

    public void RefreshItem(int index) {
        var slot = Slot(index);
        var saveSlotUI = SaveSlotsManager.Instance.SaveSlotCompleted(slot) ? SaveSlotCompletedUI : SaveSlotUI;
        if (Items[index] && Items[index].name != saveSlotUI.name) {
            Destroy(Items[index].gameObject);
            Items[index] = null;
        }

        if (Items[index] == null) {
            var saveSlotUI2 = Instantiate(saveSlotUI);
            saveSlotUI2.name = saveSlotUI.name;
            saveSlotUI2.transform.parent = transform;
            saveSlotUI2.transform.localScale = SaveSlotUI.transform.localScale;
            saveSlotUI2.transform.localPosition = Vector3.right * Spacing * index;
            Items[index] = saveSlotUI2;
            TransparencyAnimator.Register(saveSlotUI2.transform);
        }

        Items[index].SaveSlotIndex = slot;
        Items[index].Apply();
        PracticeSelect.Decorate(Items[index], index);
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class SaveSlotsManager : MonoBehaviour {
    public static int CurrentSlotIndex {
        get { return SaveSlotsManager.Instance.m_currentSlotIndex; }
        set { SaveSlotsManager.Instance.m_currentSlotIndex = value; }
    }

    public static int BackupIndex {
        get { return SaveSlotsManager.Instance.m_backupIndex; }
        set { SaveSlotsManager.Instance.m_backupIndex = value; }
    }

    public static SaveSlotInfo CurrentSaveSlot {
        get { return SaveSlotsManager.FindOrCreateSaveSlot(SaveSlotsManager.CurrentSlotIndex); }
    }

    public bool AnySaveSlotsExist {
        get { return this.SaveSlots.Any((SaveSlotInfo slot) => slot != null); }
    }

    public static int SaveSlotCount {
        get { return SaveSlotsManager.Instance.SaveSlots.Count; }
    }

    public static bool SlotExists(int slotIndex) {
        return SaveSlotsManager.SlotByIndex(slotIndex) != null;
    }

    public static SaveSlotInfo FindOrCreateSaveSlot(int slotIndex) {
        if (!SaveSlotsManager.SlotExists(slotIndex)) {
            SaveSlotsManager.Instance.SaveSlots[slotIndex] = new SaveSlotInfo();
        }

        return SaveSlotsManager.SlotByIndex(slotIndex);
    }

    public void Awake() {
        SaveSlotsManager.Instance = this;
        for (int i = 0; i < 50; i++) {
            this.SaveSlots.Add(null);
        }
    }

    public static SaveSlotInfo SlotByIndex(int index) {
        if (index < SaveSlotsManager.Instance.SaveSlots.Count && index >= 0) {
            return SaveSlotsManager.Instance.SaveSlots[index];
        }

        return null;
    }

    public static void CopySlot(int from, int to) {
        SaveSlotsManager.Instance.SaveSlots[to] = SaveSlotsManager.Instance.SaveSlots[from];
        SaveSlotBackupsManager.DeleteAllBackups(to);
        string saveFilePath = GameController.Instance.SaveGameController.GetSaveFilePath(from);
        string saveFilePath2 = GameController.Instance.SaveGameController.GetSaveFilePath(to);
        if (File.Exists(saveFilePath2)) {
            File.Delete(saveFilePath2);
        }

        File.Copy(saveFilePath, saveFilePath2);
    }

    public static void DeleteSlot(int index) {
        SaveSlotBackupsManager.DeleteAllBackups(index);
        SaveSlotsManager.Instance.SaveSlots[index] = null;
        string saveFilePath = GameController.Instance.SaveGameController.GetSaveFilePath(index);
        File.Delete(saveFilePath);
    }

    public static void PrepareSlots() {
        SaveSlotsManager.Instance.SaveSlots.Clear();
        for (int i = 0; i < 50; i++) {
            if (GameController.Instance.SaveGameController.SaveExists(i)) {
                string saveFilePath = GameController.Instance.SaveGameController.GetSaveFilePath(i);
                using (BinaryReader binaryReader = new BinaryReader(File.Open(saveFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
                    SaveSlotInfo saveSlotInfo = new SaveSlotInfo();
                    if (saveSlotInfo.LoadFromReader(binaryReader)) {
                        if (GameController.Instance.IsTrial && !saveSlotInfo.IsTrialSave) {
                            SaveSlotsManager.Instance.SaveSlots.Add(null);
                        } else {
                            SaveSlotsManager.Instance.SaveSlots.Add(saveSlotInfo);
                        }
                    } else {
                        SaveSlotsManager.Instance.SaveSlots.Add(null);
                    }
                }
            } else {
                SaveSlotsManager.Instance.SaveSlots.Add(null);
            }
        }
    }

    public bool SaveSlotCompleted(int i) {
        SaveSlotInfo saveSlotInfo = this.SaveSlots[i];
        return saveSlotInfo != null && saveSlotInfo.Completed;
    }

    public static SaveSlotsManager Instance;

    private int m_currentSlotIndex;

    private int m_backupIndex = -1;

    public List<SaveSlotInfo> SaveSlots = new List<SaveSlotInfo>();
}

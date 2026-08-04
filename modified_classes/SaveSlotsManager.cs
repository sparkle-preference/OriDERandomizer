using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class SaveSlotsManager : MonoBehaviour {
    public static int CurrentSlotIndex {
        get => Instance.currentSlotIndex;
        set => Instance.currentSlotIndex = value;
    }

    public static int BackupIndex {
        get => Instance.backupIndex;
        set => Instance.backupIndex = value;
    }

    public static SaveSlotInfo CurrentSaveSlot => FindOrCreateSaveSlot(CurrentSlotIndex);

    public bool AnySaveSlotsExist {
        get { return SaveSlots.Any(slot => slot != null); }
    }

    public static int SaveSlotCount => Instance.SaveSlots.Count;

    public static bool SlotExists(int slotIndex) {
        return SlotByIndex(slotIndex) != null;
    }

    public static SaveSlotInfo FindOrCreateSaveSlot(int slotIndex) {
        if (!SlotExists(slotIndex)) {
            Instance.SaveSlots[slotIndex] = new SaveSlotInfo();
        }

        return SlotByIndex(slotIndex);
    }

    public void Awake() {
        Instance = this;
        for (var i = 0; i < 50; i++) {
            SaveSlots.Add(null);
        }
    }

    public static SaveSlotInfo SlotByIndex(int index) {
        if (index < Instance.SaveSlots.Count && index >= 0) {
            return Instance.SaveSlots[index];
        }

        return null;
    }

    public static void CopySlot(int from, int to) {
        Instance.SaveSlots[to] = Instance.SaveSlots[from];
        SaveSlotBackupsManager.DeleteAllBackups(to);
        var saveFilePath = GameController.Instance.SaveGameController.GetSaveFilePath(from);
        var saveFilePath2 = GameController.Instance.SaveGameController.GetSaveFilePath(to);
        if (File.Exists(saveFilePath2)) {
            File.Delete(saveFilePath2);
        }

        File.Copy(saveFilePath, saveFilePath2);
    }

    public static void DeleteSlot(int index) {
        SaveSlotBackupsManager.DeleteAllBackups(index);
        Instance.SaveSlots[index] = null;
        var saveFilePath = GameController.Instance.SaveGameController.GetSaveFilePath(index);
        File.Delete(saveFilePath);
    }

    public static void PrepareSlots() {
        Instance.SaveSlots.Clear();
        for (var i = 0; i < 50; i++) {
            if (GameController.Instance.SaveGameController.SaveExists(i)) {
                var saveFilePath = GameController.Instance.SaveGameController.GetSaveFilePath(i);
                using (var binaryReader = new BinaryReader(File.Open(saveFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
                    var saveSlotInfo = new SaveSlotInfo();
                    if (saveSlotInfo.LoadFromReader(binaryReader)) {
                        if (GameController.Instance.IsTrial && !saveSlotInfo.IsTrialSave) {
                            Instance.SaveSlots.Add(null);
                        } else {
                            Instance.SaveSlots.Add(saveSlotInfo);
                        }
                    } else {
                        Instance.SaveSlots.Add(null);
                    }
                }
            } else {
                Instance.SaveSlots.Add(null);
            }
        }
    }

    public bool SaveSlotCompleted(int i) {
        var saveSlotInfo = SaveSlots[i];
        return saveSlotInfo != null && saveSlotInfo.Completed;
    }

    public static SaveSlotsManager Instance;

    private int currentSlotIndex;

    private int backupIndex = -1;

    public List<SaveSlotInfo> SaveSlots = new List<SaveSlotInfo>();
}

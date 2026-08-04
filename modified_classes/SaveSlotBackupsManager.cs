using System;
using System.Collections.Generic;
using System.IO;
using Game;
using UnityEngine;

public class SaveSlotBackupsManager : MonoBehaviour {
    public void Awake() {
        Instance = this;
        Events.Scheduler.OnGameReset.Add(OnGameReset);
        XboxOneSave.OnSaveGameCacheCleared += OnSaveGameCacheCleared;
        ClearCache();
    }

    public void OnDestroy() {
        Events.Scheduler.OnGameReset.Remove(OnGameReset);
        XboxOneSave.OnSaveGameCacheCleared -= OnSaveGameCacheCleared;
    }

    public void OnGameReset() {
        ClearCache();
    }

    public void OnSaveGameCacheCleared() {
        ClearCache();
    }

    public static void RequestReadBackups(int slotIndex, Action onFinishedReading) {
        Instance.currentReadingSlot = slotIndex;
        var saveSlotBackup = Instance.FindByIndex(slotIndex);
        if (saveSlotBackup.IsLoaded) {
            if (onFinishedReading != null) {
                onFinishedReading();
            }
        } else {
            Instance.onFinishedReaded = onFinishedReading;
        }
    }

    public static SaveSlotBackup SaveSlotBackupAtIndex(int index) {
        return Instance.saveSlotBackups[index];
    }

    public static void DeleteAllBackups(int slotIndex) {
        var saveSlotBackup = SaveSlotBackupAtIndex(slotIndex);
        for (var i = 0; i < 5; i++) {
            var path = Instance.BackupName(slotIndex, i);
            File.Delete(path);
        }

        for (var j = 0; j < saveSlotBackup.SaveSlotInfos.Length; j++) {
            saveSlotBackup.SaveSlotInfos[j] = null;
        }
    }

    public static void CreateCurrentBackup() {
        try {
            if (Time.realtimeSinceStartup >= Instance.lastSaveTime + 60f) {
                Instance.lastSaveTime = Time.realtimeSinceStartup;
                Instance.CreateBackup(SaveSlotsManager.CurrentSlotIndex);
            }
        } catch (Exception exception) {
            Debug.LogException(exception);
        }
    }

    public static void ResetBackupDelay() {
        if (Instance) {
            Instance.lastSaveTime = 0f;
        }
    }

    public void RestoreBackup(int slotIndex, int backupIndex) {
        var filename = BackupName(slotIndex, backupIndex);
        GameController.Instance.SaveGameController.LoadFromFile(filename);
        GameController.Instance.SaveGameController.RestoreCheckpoint();
    }

    private void CreateBackup(int slotIndex) {
        var saveGameController = GameController.Instance.SaveGameController;
        var saveSlotBackup = FindByIndex(slotIndex);
        var num = saveSlotBackup.IndexOfOldestSaveSlotInfo();
        var destFileName = BackupName(slotIndex, num);
        File.Copy(saveGameController.GetSaveFilePath(SaveSlotsManager.CurrentSlotIndex), destFileName, true);
        var saveSlot = new SaveSlotInfo(SaveSlotsManager.CurrentSaveSlot);
        saveSlotBackup.SaveSlotInfos[num] = new SaveSlotBackupInfo(num, saveSlot);
        if (saveSlotBackup.Count < 5) {
            saveSlotBackup.Count++;
        }

        SaveSlotsManager.CurrentSaveSlot.Order = saveSlotBackup.GetLargestOrderValue();
    }

    public void Update() {
        if (IsBusyLoading()) {
            return;
        }

        if (createBackupPending) {
            createBackupPending = false;
            XboxOneSave.WriteSaveGame(backupBytes, backupName);
            backupBytes = null;
        }

        if (IsBusyLoading()) {
            return;
        }

        if (buffersToDelete.Count > 0) {
            var array = buffersToDelete.Pop();
            XboxOneSave.DeleteSaveGame(array[0], array[1]);
        }

        if (IsBusyLoading() || currentReadingSlot == -1) {
            return;
        }

        var saveSlotBackup = FindByIndex(currentReadingSlot);
        if (!saveSlotBackup.IsLoaded) {
            LookForBackup(currentReadingSlot, saveSlotBackup.Count);
        }

        if (saveSlotBackup.IsLoaded) {
            if (onFinishedReaded != null) {
                onFinishedReaded();
                onFinishedReaded = null;
            }

            currentReadingSlot = -1;
        }
    }

    private void ClearCache() {
        saveSlotBackups.Clear();
        for (var i = 0; i < 50; i++) {
            saveSlotBackups.Add(new SaveSlotBackup(i));
        }
    }

    private SaveSlotBackup FindByIndex(int index) {
        return saveSlotBackups[index];
    }

    private string BackupName(int slot, int index) {
        return Path.Combine(OutputFolder.PlayerDataFolderPath, "saveFile" + slot + "_bkup" + index + ".sav");
    }

    private void LookForBackup(int slotIndex, int backupIndex) {
        var saveSlotBackup = FindByIndex(currentReadingSlot);
        var path = BackupName(slotIndex, backupIndex);
        if (File.Exists(path)) {
            using (var binaryReader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
                var saveSlotInfo = new SaveSlotInfo();
                if (saveSlotInfo.LoadFromReader(binaryReader)) {
                    saveSlotBackup.SaveSlotInfos[backupIndex] = new SaveSlotBackupInfo(backupIndex, saveSlotInfo);
                } else {
                    saveSlotBackup.SaveSlotInfos[backupIndex] = null;
                }
            }

            if (backupIndex + 1 == 5) {
                saveSlotBackup.IsLoaded = true;
            }

            if (saveSlotBackup.Count < 5) {
                saveSlotBackup.Count++;
            }
        } else {
            saveSlotBackup.IsLoaded = true;
        }
    }

    private bool IsBusyLoading() {
        return false;
    }

    public const float TIME_BETWEEN_SAVES = 60f;

    private static SaveSlotBackupsManager Instance;

    private byte[] backupBytes;

    private string backupName;

    private readonly Stack<int[]> buffersToDelete = new Stack<int[]>();

    private bool createBackupPending;

    private int currentReadingSlot = -1;

    private float lastSaveTime;

    private Action onFinishedReaded;

    private readonly List<SaveSlotBackup> saveSlotBackups = new List<SaveSlotBackup>();
}

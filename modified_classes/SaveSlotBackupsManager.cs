using System;
using System.Collections.Generic;
using System.IO;
using Game;
using UnityEngine;

public class SaveSlotBackupsManager : MonoBehaviour {
    public void Awake() {
        SaveSlotBackupsManager.m_instance = this;
        Events.Scheduler.OnGameReset.Add(new Action(this.OnGameReset));
        XboxOneSave.OnSaveGameCacheCleared += this.OnSaveGameCacheCleared;
        this.ClearCache();
    }

    public void OnDestroy() {
        Events.Scheduler.OnGameReset.Remove(new Action(this.OnGameReset));
        XboxOneSave.OnSaveGameCacheCleared -= this.OnSaveGameCacheCleared;
    }

    public void OnGameReset() {
        this.ClearCache();
    }

    public void OnSaveGameCacheCleared() {
        this.ClearCache();
    }

    public static void RequestReadBackups(int slotIndex, Action onFinishedReading) {
        SaveSlotBackupsManager.m_instance.m_currentReadingSlot = slotIndex;
        SaveSlotBackup saveSlotBackup = SaveSlotBackupsManager.m_instance.FindByIndex(slotIndex);
        if (saveSlotBackup.IsLoaded) {
            if (onFinishedReading != null) {
                onFinishedReading();
            }
        } else {
            SaveSlotBackupsManager.m_instance.m_onFinishedReaded = onFinishedReading;
        }
    }

    public static SaveSlotBackup SaveSlotBackupAtIndex(int index) {
        return SaveSlotBackupsManager.m_instance.m_saveSlotBackups[index];
    }

    public static void DeleteAllBackups(int slotIndex) {
        SaveSlotBackup saveSlotBackup = SaveSlotBackupsManager.SaveSlotBackupAtIndex(slotIndex);
        for (int i = 0; i < 5; i++) {
            string path = SaveSlotBackupsManager.m_instance.BackupName(slotIndex, i);
            File.Delete(path);
        }

        for (int j = 0; j < saveSlotBackup.SaveSlotInfos.Length; j++) {
            saveSlotBackup.SaveSlotInfos[j] = null;
        }
    }

    public static void CreateCurrentBackup() {
        try {
            if (Time.realtimeSinceStartup >= SaveSlotBackupsManager.m_instance.m_lastSaveTime + 60f) {
                SaveSlotBackupsManager.m_instance.m_lastSaveTime = Time.realtimeSinceStartup;
                SaveSlotBackupsManager.m_instance.CreateBackup(SaveSlotsManager.CurrentSlotIndex);
            }
        } catch (Exception exception) {
            Debug.LogException(exception);
        }
    }

    public static void ResetBackupDelay() {
        if (SaveSlotBackupsManager.m_instance) {
            SaveSlotBackupsManager.m_instance.m_lastSaveTime = 0f;
        }
    }

    public void RestoreBackup(int slotIndex, int backupIndex) {
        string filename = this.BackupName(slotIndex, backupIndex);
        GameController.Instance.SaveGameController.LoadFromFile(filename);
        GameController.Instance.SaveGameController.RestoreCheckpoint();
    }

    private void CreateBackup(int slotIndex) {
        SaveGameController saveGameController = GameController.Instance.SaveGameController;
        SaveSlotBackup saveSlotBackup = this.FindByIndex(slotIndex);
        int num = saveSlotBackup.IndexOfOldestSaveSlotInfo();
        string destFileName = this.BackupName(slotIndex, num);
        File.Copy(saveGameController.GetSaveFilePath(SaveSlotsManager.CurrentSlotIndex), destFileName, true);
        SaveSlotInfo saveSlot = new SaveSlotInfo(SaveSlotsManager.CurrentSaveSlot);
        saveSlotBackup.SaveSlotInfos[num] = new SaveSlotBackupInfo(num, saveSlot);
        if (saveSlotBackup.Count < 5) {
            saveSlotBackup.Count++;
        }

        SaveSlotsManager.CurrentSaveSlot.Order = saveSlotBackup.GetLargestOrderValue();
    }

    public void Update() {
        if (this.IsBusyLoading()) {
            return;
        }

        if (this.m_createBackupPending) {
            this.m_createBackupPending = false;
            XboxOneSave.WriteSaveGame(this.m_backupBytes, this.m_backupName);
            this.m_backupBytes = null;
        }

        if (this.IsBusyLoading()) {
            return;
        }

        if (this.m_buffersToDelete.Count > 0) {
            int[] array = this.m_buffersToDelete.Pop();
            XboxOneSave.DeleteSaveGame(array[0], array[1]);
        }

        if (this.IsBusyLoading() || this.m_currentReadingSlot == -1) {
            return;
        }

        SaveSlotBackup saveSlotBackup = this.FindByIndex(this.m_currentReadingSlot);
        if (!saveSlotBackup.IsLoaded) {
            this.LookForBackup(this.m_currentReadingSlot, saveSlotBackup.Count);
        }

        if (saveSlotBackup.IsLoaded) {
            if (this.m_onFinishedReaded != null) {
                this.m_onFinishedReaded();
                this.m_onFinishedReaded = null;
            }

            this.m_currentReadingSlot = -1;
        }
    }

    private void ClearCache() {
        this.m_saveSlotBackups.Clear();
        for (int i = 0; i < 50; i++) {
            this.m_saveSlotBackups.Add(new SaveSlotBackup(i));
        }
    }

    private SaveSlotBackup FindByIndex(int index) {
        return this.m_saveSlotBackups[index];
    }

    private string BackupName(int slot, int index) {
        return Path.Combine(OutputFolder.PlayerDataFolderPath, "saveFile" + slot + "_bkup" + index + ".sav");
    }

    private void LookForBackup(int slotIndex, int backupIndex) {
        SaveSlotBackup saveSlotBackup = this.FindByIndex(this.m_currentReadingSlot);
        string path = this.BackupName(slotIndex, backupIndex);
        if (File.Exists(path)) {
            using (BinaryReader binaryReader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
                SaveSlotInfo saveSlotInfo = new SaveSlotInfo();
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

    private static SaveSlotBackupsManager m_instance;

    private byte[] m_backupBytes;

    private string m_backupName;

    private readonly Stack<int[]> m_buffersToDelete = new Stack<int[]>();

    private bool m_createBackupPending;

    private int m_currentReadingSlot = -1;

    private float m_lastSaveTime;

    private Action m_onFinishedReaded;

    private readonly List<SaveSlotBackup> m_saveSlotBackups = new List<SaveSlotBackup>();
}

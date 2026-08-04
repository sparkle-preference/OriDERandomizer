using System;
using System.Collections.Generic;
using System.IO;
using Game;
using UnityEngine;

public class SaveSlotBackupsManager : MonoBehaviour
{
	public void Awake()
	{
		m_instance = this;
		Events.Scheduler.OnGameReset.Add(OnGameReset);
		XboxOneSave.OnSaveGameCacheCleared += OnSaveGameCacheCleared;
		ClearCache();
	}

	public void OnDestroy()
	{
		Events.Scheduler.OnGameReset.Remove(OnGameReset);
		XboxOneSave.OnSaveGameCacheCleared -= OnSaveGameCacheCleared;
	}

	public void OnGameReset()
	{
		ClearCache();
	}

	public void OnSaveGameCacheCleared()
	{
		ClearCache();
	}

	public static void RequestReadBackups(int slotIndex, Action onFinishedReading)
	{
		m_instance.m_currentReadingSlot = slotIndex;
		var saveSlotBackup = m_instance.FindByIndex(slotIndex);
		if (saveSlotBackup.IsLoaded)
		{
			if (onFinishedReading != null)
			{
				onFinishedReading();
			}
		}
		else
		{
			m_instance.m_onFinishedReaded = onFinishedReading;
		}
	}

	public static SaveSlotBackup SaveSlotBackupAtIndex(int index)
	{
		return m_instance.m_saveSlotBackups[index];
	}

	public static void DeleteAllBackups(int slotIndex)
	{
		var saveSlotBackup = SaveSlotBackupAtIndex(slotIndex);
		for (var i = 0; i < 5; i++)
		{
			var path = m_instance.BackupName(slotIndex, i);
			File.Delete(path);
		}
		for (var j = 0; j < saveSlotBackup.SaveSlotInfos.Length; j++)
		{
			saveSlotBackup.SaveSlotInfos[j] = null;
		}
	}

	public static void CreateCurrentBackup()
	{
		try
		{
			if (Time.realtimeSinceStartup >= m_instance.m_lastSaveTime + 60f)
			{
				m_instance.m_lastSaveTime = Time.realtimeSinceStartup;
				m_instance.CreateBackup(SaveSlotsManager.CurrentSlotIndex);
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	public static void ResetBackupDelay()
	{
		if (m_instance)
		{
			m_instance.m_lastSaveTime = 0f;
		}
	}

	public void RestoreBackup(int slotIndex, int backupIndex)
	{
		var filename = BackupName(slotIndex, backupIndex);
		GameController.Instance.SaveGameController.LoadFromFile(filename);
		GameController.Instance.SaveGameController.RestoreCheckpoint();
	}

	private void CreateBackup(int slotIndex)
	{
		var saveGameController = GameController.Instance.SaveGameController;
		var saveSlotBackup = FindByIndex(slotIndex);
		var num = saveSlotBackup.IndexOfOldestSaveSlotInfo();
		var destFileName = BackupName(slotIndex, num);
		File.Copy(saveGameController.GetSaveFilePath(SaveSlotsManager.CurrentSlotIndex), destFileName, true);
		var saveSlot = new SaveSlotInfo(SaveSlotsManager.CurrentSaveSlot);
		saveSlotBackup.SaveSlotInfos[num] = new SaveSlotBackupInfo(num, saveSlot);
		if (saveSlotBackup.Count < 5)
		{
			saveSlotBackup.Count++;
		}
		SaveSlotsManager.CurrentSaveSlot.Order = saveSlotBackup.GetLargestOrderValue();
	}

	public void Update()
	{
		if (IsBusyLoading())
		{
			return;
		}
		if (m_createBackupPending)
		{
			m_createBackupPending = false;
			XboxOneSave.WriteSaveGame(m_backupBytes, m_backupName);
			m_backupBytes = null;
		}
		if (IsBusyLoading())
		{
			return;
		}
		if (m_buffersToDelete.Count > 0)
		{
			var array = m_buffersToDelete.Pop();
			XboxOneSave.DeleteSaveGame(array[0], array[1]);
		}
		if (IsBusyLoading() || m_currentReadingSlot == -1)
		{
			return;
		}
		var saveSlotBackup = FindByIndex(m_currentReadingSlot);
		if (!saveSlotBackup.IsLoaded)
		{
			LookForBackup(m_currentReadingSlot, saveSlotBackup.Count);
		}
		if (saveSlotBackup.IsLoaded)
		{
			if (m_onFinishedReaded != null)
			{
				m_onFinishedReaded();
				m_onFinishedReaded = null;
			}
			m_currentReadingSlot = -1;
		}
	}

	private void ClearCache()
	{
		m_saveSlotBackups.Clear();
		for (var i = 0; i < 50; i++)
		{
			m_saveSlotBackups.Add(new SaveSlotBackup(i));
		}
	}

	private SaveSlotBackup FindByIndex(int index)
	{
		return m_saveSlotBackups[index];
	}

	private string BackupName(int slot, int index)
	{
		return Path.Combine(OutputFolder.PlayerDataFolderPath, "saveFile" + slot + "_bkup" + index + ".sav");
	}

	private void LookForBackup(int slotIndex, int backupIndex)
	{
		var saveSlotBackup = FindByIndex(m_currentReadingSlot);
		var path = BackupName(slotIndex, backupIndex);
		if (File.Exists(path))
		{
			using (var binaryReader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
			{
				var saveSlotInfo = new SaveSlotInfo();
				if (saveSlotInfo.LoadFromReader(binaryReader))
				{
					saveSlotBackup.SaveSlotInfos[backupIndex] = new SaveSlotBackupInfo(backupIndex, saveSlotInfo);
				}
				else
				{
					saveSlotBackup.SaveSlotInfos[backupIndex] = null;
				}
			}
			if (backupIndex + 1 == 5)
			{
				saveSlotBackup.IsLoaded = true;
			}
			if (saveSlotBackup.Count < 5)
			{
				saveSlotBackup.Count++;
			}
		}
		else
		{
			saveSlotBackup.IsLoaded = true;
		}
	}

	private bool IsBusyLoading()
	{
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

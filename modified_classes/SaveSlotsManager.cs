using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class SaveSlotsManager : MonoBehaviour
{
	public static int CurrentSlotIndex
	{
		get
		{
			return Instance.m_currentSlotIndex;
		}
		set
		{
			Instance.m_currentSlotIndex = value;
		}
	}

	public static int BackupIndex
	{
		get
		{
			return Instance.m_backupIndex;
		}
		set
		{
			Instance.m_backupIndex = value;
		}
	}

	public static SaveSlotInfo CurrentSaveSlot
	{
		get
		{
			return FindOrCreateSaveSlot(CurrentSlotIndex);
		}
	}

	public bool AnySaveSlotsExist
	{
		get
		{
			return SaveSlots.Any(slot => slot != null);
		}
	}

	public static int SaveSlotCount
	{
		get
		{
			return Instance.SaveSlots.Count;
		}
	}

	public static bool SlotExists(int slotIndex)
	{
		return SlotByIndex(slotIndex) != null;
	}

	public static SaveSlotInfo FindOrCreateSaveSlot(int slotIndex)
	{
		if (!SlotExists(slotIndex))
		{
			Instance.SaveSlots[slotIndex] = new SaveSlotInfo();
		}
		return SlotByIndex(slotIndex);
	}

	public void Awake()
	{
		Instance = this;
		for (int i = 0; i < 50; i++)
		{
			SaveSlots.Add(null);
		}
	}

	public static SaveSlotInfo SlotByIndex(int index)
	{
		if (index < Instance.SaveSlots.Count && index >= 0)
		{
			return Instance.SaveSlots[index];
		}
		return null;
	}

	public static void CopySlot(int from, int to)
	{
		Instance.SaveSlots[to] = Instance.SaveSlots[from];
		SaveSlotBackupsManager.DeleteAllBackups(to);
		string saveFilePath = GameController.Instance.SaveGameController.GetSaveFilePath(from);
		string saveFilePath2 = GameController.Instance.SaveGameController.GetSaveFilePath(to);
		if (File.Exists(saveFilePath2))
		{
			File.Delete(saveFilePath2);
		}
		File.Copy(saveFilePath, saveFilePath2);
	}

	public static void DeleteSlot(int index)
	{
		SaveSlotBackupsManager.DeleteAllBackups(index);
		Instance.SaveSlots[index] = null;
		string saveFilePath = GameController.Instance.SaveGameController.GetSaveFilePath(index);
		File.Delete(saveFilePath);
	}

	public static void PrepareSlots()
	{
		Instance.SaveSlots.Clear();
		for (int i = 0; i < 50; i++)
		{
			if (GameController.Instance.SaveGameController.SaveExists(i))
			{
				string saveFilePath = GameController.Instance.SaveGameController.GetSaveFilePath(i);
				using (BinaryReader binaryReader = new BinaryReader(File.Open(saveFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
				{
					SaveSlotInfo saveSlotInfo = new SaveSlotInfo();
					if (saveSlotInfo.LoadFromReader(binaryReader))
					{
						if (GameController.Instance.IsTrial && !saveSlotInfo.IsTrialSave)
						{
							Instance.SaveSlots.Add(null);
						}
						else
						{
							Instance.SaveSlots.Add(saveSlotInfo);
						}
					}
					else
					{
						Instance.SaveSlots.Add(null);
					}
				}
			}
			else
			{
				Instance.SaveSlots.Add(null);
			}
		}
	}

	public bool SaveSlotCompleted(int i)
	{
		SaveSlotInfo saveSlotInfo = SaveSlots[i];
		return saveSlotInfo != null && saveSlotInfo.Completed;
	}

	public static SaveSlotsManager Instance;

	private int m_currentSlotIndex;

	private int m_backupIndex = -1;

	public List<SaveSlotInfo> SaveSlots = new List<SaveSlotInfo>();
}

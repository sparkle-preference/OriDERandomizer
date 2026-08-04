using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class SaveGameData
{
	public void SaveToWriter(BinaryWriter writer)
	{
		CurrentSaveFileVersion = 1;
		writer.Write("SaveGameData");
		writer.Write(1);
		writer.Write(Scenes.Count);
		foreach (SaveScene saveScene in Scenes.Values)
		{
			writer.Write(saveScene.SceneGUID.ToByteArray());
			writer.Write(saveScene.SaveObjects.Count);
			foreach (SaveObject saveObject in saveScene.SaveObjects)
			{
				writer.Write(saveObject.Id.ToByteArray());
				saveObject.Data.WriteMemoryStreamToBinaryWriter(writer);
			}
		}
		((IDisposable)writer).Dispose();
	}

	public bool LoadFromReader(BinaryReader reader)
	{
		Scenes.Clear();
		PendingScenes.Clear();
		if (reader.ReadString() != "SaveGameData")
		{
			return false;
		}
		CurrentSaveFileVersion = reader.ReadInt32();
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			SaveScene saveScene = new SaveScene();
			saveScene.SceneGUID = new MoonGuid(reader.ReadBytes(16));
			Scenes.Add(saveScene.SceneGUID, saveScene);
			int num2 = reader.ReadInt32();
			for (int j = 0; j < num2; j++)
			{
				SaveObject item = new SaveObject(new MoonGuid(reader.ReadBytes(16)));
				item.Data.ReadMemoryStreamFromBinaryReader(reader);
				saveScene.SaveObjects.Add(item);
			}
		}
		return true;
	}

	public SaveScene Master
	{
		get
		{
			return InsertScene(MoonGuid.Empty);
		}
	}

	public SaveScene GetScene(MoonGuid sceneGuid)
	{
		SaveScene result;
		if (Scenes.TryGetValue(sceneGuid, out result))
		{
			return result;
		}
		return null;
	}

	public SaveScene InsertScene(MoonGuid sceneGuid)
	{
		SaveScene saveScene;
		if (Scenes.TryGetValue(sceneGuid, out saveScene))
		{
			return saveScene;
		}
		saveScene = new SaveScene
		{
			SceneGUID = sceneGuid
		};
		Scenes.Add(saveScene.SceneGUID, saveScene);
		return saveScene;
	}

	public SaveScene InsertPendingScene(MoonGuid sceneGUID)
	{
		SaveScene saveScene;
		if (PendingScenes.TryGetValue(sceneGUID, out saveScene))
		{
			return saveScene;
		}
		saveScene = new SaveScene
		{
			SceneGUID = sceneGUID
		};
		PendingScenes.Add(saveScene.SceneGUID, saveScene);
		return saveScene;
	}

	public bool SceneExists(MoonGuid sceneGUID)
	{
		return Scenes.ContainsKey(sceneGUID);
	}

	public void ApplyPendingScenes()
	{
		foreach (SaveScene saveScene in PendingScenes.Values)
		{
			if (SceneExists(saveScene.SceneGUID))
			{
				Scenes.Remove(saveScene.SceneGUID);
			}
			Scenes.Add(saveScene.SceneGUID, saveScene);
		}
		ClearPendingScenes();
	}

	public void ClearPendingScenes()
	{
		PendingScenes.Clear();
	}

	public void ClearAllData()
	{
		Scenes.Clear();
		PendingScenes.Clear();
	}

	public void LoadCustomData(ArrayList data)
	{
		SaveScene saveScene = new SaveScene();
		saveScene.SceneGUID = (MoonGuid)data[0];
		Scenes.Add(saveScene.SceneGUID, saveScene);
		for (int i = 1; i < data.Count; i++)
		{
			SaveObject saveObject = new SaveObject((MoonGuid)((object[])data[i])[0]);
			byte[] array = (byte[])((object[])data[i])[1];
			BinaryReader binaryReader = new BinaryReader(new MemoryStream(array));
			int num = array.Length;
			saveObject.Data.MemoryStream.SetLength(num);
			binaryReader.Read(saveObject.Data.MemoryStream.GetBuffer(), 0, num);
			saveScene.SaveObjects.Add(saveObject);
		}
	}

	
	public const int DATA_VERSION = 1;

	private const string FILE_FORMAT_STRING = "SaveGameData";

	public readonly Dictionary<MoonGuid, SaveScene> Scenes = new Dictionary<MoonGuid, SaveScene>();

	public readonly Dictionary<MoonGuid, SaveScene> PendingScenes = new Dictionary<MoonGuid, SaveScene>();

	public static int CurrentSaveFileVersion = -1;
}

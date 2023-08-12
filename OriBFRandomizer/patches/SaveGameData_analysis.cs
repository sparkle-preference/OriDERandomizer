/*

This class contains a modified load method which logs all saved objects when a
file is loaded. To be used for investigation purposes only (this code should
never be present in a release).

*/

using System.Collections;
using System.IO;

namespace OriBFRandomizer.patches
{
	public static class SaveGameDataExt
	{
		public static void LoadCustomData(this SaveGameData _this, ArrayList data)
		{
			SaveScene saveScene = new SaveScene();
			saveScene.SceneGUID = (MoonGuid)data[0];
			_this.Scenes.Add(saveScene.SceneGUID, saveScene);
			for (int i = 1; i < data.Count; i++)
			{
				SaveObject saveObject = new SaveObject((MoonGuid)((object[])data[i])[0]);
				byte[] array = (byte[])((object[])data[i])[1];
				BinaryReader binaryReader = new BinaryReader(new MemoryStream(array));
				int num = array.Length;
				saveObject.Data.MemoryStream.SetLength((long)num);
				binaryReader.Read(saveObject.Data.MemoryStream.GetBuffer(), 0, num);
				saveScene.SaveObjects.Add(saveObject);
			}
		}
	}
}

/*

This class contains a modified load method which logs all saved objects when a
file is loaded. To be used for investigation purposes only (this code should
never be present in a release).

*/

using System;
using System.Collections;
using System.IO;

public class SaveGameData {
    public bool LoadFromReader(BinaryReader reader) {
        this.Scenes.Clear();
        this.PendingScenes.Clear();
        if (reader.ReadString() != "SaveGameData") {
            return false;
        }

        SaveGameData.CurrentSaveFileVersion = reader.ReadInt32();
        var num = reader.ReadInt32();
        var logging = RandomizerSettings.Controls.BashDeadzone > 0.9f;
        var reading = RandomizerSettings.QOL.AbilityMenuOpacity > 0.9f;
        var DifferentDataMap = new Hashtable();
        if (reading) {
            var array = File.ReadAllLines("datamap.dat");
            for (var i = 0; i < array.Length; i += 2) {
                DifferentDataMap[array[i]] = array[i + 1];
            }
        }

        for (var j = 0; j < num; j++) {
            var saveScene = new SaveScene();
            saveScene.SceneGUID = new MoonGuid(reader.ReadBytes(16));
            if (logging) {
                Randomizer.log("SCENE");
                Randomizer.log(saveScene.SceneGUID.ToString());
            }

            this.Scenes.Add(saveScene.SceneGUID, saveScene);
            var num2 = reader.ReadInt32();
            for (var k = 0; k < num2; k++) {
                var saveObject = new SaveObject(new MoonGuid(reader.ReadBytes(16)));
                if (logging) {
                    Randomizer.log(saveObject.Id.ToString());
                }

                saveObject.Data.ReadMemoryStreamFromBinaryReader(reader);
                if (logging) {
                    var str = "";
                    for (var l = 0; l < saveObject.Data.MemoryStream.GetBuffer().Length; l++) {
                        str = str + saveObject.Data.MemoryStream.GetBuffer()[l] + " ";
                    }

                    Randomizer.log(str);
                }

                if (reading && DifferentDataMap.ContainsKey(saveObject.Id.ToString())) {
                    saveObject.Data = new Archive();
                    var array2 = ((string)DifferentDataMap[saveObject.Id.ToString()]).Split(
                        ' '
                    );
                    var bytes = new byte[array2.Length];
                    for (var m = 0; m < array2.Length; m++) {
                        bytes[m] = Convert.ToByte(array2[m]);
                    }

                    var binaryReader = new BinaryReader(new MemoryStream(bytes));
                    saveObject.Data.MemoryStream.SetLength(bytes.Length);
                    binaryReader.Read(saveObject.Data.MemoryStream.GetBuffer(), 0, bytes.Length);
                }

                saveScene.SaveObjects.Add(saveObject);
            }
        }

        return true;
    }
}

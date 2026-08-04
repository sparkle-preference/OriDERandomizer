using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class SaveSceneManager : MonoBehaviour {
    // Note: this type is marked as 'beforefieldinit'.
    static SaveSceneManager() {
    }

    public static SaveSceneManager FromTransform(Transform transform) {
        var sceneRoot = SceneRoot.FindFromTransform(transform);
        if (sceneRoot) {
            return sceneRoot.SaveSceneManager;
        }

        return null;
    }

    public void ReleaseNullReferences() {
        for (var i = 0; i < SaveData.Count; i++) {
            var saveId = SaveData[i];
            if (saveId.SaveObject == null) {
                saveId.SaveObject = null;
            }
        }
    }

    [ContextMenu("Print info")]
    public void PrintInfo() {
    }

    public void RegisterGameObject(GameObject go) {
        go.GetComponentsInChildren(SaveSerializeList);
        for (var i = 0; i < SaveSerializeList.Count; i++) {
            SaveSerializeList[i].RegisterToSaveSceneManager(this);
        }

        SaveSerializeList.Clear();
    }

    public void UnregisterGameObject(GameObject go) {
        go.GetComponentsInChildren(SaveSerializeList);
        for (var i = 0; i < SaveSerializeList.Count; i++) {
            SaveSerializableHashSet.Add(SaveSerializeList[i]);
        }

        SaveData.RemoveAll(a => SaveSerializableHashSet.Contains(a.Save));
        SaveSerializeList.Clear();
        SaveSerializableHashSet.Clear();
    }

    public ISerializable IdToSaveSerialize(MoonGuid id) {
        if (id == null) {
            return null;
        }

        for (var i = 0; i < SaveData.Count; i++) {
            var saveId = SaveData[i];
            if (saveId.Id == id) {
                return saveId.Save;
            }
        }

        return null;
    }

    public MoonGuid SaveSerializeToId(ISerializable saveSerialize) {
        if (saveSerialize == null) {
            return null;
        }

        for (var i = 0; i < SaveData.Count; i++) {
            var saveId = SaveData[i];
            if (saveId.Save == saveSerialize) {
                return saveId.Id;
            }
        }

        return MoonGuid.Empty;
    }

    public void AddSaveObject(ISerializable saveSerialize, MoonGuid guid) {
        var item = new SaveId {
            Id = guid,
            Save = saveSerialize,
        };
        SaveData.RemoveAll(a => a.Id == guid);
        SaveData.Add(item);
    }

    public void Save(SaveScene saveScene) {
        saveScene.SaveObjects.Clear();
        for (var i = 0; i < SaveData.Count; i++) {
            var saveId = SaveData[i];
            try {
                if (saveId.Save as Component != null) {
                    var item = new SaveObject(saveId.Id);
                    item.Data.WriteMode();
                    saveId.Save.Serialize(item.Data);
                    saveScene.SaveObjects.Add(item);
                }
            } catch (Exception ex) {
            }
        }
    }

    public void SaveWithoutClearing(SaveScene saveScene) {
        saveCache.Clear();
        for (var i = 0; i < saveScene.SaveObjects.Count; i++) {
            saveCache.Add(saveScene.SaveObjects[i].Id, saveScene.SaveObjects[i].Data);
        }

        for (var j = 0; j < SaveData.Count; j++) {
            var saveId = SaveData[j];
            try {
                if (saveId.Save as Component != null) {
                    Archive archive;
                    if (saveCache.TryGetValue(saveId.Id, out archive)) {
                        archive.WriteMode();
                        saveId.Save.Serialize(archive);
                    } else {
                        var item = new SaveObject(saveId.Id);
                        item.Data.WriteMode();
                        saveId.Save.Serialize(item.Data);
                        saveScene.SaveObjects.Add(item);
                    }
                }
            } catch (Exception ex) {
            }
        }

        saveCache.Clear();
    }

    public void Save(SaveScene saveScene, ISerializable serializable) {
        var moonGuid = SaveSerializeToId(serializable);
        var flag = false;
        for (var i = 0; i < saveScene.SaveObjects.Count; i++) {
            if (moonGuid == saveScene.SaveObjects[i].Id) {
                var data = saveScene.SaveObjects[i].Data;
                data.WriteMode();
                serializable.Serialize(data);
                flag = true;
            }
        }

        if (!flag) {
            var item = new SaveObject(moonGuid);
            saveScene.SaveObjects.Add(item);
            var data2 = item.Data;
            data2.WriteMode();
            serializable.Serialize(data2);
        }
    }

    public void Load(SaveScene saveScene, HashSet<SaveSerialize> objects) {
        for (var i = 0; i < saveScene.SaveObjects.Count; i++) {
            var saveObject = saveScene.SaveObjects[i];
            var serializable = IdToSaveSerialize(saveObject.Id);
            try {
                var saveSerialize = serializable as SaveSerialize;
                if (saveSerialize != null && objects.Contains(saveSerialize)) {
                    saveObject.Data.ReadMode();
                    serializable.Serialize(saveObject.Data);
                }
            } catch (Exception ex) {
            }
        }

        if (bootstrapHook != null) {
            try {
                bootstrapHook(sceneRoot);
            } catch (Exception ex) {
                Randomizer.Log("Bootstrap exception: " + ex);
            }
        }
    }

    public void Load(SaveScene saveScene) {
        for (var i = 0; i < saveScene.SaveObjects.Count; i++) {
            var saveObject = saveScene.SaveObjects[i];
            var serializable = IdToSaveSerialize(saveObject.Id);
            try {
                if (serializable as Component) {
                    saveObject.Data.ReadMode();
                    serializable.Serialize(saveObject.Data);
                }
            } catch (Exception ex) {
            }
        }

        if (bootstrapHook != null) {
            try {
                bootstrapHook(sceneRoot);
            } catch (Exception ex) {
                Randomizer.Log("Bootstrap exception: " + ex);
            }
        }
    }

    public void AddChildSaveSerializables() {
        SaveData.Clear();
        try {
            RegisterGameObject(gameObject);
        } catch (Exception ex) {
        }
    }

    public static void ClearSaveSlotForOneLife(SaveGameData data) {
        var item = default(SaveObject);
        if (SeinDeathsManager.Instance) {
            item = data.Master.SaveObjects.Find(a => a.Id == SeinDeathsManager.Instance.MoonGuid);
        }

        data.PendingScenes.Clear();
        data.Scenes.Clear();
        SaveScene master = data.Master;
        master.SaveObjects.Add(item);
    }

    public static SaveSceneManager Master;

    public List<SaveId> SaveData = new List<SaveId>();

    private static readonly List<SaveSerialize> SaveSerializeList = new List<SaveSerialize>();

    private static readonly HashSet<ISerializable> SaveSerializableHashSet = new HashSet<ISerializable>();

    private Dictionary<MoonGuid, Archive> saveCache = new Dictionary<MoonGuid, Archive>();

    [Serializable]
    public class SaveId {
        public ISerializable Save {
            get => (ISerializable)SaveObject;
            set => SaveObject = (Object)value;
        }

        public MoonGuid Id;

        public Object SaveObject;
    }

    public Action<SceneRoot> bootstrapHook;

    public SceneRoot sceneRoot;
}

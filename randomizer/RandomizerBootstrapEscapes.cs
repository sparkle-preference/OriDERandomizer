using System;
using System.Collections.Generic;
using System.IO;
using Core;
using Game;
using UnityEngine;
using Object = UnityEngine.Object;
using RandoExts;
using System.Diagnostics;

public class RandomizerBootstrapEscapes {

    public static void GenericResetScene(SceneRoot sceneRoot) {
        // This resets the scene to the savescene in SaveScenesToRestore.
        if (TriggerByString.IsInTriggerList("Reset" + sceneRoot.name)) {
            Randomizer.log("GenericResetScene.StringTrigger " + sceneRoot.name);
            if (SaveScenesToRestore.ContainsKey(sceneRoot.name)) {
                sceneRoot.SaveSceneManager.LoadWithMurderousIntent(SaveScenesToRestore[sceneRoot.name]);
            } else {
                Randomizer.log("GenericResetScene error - key not found - " + sceneRoot.name);
            }
            TriggerByString.Deregister("Reset" + sceneRoot.name);
        }
    }

    private static Transform AddStringTriggerSceneResetter(SceneRoot sceneRoot, MoonGuid[] moonGuids, string triggerString, SaveScene saveScene, TriggerByString.TriggerEvent eventType) {
        // This adds an object to the scene that resets the scene to the given state. It runs on scene start or
        // always depending on eventType. moonGuids must be of length 3.
        var resetObject = new GameObject("String Trigger Scene Resetter");
        resetObject.transform.SetParent(sceneRoot.transform);
        var sequence = resetObject.AddComponent<ActionSequence>();
        sequence.MoonGuid = moonGuids[0];
        var trigger = resetObject.AddComponent<TriggerByString>();
        trigger.MoonGuid = moonGuids[1];
        trigger.Data = new TriggerByString.StringTriggerData { String = triggerString, TriggerEvent = eventType };
        trigger.TriggerOnce = false;
        trigger.ActionToRun = sequence;

        var resetAction = resetObject.AddComponent<RandomizerResetSceneAction>();
        resetAction.saveScene = saveScene;
        resetAction.sceneRoot = sceneRoot;
        resetAction.MoonGuid = moonGuids[2];
        sequence.Actions.Add(resetAction);
        return resetObject.transform;
    }

    private static SaveScene StoreSaveScene(SceneRoot sceneRoot, MoonGuid[] guidsToRemove, Dictionary<MoonGuid, Archive> guidsToReplace, Dictionary<MoonGuid, Archive> guidsToAdd) {
        if (SaveScenesToRestore.ContainsKey(sceneRoot.MetaData.SceneName)) {
            return SaveScenesToRestore[sceneRoot.MetaData.SceneName];
        }
        var saveScene = new SaveScene();
        sceneRoot.SaveSceneManager.Save(saveScene);
        for (int i = 0; i < saveScene.SaveObjects.Count;) {
            var saveObject = saveScene.SaveObjects[i];
            if ((guidsToRemove != null) && Array.Exists(guidsToRemove, x => x == saveObject.Id)) {
                saveScene.SaveObjects.RemoveAt(i);
            } else if ((guidsToReplace != null) && guidsToReplace.ContainsKey(saveObject.Id)) {
                saveObject.Data = guidsToReplace[saveObject.Id];
                saveScene.SaveObjects[i] = saveObject;
                saveScene.SaveObjects[i].Data.ResetStream();
                i++;
            } else {
                i++;
            }
        }
        if (guidsToAdd != null) {
            foreach (var guid in guidsToAdd.Keys) {
                var saveObject = new SaveObject(guid) { Data = guidsToAdd[guid] };
                saveScene.SaveObjects.Add(saveObject);
            }
        }
        SaveScenesToRestore.Add(sceneRoot.MetaData.SceneName, saveScene);
        return saveScene;
    }

    public static Dictionary<string, SaveScene> SaveScenesToRestore = new Dictionary<string, SaveScene>();

    public static Dictionary<string, Action<SceneRoot>> BootstrapScenes = new Dictionary<string, Action<SceneRoot>> {
    };
}











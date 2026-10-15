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
    private static void BootstrapForlornEscapeBackground(SceneRoot sceneRoot) {
        Dictionary<MoonGuid, Archive> guidsToAdd = new Dictionary<MoonGuid, Archive> {
            // *fallingRockNest/cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(525560687, 1251748971, 1973789351, -415379698), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
            // floatingLogA/log - RigidbodySerialize
            {new MoonGuid(-1764381441, 1161236252, -288838999, -1552650361), new Archive(new byte[] { 160,  95, 170, 196, 209,  91, 162, 195, 192,  54, 150,  65,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 128,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
            // floatingLogSetup/log - RigidbodySerialize
            {new MoonGuid(-1339271757, 1166254031, -1858194015, 101166178), new Archive(new byte[] { 106,  30, 169, 196,  79, 198, 178, 195,  55, 109,  83,  65,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 128,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
        };
        StoreSaveScene(sceneRoot, null, null, guidsToAdd);
    }

    private static void BootstrapForlornEscapeWindB(SceneRoot sceneRoot) {
        Dictionary<MoonGuid, Archive> guidsToAdd = new Dictionary<MoonGuid, Archive> {
            // *setups/secondSection/*fallingRock/cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(-1945144949, 1333078676, 24517258, -1153775781), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
            // cameraOffsetZones/cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(482748523, 1100572703, -1947352948, 1257591027), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
        };
        StoreSaveScene(sceneRoot, null, null, guidsToAdd);
    }

    private static void BootstrapForlornEscapeWindC(SceneRoot sceneRoot) {
        Dictionary<MoonGuid, Archive> guidsToAdd = new Dictionary<MoonGuid, Archive> {
            // cameraOffsetZones/cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(-1830350123, 1338020223, -189795670, 2116797415), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
        };
        StoreSaveScene(sceneRoot, null, null, guidsToAdd);
    }

    private static void BootstrapForlornEscapeNest(SceneRoot sceneRoot) {
        MoonGuid[] guidsToRemove = new MoonGuid[] {
            // petrifiedPlant/plant - PetrifiedPlant
			new MoonGuid(1660790238, 1127452529, 300174735, -1356736769),
            // petrifiedPlant/plant - EntityDamageReceiver
			new MoonGuid(948742023, 1318543202, 1597983372, -222408251),
        };
        Dictionary<MoonGuid, Archive> guidsToAdd = new Dictionary<MoonGuid, Archive> {
            // *storyNew/setup/cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(-729979857, 1163985899, -1726632815, -847985444), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
            // cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(1761412128, 1309500939, -342666335, 1483772454), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
        };
        StoreSaveScene(sceneRoot, guidsToRemove, null, guidsToAdd);
        // Running the nest cutscene twice breaks it, so we limit it to 1 run.
        var nestTransform = sceneRoot.transform.FindChild("*storyNew/setup/action");
        var nestSequence = nestTransform.gameObject.GetComponent<ActionSequence>();
        var nestTriggerTransform = sceneRoot.transform.FindChild("*storyNew/setup/collider");
        var nestTrigger = nestTriggerTransform.gameObject.GetComponent<PlayerCollisionTrigger>();
        var fixObject = new GameObject("Cutscene fix");
        fixObject.transform.SetParent(nestTriggerTransform);
        fixObject.SetActive(false);
        var fixSequence = fixObject.AddComponent<ActionSequence>();
        // This needs to be serialised, since it is how we determine which sequence to run.
        var activateAction = nestSequence.gameObject.AddComponent<ActivateAction>();
        activateAction.MoonGuid = new MoonGuid(989277246, 1167153474, -356258154, 1564212547);
        activateAction.Save = true;
        activateAction.Target = fixObject;
        activateAction.Activate = true;
        (activateAction as SaveSerialize).RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);
        nestSequence.Actions.Insert(0, activateAction);
        var condition = nestSequence.gameObject.AddComponent<IsActiveCondition>();
        condition.ActivationType = IsActiveCondition.Mode.ActiveSelf;
        condition.Active = true;
        condition.Target = fixObject;
        var conditionalAction = nestSequence.gameObject.AddComponent<RunActionCondition>();
        conditionalAction.Action = fixSequence;
        conditionalAction.ElseAction = nestSequence;
        conditionalAction.Condition = condition;
        nestTrigger.ActionToRun = conditionalAction;
        var setPosition = fixObject.AddComponent<SetCharacterPosition>();
        setPosition.MoonGuid = new MoonGuid(-394352084, 1234374222, 1329944463, 1479187539);
        setPosition.transform.position = new Vector3(-537.8f, -270.8f, 0f);
        setPosition.Position = setPosition.transform;
        fixSequence.Actions.Add(setPosition);
    }

    private static void BootstrapForlornEscapeResurrection(SceneRoot sceneRoot) {
        // This is the *escape ResurrectionAfter*, not the pre-escape resurrection.
        MoonGuid[] guidsToRemove = new MoonGuid[] {
            // *startSequence - OnSceneStartRunAction
			new MoonGuid(-216819770, 1262214645, -943610713, 348842924), 
            // *startSequence/*startSequence - ActionSequence
			new MoonGuid(-1590895792, 1248701012, -1866951527, -423739732), 
            // *startSequence/*startSequence - ActionSequenceSerializer
			new MoonGuid(-624430614, 1252109364, -1688148827, 581974120),
        };
        Dictionary<MoonGuid, Archive> guidsToReplace = new Dictionary<MoonGuid, Archive> {
            // **resurrection/timelineSequenceBgRocks - BaseAnimatorSerializer
            {new MoonGuid(329854042, 1259928693, 2140653233, -899552695), new Archive(new byte[] {   1,   0,   0,   0,   0})}, 
            // **resurrection/timelineSequence - BaseAnimatorSerializer
            {new MoonGuid(1684858457, 1105669517, 1024360886, 617432205), new Archive(new byte[] {   1,   0,   0,   0,   0})}, 
            // **resurrection/timelineSequenceRocks - BaseAnimatorSerializer
            {new MoonGuid(1072751770, 1282177582, 643734436, 1765829750), new Archive(new byte[] {   1,   0,   0,   0,   0})},
        };
        Dictionary<MoonGuid, Archive> guidsToAdd = new Dictionary<MoonGuid, Archive> {
            // cameraOffsetZones/cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(737217253, 1309058827, -1456609887, 1132713278), new Archive(new byte[] {   0,   0, 128, 127,   0,   0,   0,   0,   0,   0, 128,  63,})}, 
            // cameraOffsetZones/cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(1781637578, 1090731245, 674786748, -1454928590), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
            // cameraOffsetZones/cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(2049257809, 1265911785, 982443918, 42932965), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
            // cameraOffsetZones/cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(854009221, 1197143094, -912873583, -1545290995), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,})},
        };
        var saveScene = StoreSaveScene(sceneRoot, guidsToRemove, guidsToReplace, guidsToAdd);

        // This adds an object to the scene that resets the scene to the given state. It runs on scene start
        // when the triggerString exists. This way can't be used for the other scenes in the forlorn escape, here
        // it is guaranteed to run and won't break on checkpoints, and we need to do things with the start 
        // sequence anyway.
        var resetObject = new GameObject("String Trigger Scene Resetter");
        resetObject.transform.SetParent(sceneRoot.transform);
        var resetSequence = resetObject.AddComponent<ActionSequence>();
        resetSequence.MoonGuid = new MoonGuid(-1225688723, 1174941369, 1603139994, -1588722625);
        var trigger = resetObject.AddComponent<TriggerByString>();
        trigger.MoonGuid = new MoonGuid(1427500555, 1265340544, 186185395, 1348012912);
        trigger.Data = new TriggerByString.StringTriggerData { String = "ResetforlornRuinsResurrectionAfter", TriggerEvent = TriggerByString.TriggerEvent.SceneEnabledAfterSerialize };
        trigger.TriggerOnce = false;
        trigger.ActionToRun = resetSequence;

        var resetAction = resetObject.AddComponent<RandomizerResetSceneAction>();
        resetAction.saveScene = saveScene;
        resetAction.sceneRoot = sceneRoot;
        resetAction.MoonGuid = new MoonGuid(-1502115480, 1297260134, 501936277, -1270054818);
        resetSequence.Actions.Add(resetAction);

        var startSequence = sceneRoot.transform.FindChild("*startSequence/*startSequence").GetComponent<ActionSequence>();
        foreach (ActionMethod action in startSequence.Actions) {
            resetSequence.Actions.Add(action);
        }
        // Set the has-started-forlorn-escape item.
        var setInventory = startSequence.gameObject.AddComponent<RandomizerSetInventoryAction>();
        setInventory.MoonGuid = new MoonGuid(-1379371349, 1183738520, -782178651, -830127316);
        setInventory.inventorySlot = 1106;
        setInventory.value = 1;
        setInventory.modifier = RandomizerSetInventoryAction.Modifier.Set;
        startSequence.Actions.Insert(0, setInventory);
    }

    private static void BootstrapForlornRuinsResurrection(SceneRoot sceneRoot) {
        // This allows for restarting and resetting the forlorn escape. This is the wind restored room 
        // where you trigger the forlorn escape, but not the actual escape scene itself.
        // Clone the original trigger to make our own trigger, with custom text.
        var triggerTransform = sceneRoot.transform.FindChild("*story/resurrectionSequence/trigger");
        var restartTrigger = RandomizerBootstrap.CloneObject(sceneRoot, triggerTransform, "restartEscapeTrigger").GetComponent<OriInterestTriggerB>();
        restartTrigger.RunOnce = false;
        restartTrigger.transform.position = triggerTransform.position;
        var message = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        string text = "Press [Listen] to restart the #Forlorn Escape#";
        message.SetMessage(text);
        restartTrigger.HintMessage = message;

        // Only enable the new trigger when the original trigger has been triggered.		
        var gumo = sceneRoot.transform.FindChild("*gumoSetup/group/timelineSequenceGumo/gumo");
        var gumoDeactivated = restartTrigger.gameObject.AddComponent<IsActiveCondition>();
        gumoDeactivated.ActivationType = IsActiveCondition.Mode.ActiveSelf;
        gumoDeactivated.Active = false;
        gumoDeactivated.Target = gumo.gameObject;
        var inputLockedCondition = restartTrigger.gameObject.AddComponent<RandomizerInputLockedCondition>();
        inputLockedCondition.InputLocked = false;
        var compoundCondition = restartTrigger.gameObject.AddComponent<CompoundCondition>();
        var conditionInformation = new CompoundCondition.ConditionInformation();
        conditionInformation.Conditions.Add(inputLockedCondition);
        conditionInformation.Conditions.Add(gumoDeactivated);
        compoundCondition.Tests.Add(conditionInformation);
        restartTrigger.Condition = compoundCondition;

        var restartObject = new GameObject("Restart escape sequence");
        restartObject.transform.SetParent(sceneRoot.transform);
        var restartSequence = restartObject.AddComponent<ActionSequence>();
        restartSequence.MoonGuid = new MoonGuid(-1530685721, 1301800936, 1600722056, 1488425281);

        var lockInput = restartObject.AddComponent<LockPlayerInputManualAction>();
        lockInput.MoonGuid = new MoonGuid(573176615, 1143569580, 1656362385, -1448444720);
        lockInput.ShouldLock = true;
        restartSequence.Actions.Add(lockInput);

        var fade = restartObject.AddComponent<FaderBFadeInAction>();
        fade.MoonGuid = new MoonGuid(1981662771, 1218319302, 1805828504, 1687828485);
        fade.Duration = 1.5f;
        restartSequence.Actions.Add(fade);

        // We only reset the escape if we have already visited there and softlock fixes are enabled.
        var softlockObject = new GameObject("softlock fix sequence");
        softlockObject.transform.SetParent(restartObject.transform);
        var softlockSequence = softlockObject.AddComponent<ActionSequence>();
        softlockSequence.MoonGuid = new MoonGuid(1059464764, 1339241816, -1297627485, -85828949);
        var softlockFixCompoundCondition = softlockObject.AddComponent<CompoundCondition>();
        var hasStartedEscapeCondition = softlockObject.AddComponent<RandomizerCheckInventoryCondition>();
        hasStartedEscapeCondition.comparison = RandomizerCheckInventoryCondition.Comparison.NotEqual;
        hasStartedEscapeCondition.inventorySlot = 1106;
        hasStartedEscapeCondition.value = 0;
        hasStartedEscapeCondition.defaultResult = false;
        var softlockFixCondition = softlockObject.AddComponent<RandomizerCheckInventoryCondition>();
        softlockFixCondition.comparison = RandomizerCheckInventoryCondition.Comparison.Equal;
        softlockFixCondition.inventorySlot = 1103;
        softlockFixCondition.value = 0;
        softlockFixCondition.defaultResult = false;
        var softlockConditionInformation = new CompoundCondition.ConditionInformation();
        softlockConditionInformation.Conditions.Add(hasStartedEscapeCondition);
        softlockConditionInformation.Conditions.Add(softlockFixCondition);
        softlockFixCompoundCondition.Tests.Add(softlockConditionInformation);
        var conditionalAction = softlockObject.AddComponent<RunActionCondition>();
        conditionalAction.MoonGuid = new MoonGuid(488830862, 1312406989, -1073490546, 556220863);
        conditionalAction.Action = softlockSequence;
        conditionalAction.Condition = softlockFixCompoundCondition;
        restartSequence.Actions.Add(conditionalAction);

        var stringTrigger1 = softlockObject.AddComponent<RegisterStringTriggerAction>();
        stringTrigger1.MoonGuid = new MoonGuid(-1904455594, 1177976455, -2113478514, -1953326739);
        stringTrigger1.String = "ResetforlornRuinsResurrectionAfter";
        softlockSequence.Actions.Add(stringTrigger1);
        var stringTrigger2 = softlockObject.AddComponent<RegisterStringTriggerAction>();
        stringTrigger2.MoonGuid = new MoonGuid(1915104449, 1152520924, 463699106, 1398175807);
        stringTrigger2.String = "ResetforlornRuinsWindShaftMockupB";
        softlockSequence.Actions.Add(stringTrigger2);
        var stringTrigger3 = softlockObject.AddComponent<RegisterStringTriggerAction>();
        stringTrigger3.MoonGuid = new MoonGuid(-1241939250, 1184481564, -2050080080, -973405963);
        stringTrigger3.String = "ResetforlornRuinsWindShaftMockupC";
        softlockSequence.Actions.Add(stringTrigger3);
        var stringTrigger4 = softlockObject.AddComponent<RegisterStringTriggerAction>();
        stringTrigger4.MoonGuid = new MoonGuid(-413645635, 1339058668, 1458927786, -1834853022);
        stringTrigger4.String = "ResetforlornRuinsBackgroundC";
        softlockSequence.Actions.Add(stringTrigger4);
        var stringTrigger5 = softlockObject.AddComponent<RegisterStringTriggerAction>();
        stringTrigger5.MoonGuid = new MoonGuid(-1978479667, 1075822654, -672702549, 1288126496);
        stringTrigger5.String = "ResetforlornRuinsNestC";
        softlockSequence.Actions.Add(stringTrigger5);

        // We unload the scenes so that we guarantee 1 load of the save so we can reset it then.
        // Ugh, yeah, this isn't that pretty.
        var unloadScenes = softlockObject.AddComponent<RandomizerUnloadScenesAction>();
        unloadScenes.scenes = new string[] {
            "forlornRuinsResurrectionAfter",
            "forlornRuinsWindShaftMockupB",
            "forlornRuinsWindShaftMockupC",
            "forlornRuinsNestC",
            "forlornRuinsBackgroundC"
        };
        unloadScenes.unloadInstantly = true;
        unloadScenes.keepInMemory = false;
        softlockSequence.Actions.Add(unloadScenes);

        var wait = restartObject.AddComponent<WaitAction>();
        wait.MoonGuid = new MoonGuid(-491212927, 1304793841, 413500315, -699609412);
        wait.Duration = 0.5f;
        restartSequence.Actions.Add(wait);

        var escapeMetaData = Core.Scenes.Manager.GetSceneInformation("forlornRuinsResurrectionAfter");
        var preloadScene = restartObject.AddComponent<RandomizerPreloadRuntimeSceneAction>();
        preloadScene.MoonGuid = new MoonGuid(1251696958, 1166159616, -544294482, 81195273);
        preloadScene.Scene = escapeMetaData;
        restartSequence.Actions.Add(preloadScene);

        var wait2 = restartObject.AddComponent<WaitAction>();
        wait2.MoonGuid = new MoonGuid(-1402262494, 1147531631, 1470327187, 316199837);
        wait2.Duration = 1.0f;
        restartSequence.Actions.Add(wait2);

        var unlockInput = restartObject.AddComponent<LockPlayerInputManualAction>();
        unlockInput.MoonGuid = new MoonGuid(1186194510, 1106610206, -1571669109, 2106137345);
        unlockInput.ShouldLock = false;
        restartSequence.Actions.Add(unlockInput);

        var fade2 = restartObject.AddComponent<FaderBFadeOutAction>();
        fade2.MoonGuid = new MoonGuid(592899420, 1265435556, 302671783, 1716035380);
        fade2.Duration = 0.5f;
        restartSequence.Actions.Add(fade2);

        var loadScene = restartObject.AddComponent<RandomizerLoadRuntimeSceneAction>();
        loadScene.MoonGuid = new MoonGuid(1312815252, 1233006520, -474857070, 1221267318);
        loadScene.SceneMetaData = escapeMetaData;
        loadScene.UseSceneInitialValues = false;
        loadScene.CreateCheckpoint = false;
        restartSequence.Actions.Add(loadScene);

        restartTrigger.ActivateAction = restartSequence;
    }

    private static void ForlornEntranceHideAndSeek(SceneRoot sceneRoot) {
        // This disables the kuro hide and seek part.
        var disabledObject = new GameObject("Hide and seek disable");
        disabledObject.transform.SetParent(sceneRoot.transform);
        var disabledSequence = disabledObject.AddComponent<ActionSequence>();
        disabledSequence.MoonGuid = new MoonGuid(457861956, 1124044628, 1908663220, -924221753);

        var originalSequence = sceneRoot.transform.FindChild("*setups/kurosEscape/actionSequence").GetComponent<ActionSequence>();
        var stringTrigger = sceneRoot.transform.FindChild("*setups/kurosEscape").GetComponent<TriggerByString>();
        stringTrigger.TriggerOnce = false;

        // Only swap to the soft lock fix if soft lock fixes are not disabled.
        var condition = stringTrigger.gameObject.AddComponent<RandomizerCheckInventoryCondition>();
        condition.comparison = RandomizerCheckInventoryCondition.Comparison.NotEqual;
        condition.inventorySlot = 1103;
        condition.value = 0;
        condition.defaultResult = false;

        var conditionalAction = stringTrigger.gameObject.AddComponent<RunActionCondition>();
        conditionalAction.Action = originalSequence;
        conditionalAction.ElseAction = disabledSequence;
        conditionalAction.Condition = condition;

        stringTrigger.ActionToRun = conditionalAction;

        var fadeOut = disabledObject.AddComponent<FaderBFadeOutAction>();
        fadeOut.MoonGuid = new MoonGuid(1287600269, 1294930625, 1299658901, -1707084805);
        fadeOut.Duration = 2;
        disabledSequence.Actions.Add(fadeOut);

        var showUI = disabledObject.AddComponent<ShowSeinUIAction>();
        showUI.MoonGuid = new MoonGuid(-656123457, 1209606796, 787420561, -2025561358);
        showUI.ShouldShow = true;
        disabledSequence.Actions.Add(showUI);

        var hideLetterboxes = disabledObject.AddComponent<LetterboxAction>();
        hideLetterboxes.MoonGuid = new MoonGuid(671426682, 1287774943, -1288756342, -157128065);
        hideLetterboxes.ShowLetterboxes = false;
        disabledSequence.Actions.Add(hideLetterboxes);

        var waitAction = disabledObject.AddComponent<WaitAction>();
        waitAction.MoonGuid = new MoonGuid(-848754786, 1119927210, -525211239, -1895775866);
        waitAction.Duration = 2f;
        disabledSequence.Actions.Add(waitAction);

        var inputLockAction = disabledObject.AddComponent<LockPlayerInputManualAction>();
        inputLockAction.MoonGuid = new MoonGuid(-1661490662, 1280570223, -1200375398, 1343821057);
        inputLockAction.ShouldLock = false;
        disabledSequence.Actions.Add(inputLockAction);
    }

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
        { "forlornRuinsResurrectionAfter", BootstrapForlornEscapeResurrection },
        { "forlornRuinsWindShaftMockupB", BootstrapForlornEscapeWindB },
        { "forlornRuinsWindShaftMockupC", BootstrapForlornEscapeWindC },
        { "forlornRuinsNestC", BootstrapForlornEscapeNest },
        { "forlornRuinsBackgroundC", BootstrapForlornEscapeBackground },
        { "forlornRuinsResurrection", BootstrapForlornRuinsResurrection },
        { "forlornRuinsKuroHideStreamlined", ForlornEntranceHideAndSeek },
    };
}











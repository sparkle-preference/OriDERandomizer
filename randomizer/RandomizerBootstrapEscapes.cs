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

    private static void BootstrapGinsoResurrection(SceneRoot sceneRoot) {
        // This saveScene is just what is required to reset the bulb etc.
        var saveScene = new SaveScene();
        if (SaveScenesToRestore.ContainsKey(sceneRoot.MetaData.SceneName)) {
            saveScene = SaveScenesToRestore[sceneRoot.MetaData.SceneName];
        } else {
            saveScene.SceneGUID = sceneRoot.MetaData.SceneMoonGuid;
            Dictionary<MoonGuid, Archive> guidsToAdd = new Dictionary<MoonGuid, Archive> {
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas - PhysicalSystemManager
                {new MoonGuid(1951272400, 1116136422, -1467713898, -1793092062), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 198, 229,   3,  68, 179, 144,  20,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 152, 106, 234, 190, 159, 151,  99,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   8, 174,   3,  68, 214, 222,  20,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 152, 106, 234, 190, 159, 151,  99,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  74, 118,   3,  68, 249,  44,  21,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 152, 106, 234, 190, 159, 151,  99,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 140,  62,   3,  68,  27, 123,  21,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  54, 105, 234, 190, 250, 151,  99,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 206,   6,   3,  68,  62, 201,  21,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 152, 106, 234, 190, 159, 151,  99,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  16, 207,   2,  68,  97,  23,  22,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 152, 106, 234, 190, 159, 151,  99,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 222, 126,   1,  68, 188, 146,  20,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 127,  99, 112,  63, 104,  15, 176, 190,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 166, 202,   1,  68, 213, 210,  20,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 127,  99, 112,  63, 104,  15, 176, 190,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 112,  22,   2,  68, 238,  18,  21,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  18, 100, 112,  63,  72,  12, 176, 190,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  58,  98,   2,  68,   8,  83,  21,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 187,  99, 112,  63,  35,  14, 176, 190,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   2, 174,   2,  68,  33, 147,  21,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 127,  99, 112,  63, 104,  15, 176, 190,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 204, 249,   2,  68,  58, 211,  21,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  18, 100, 112,  63,  72,  12, 176, 190,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 198, 247,   2,  68,  50, 213,  21,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 128,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  46, 240,   2,  68,  42, 218,  21,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 128,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 140, 236,   2,  68,  45, 234,  21,  68,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 153, 127, 171, 189, 209,  25, 127,  63})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/lianaConnector - AttachToRope
                {new MoonGuid(171396518, 1115341135, 1094506674, -113226127), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/lianaConnector - AttachToRope
                {new MoonGuid(-1390552396, 1210886848, 1409785021, -96276738), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/lock - AttachToRope
                {new MoonGuid(-622965360, 1136474872, -1407679567, -1472972590), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/lock - GameObjectActivator
                {new MoonGuid(262945740, 1088346344, -547476289, -1128762443), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/spiritLantern - SpiritLantern
                {new MoonGuid(664654869, 1094994493, -921432390, 67339341), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/spiritLantern - AttachToRope
                {new MoonGuid(-1401955912, 1311806998, -240411984, -1923306710), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/spiritLantern - GameObjectActivator
                {new MoonGuid(-2128870074, 1206359744, -181435209, 1308574224), new Archive(new byte[] {   0})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas - PhysicalSystemManager
                {new MoonGuid(-774216497, 1212876729, -480291957, 1907304239), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  78,  33,   4,  68,  38, 199,  23,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 191,  41, 227,  62, 120, 108, 101,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  38, 238,   3,  68,  13, 132,  23,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 191,  41, 227,  62, 120, 108, 101,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 254, 186,   3,  68, 245,  64,  23,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0,  26,  40, 227,  62, 224, 108, 101,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 216, 135,   3,  68, 220, 253,  22,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0,  20,  46, 227,  62, 102, 107, 101,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 176,  84,   3,  68, 195, 186,  22,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 191,  41, 227,  62, 120, 108, 101,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 138,  33,   3,  68, 170, 119,  22,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0,  20,  46, 227,  62, 102, 107, 101,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  98, 238,   2,  68, 146,  52,  22,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0,  26,  40, 227,  62, 224, 108, 101,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  58, 187,   2,  68, 121, 241,  21,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 191,  41, 227,  62, 120, 108, 101,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  14, 122,   1,  68, 146, 239,  23,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0, 172, 241, 103,  63,  16, 175, 216,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 154, 175,   1,  68, 150, 175,  23,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0, 172, 241, 103,  63,  16, 175, 216,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  36, 229,   1,  68, 155, 111,  23,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0,  24, 241, 103,  63, 138, 177, 216,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 174,  26,   2,  68, 159,  47,  23,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0, 173, 240, 103,  63,  83, 179, 216,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  58,  80,   2,  68, 163, 239,  22,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0, 172, 241, 103,  63,  16, 175, 216,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 196, 133,   2,  68, 167, 175,  22,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0, 173, 240, 103,  63,  83, 179, 216,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  78, 187,   2,  68, 172, 111,  22,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0,  24, 241, 103,  63, 138, 177, 216,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 218, 240,   2,  68, 176,  47,  22,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0, 172, 241, 103,  63,  16, 175, 216,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 158, 246,   2,  68, 209,  55,  22,  68,   0,   0, 128, 179,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 128,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 154, 229,   2,  68,  90,  34,  22,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 153, 127, 171, 189, 209,  25, 127,  63})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/lianaConnector - AttachToRope
                {new MoonGuid(178371832, 1273519208, -2124900424, 1709777798), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/lianaConnector - AttachToRope
                {new MoonGuid(1960890494, 1281138681, -1226535495, 1030485279), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/spiritLantern - SpiritLantern
                {new MoonGuid(1746023090, 1338378712, -715567221, -181790224), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/spiritLantern - AttachToRope
                {new MoonGuid(-2045352757, 1290326496, -2003925057, -1635746913), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas - PhysicalSystemManager
                {new MoonGuid(2096379141, 1331294147, 632160956, 1875162157), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  68,  26,   4,  68, 179, 225,  25,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 249,   6, 243,  62, 175,  82,  97,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 154, 231,   3,  68, 165, 148,  25,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 249,   6, 243,  62, 175,  82,  97,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 238, 180,   3,  68, 151,  71,  25,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 227,   2, 243,  62, 200,  83,  97,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  66, 130,   3,  68, 138, 250,  24,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 139,   1, 243,  62,  37,  84,  97,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 152,  79,   3,  68, 124, 173,  24,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 249,   6, 243,  62, 175,  82,  97,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 238,  28,   3,  68, 110,  96,  24,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 249,   6, 243,  62, 175,  82,  97,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  66, 234,   2,  68,  96,  19,  24,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 227,   2, 243,  62, 200,  83,  97,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 158, 247,   1,  68, 222, 176,  25,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0,  94,  38, 104,  63, 221, 204, 215,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 222,  50,   2,  68, 157, 106,  25,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0,  94,  38, 104,  63, 221, 204, 215,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  32, 110,   2,  68,  91,  36,  25,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0, 227,  38, 104,  63, 161, 202, 215,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  96, 169,   2,  68,  26, 222,  24,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0,  94,  38, 104,  63, 221, 204, 215,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 162, 228,   2,  68, 216, 151,  24,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0, 227,  38, 104,  63, 161, 202, 215,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 226,  31,   3,  68, 151,  81,  24,  68,  31, 133, 107,  62,   0,   0,   0,   0,   0,   0,   0,   0,  94,  38, 104,  63, 221, 204, 215,  62,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 166,  37,   3,  68, 184,  89,  24,  68,   0,   0, 128, 179,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 128,  63,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 162,  20,   3,  68,  65,  68,  24,  68, 205, 204,  76,  62,   0,   0,   0,   0,   0,   0,   0,   0, 153, 127, 171, 189, 209,  25, 127,  63})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/lianaConnector - AttachToRope
                {new MoonGuid(-535164233, 1247262691, 1605469076, -1177271759), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/lianaConnector - AttachToRope
                {new MoonGuid(1661904791, 1313395362, -1835874156, 1445791251), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/spiritLantern - SpiritLantern
                {new MoonGuid(630299582, 1218977751, 158455452, -346059175), new Archive(new byte[] {   1})},
                //  *heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle/lanternOnLianas/spiritLantern - AttachToRope
                {new MoonGuid(1319895327, 1137486828, 1312413582, -89925671), new Archive(new byte[] {   1})},
                // *heartResurrection/restoringHeartWaterRising/blockingWalls/ginsoTreeBlockingWallA - LegacyAnimatorSerializer
                {new MoonGuid(1474542370, 1162171178, 732416389, 1376850379), new Archive(new byte[] {   0,   0,   0,   0,   1,   0})},
                // *heartResurrection/restoringHeartWaterRising/blockingWalls/ginsoTreeBlockingWallA - LegacyAnimatorSerializer
                {new MoonGuid(-406554768, 1087058291, -1146303329, -1105526941), new Archive(new byte[] {   0,   0,   0,   0,   1,   0})},
                // *heartResurrection/restoringHeartWaterRising/timelineSequence - BaseAnimatorSerializer
                {new MoonGuid(138114904, 1269836703, 1728861083, -349906100), new Archive(new byte[] {   0,   0,   0,   0,   0})},
                // *heartResurrection/restoringHeartWaterRising/triggerWaterSequence - OriInterestTriggerB 
                {new MoonGuid(38773067, 1133205061, 1144176819, -122691416), new Archive(new byte[] {   0})},
                // *heartResurrection/restoringHeartWaterRising/triggerWaterSequence - GameObjectActivator
                {new MoonGuid(-1132914496, 1206589852, -1986239850, 250181432), new Archive(new byte[] {   1})},
                // *heartResurrection/restoringHeartWaterRising/waterSequenceStartAction - ActionSequenceSerializer
                {new MoonGuid(-931408080, 1104098193, 2013734038, -576849898), new Archive(new byte[] {   0,   0,   0,   0,   0})},
                // *heartResurrection/restoringHeartWaterRising/waterSequenceStartAction/24. Run *unhighlightSequence (ActionSequence) action - RunActionOnce
                {new MoonGuid(-2131928798, 1151053004, -1555066196, -306220580), new Archive(new byte[] {   0})},
                // *heartResurrection/restoringHeartWaterRising/waterSequenceStartAction/26. Deactivate blockingCreep - ActivateAction
                {new MoonGuid(-991063856, 1297628211, -673093959, 847091872), new Archive(new byte[] {   1})},
                // *heartResurrection/restoringHeartWaterRising/waterSequenceStartAction/21. Activate musicZoneHeartWaterRising - ActivateAction
                {new MoonGuid(-147259863, 1148491338, 1333347473, -233039763), new Archive(new byte[] {   0})},
                // *heartResurrection/restoringHeartWaterRising/waterSequenceStartAction/09. Activate scrollLockWaterRising - ActivateAction
                {new MoonGuid(-348740911, 1245652916, -1985961550, -2107032426), new Archive(new byte[] {   0})},
                // *heartResurrection/restoringHeartWaterRising/waterSequenceStartAction/08. Deactivate musicZoneHeartBefore - ActivateAction
                {new MoonGuid(2042125136, 1307010574, -1929355389, -330155444), new Archive(new byte[] {   1})},
                // *heartResurrection/restoringHeartWaterRising/waterSequenceStartAction/14. Deactivate purpleGoopFallSounds - ActivateAction
                {new MoonGuid(-289053893, 1103145181, -1240229187, -846974877), new Archive(new byte[] {   1})},
                // surfaceColliders/surfaceColliderAfterResurrection - GameObjectActivator
                {new MoonGuid(606430441, 1122888326, 446701469, 1313404359), new Archive(new byte[] {   0})},
            };

            if (guidsToAdd != null) {
                foreach (var guid in guidsToAdd.Keys) {
                    var saveObject = new SaveObject(guid) { Data = guidsToAdd[guid] };
                    saveScene.SaveObjects.Add(saveObject);
                }
            }
            SaveScenesToRestore.Add(sceneRoot.MetaData.SceneName, saveScene);
        }

        MoonGuid[] resetGuids = new MoonGuid[] {
            new MoonGuid(-818294350, 1152363901, -199920491, 1691819527),
            new MoonGuid(-2087069953, 1097959376, 1429727127, -994677856),
            new MoonGuid(1733253145, 1158250958, 1997332359, 796196337)
        };
        AddStringTriggerSceneResetter(sceneRoot, resetGuids, "Reset" + sceneRoot.name, saveScene, TriggerByString.TriggerEvent.Always);

        var startSequenceTransform = sceneRoot.transform.FindChild("*heartResurrection/restoringHeartWaterRising/waterSequenceStartAction");
        var startSequence = startSequenceTransform.gameObject.GetComponent<ActionSequence>();
        // If we have already finished the escape then don't start the water. But it does everything
        // else to unlock the path.
        var ginsoEventTransform = startSequenceTransform.FindChild("22. Set World Event Action");
        var ginsoEventAction = ginsoEventTransform.gameObject.GetComponent<SetWorldEventAction>();
        var finishedCondition = ginsoEventTransform.gameObject.AddComponent<RandomizerGinsoEscapeWaterCondition>();
        var startWaterAction = ginsoEventTransform.gameObject.AddComponent<RunActionCondition>();
        startWaterAction.MoonGuid = new MoonGuid(135334539, 1305189427, -1936572794, -537139954);
        startWaterAction.Condition = finishedCondition;
        startWaterAction.Action = ginsoEventAction;
        startSequence.Actions[21] = startWaterAction;

        // Reset the ginso escape areas when we start the escape, ensuring that enemies exist etc.
        var resetObject = new GameObject("reset escape object");
        resetObject.transform.SetParent(startSequenceTransform);
        var resetSequence = startSequence.gameObject.AddComponent<ActionSequence>();
        resetSequence.MoonGuid = new MoonGuid(386320845, 1105950180, 648461700, -1633166887);
        var stringTrigger1 = resetObject.AddComponent<RegisterStringTriggerAction>();
        stringTrigger1.MoonGuid = new MoonGuid(-1824500569, 1090089555, 2036275100, 2029612424);
        stringTrigger1.String = "ResetginsoTreeWaterRisingBtm";
        resetSequence.Actions.Add(stringTrigger1);
        var stringTrigger2 = resetObject.AddComponent<RegisterStringTriggerAction>();
        stringTrigger2.MoonGuid = new MoonGuid(1686457687, 1189318547, -683848525, -163496974);
        stringTrigger2.String = "ResetginsoTreeWaterRisingMid";
        resetSequence.Actions.Add(stringTrigger2);
        var stringTrigger3 = resetObject.AddComponent<RegisterStringTriggerAction>();
        stringTrigger3.MoonGuid = new MoonGuid(1741380821, 1110385922, 1731917966, -1892890989);
        stringTrigger3.String = "ResetginsoTreeWaterRisingEnd";
        resetSequence.Actions.Add(stringTrigger3);
        var softlockFixCondition = startSequence.gameObject.AddComponent<RandomizerCheckInventoryCondition>();
        softlockFixCondition.comparison = RandomizerCheckInventoryCondition.Comparison.Equal;
        softlockFixCondition.inventorySlot = 1103;
        softlockFixCondition.value = 0;
        softlockFixCondition.defaultResult = true;
        var resetAction = startSequence.gameObject.AddComponent<RunActionCondition>();
        resetAction.MoonGuid = new MoonGuid(-714635744, 1093956791, -1573768277, -717989336);
        resetAction.Action = resetSequence;
        resetAction.Condition = softlockFixCondition;
        startSequence.Actions.Insert(0, resetAction);

        // So the first middle lantern can break, this fixes it without knowing why. 
        // Fix it just before 18. Deactivate Lock.
        var prelanternTransform = sceneRoot.transform.FindChild("*heartResurrection/chainReactionSetup/spiritLanternPlaceholders/middle").GetChild(0);
        var lanternTransform = prelanternTransform.FindChild("spiritLantern");
        var lanternFix = startSequence.gameObject.AddComponent<RandomizerFixGinsoLanternAction>();
        lanternFix.joint = lanternTransform.gameObject.GetComponent<ConfigurableJoint>();
        startSequence.Actions.Insert(18, lanternFix);
    }

    private static void BootstrapGinsoRisingBackground(SceneRoot sceneRoot) {
        Dictionary<MoonGuid, Archive> guidsToReplace = new Dictionary<MoonGuid, Archive> {
            // *risingWaterGroup/*risingWater - LegacyAnimatorSerializer
            {new MoonGuid(-1328602964, 1148476222, 624003226, 2054223519), new Archive(new byte[] {   0,   0,   0,   0,   1,   0})},
        };
        var saveScene = StoreSaveScene(sceneRoot, null, guidsToReplace, null);
        MoonGuid[] resetGuids = new MoonGuid[] {
            new MoonGuid(-1054162101, 1325166101, 155213245, 717062621),
            new MoonGuid(-51872315, 1322113630, 725392261, 1527489992),
            new MoonGuid(-1285731145, 1194340948, -2000290652, 1955483806)
        };
        AddStringTriggerSceneResetter(sceneRoot, resetGuids, "Reset" + sceneRoot.name, saveScene, TriggerByString.TriggerEvent.Always);
    }

    private static void BootstrapGinsoRisingBtm(SceneRoot sceneRoot) {
        // Make it so the spider respawns.
        var spiderTransform = sceneRoot.transform.FindChild("enemies/shootingSpiderEnemySetup/shootingSpiderPlaceholder");
        var spiderPlaceholder = spiderTransform.gameObject.GetComponent<ShootingSpiderPlaceholder>();
        spiderPlaceholder.RespawnTime = 60;

        MoonGuid[] guidsToRemove = new MoonGuid[] {
            // orbs/largeExpOrbPlaceholder - CollectablePlaceholder
			new MoonGuid(-1915834124, 1116807532, 836193716, -2106681095),
        };
        Dictionary<MoonGuid, Archive> guidsToAdd = new Dictionary<MoonGuid, Archive> {
            // cameraOffsetZones/cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(1459091892, 1292279826, 1005940402, -696068962), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0})},
        };
        var saveScene = StoreSaveScene(sceneRoot, guidsToRemove, null, guidsToAdd);
        MoonGuid[] resetGuids = new MoonGuid[] {
            new MoonGuid(-1210823307, 1262238765, -1058819664, 1927245486),
            new MoonGuid(-1925271232, 1194808160, -615463024, -848405948),
            new MoonGuid(580063949, 1106290301, 839169942, 377624067)
        };
        AddStringTriggerSceneResetter(sceneRoot, resetGuids, "Reset" + sceneRoot.name, saveScene, TriggerByString.TriggerEvent.Always);
    }

    private static void BootstrapGinsoRisingMid(SceneRoot sceneRoot) {
        MoonGuid[] guidsToRemove = new MoonGuid[] {
            // orbs/mediumExpOrbPlaceholder - CollectablePlaceholder
			new MoonGuid(53964193, 1257981966, 415469751, -2115092716),
            // orbs/mediumExpOrbPlaceholder - CollectablePlaceholder
			new MoonGuid(-1004154350, 1288196703, 335284919, -865007178),
        };
        Dictionary<MoonGuid, Archive> guidsToReplace = new Dictionary<MoonGuid, Archive> {
            // landOnAndSpawnOrbs/deathEffect/sprite - LegacyAnimatorSerializer
            {new MoonGuid(449482198, 1184250308, -1979787623, -766371553), new Archive(new byte[] {   0,   0,   0,   0,   1,   0})},
            // landOnAndSpawnOrbs/deathEffect/sprite - LegacyAnimatorSerializer
            {new MoonGuid(1654915717, 1225815753, 197024933, 1992423282), new Archive(new byte[] {   0,   0,   0,   0,   1,   0})},
            // projectileSpawnerPinkPlants/projectileSpawnerPinkPlant/projectileSpawner - TimedTrigger
            {new MoonGuid(-1669149119, 1255537032, 1214619565, -400425093), new Archive(new byte[] {   0,   0,   0,  64})},
            // projectileSpawnerPinkPlants/projectileSpawnerPinkPlant/projectileSpawner - TimedTrigger
            {new MoonGuid(-378876207, 1136289747, 1061897122, -906226002), new Archive(new byte[] {   0,   0, 224,  63})},
            // projectileSpawnerPinkPlants/projectileSpawnerPinkPlant/projectileSpawner - TimedTrigger
            {new MoonGuid(-974676905, 1078801203, -1094686583, 2073817249), new Archive(new byte[] {   0,   0, 128,  63})},
        };
        Dictionary<MoonGuid, Archive> guidsToAdd = new Dictionary<MoonGuid, Archive>
        {
            // cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(385047157, 1326497808, -1952317540, -1114365018), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0})},
        };
        var saveScene = StoreSaveScene(sceneRoot, guidsToRemove, guidsToReplace, guidsToAdd);
        MoonGuid[] resetGuids = new MoonGuid[] {
            new MoonGuid(1605210463, 1235400369, 119180700, 113708956),
            new MoonGuid(1512559529, 1248451687, -1927737216, -381345263),
            new MoonGuid(-1976985576, 1254719064, -1555547477, -819825165)
        };
        AddStringTriggerSceneResetter(sceneRoot, resetGuids, "Reset" + sceneRoot.name, saveScene, TriggerByString.TriggerEvent.Always);
    }

    private static void BootstrapGinsoRisingEnd(SceneRoot sceneRoot) {
        MoonGuid[] guidsToRemove = new MoonGuid[] {
            // orbs/mediumExpOrbPlaceholder - CollectablePlaceholder
			new MoonGuid(-227538525, 1250107993, -189198714, 282099947),
        };
        Dictionary<MoonGuid, Archive> guidsToAdd = new Dictionary<MoonGuid, Archive> {
            // cameraOffsetAnimationZone - CameraOffsetAnimationZone
            {new MoonGuid(-1785719754, 1075387822, 679764873, -1964096696), new Archive(new byte[] {   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0})},
        };
        var saveScene = StoreSaveScene(sceneRoot, guidsToRemove, null, guidsToAdd);
        MoonGuid[] resetGuids = new MoonGuid[] {
            new MoonGuid(-1254367699, 1116691683, 2019866803, 1081176983),
            new MoonGuid(-1384264285, 1150785703, -1193707599, 1188420518),
            new MoonGuid(-242230410, 1247501918, -1954679131, -1201607239)
        };
        AddStringTriggerSceneResetter(sceneRoot, resetGuids, "Reset" + sceneRoot.name, saveScene, TriggerByString.TriggerEvent.Always);
    }

    public static void GenericResetScene(SceneRoot sceneRoot) {
        // This resets the scene to the savescene in SaveScenesToRestore.
        if (TriggerByString.IsInTriggerList("Reset" + sceneRoot.name)) {
            if (SaveScenesToRestore.ContainsKey(sceneRoot.name)) {
                sceneRoot.SaveSceneManager.LoadWithMurderousIntent(SaveScenesToRestore[sceneRoot.name]);
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
        { "ginsoTreeResurrection", BootstrapGinsoResurrection },
        { "ginsoTreeWaterRisingBackground", BootstrapGinsoRisingBackground },
        { "ginsoTreeWaterRisingBtm", BootstrapGinsoRisingBtm },
        { "ginsoTreeWaterRisingMid", BootstrapGinsoRisingMid },
        { "ginsoTreeWaterRisingEnd", BootstrapGinsoRisingEnd },
        { "forlornRuinsResurrectionAfter", BootstrapForlornEscapeResurrection },
        { "forlornRuinsWindShaftMockupB", BootstrapForlornEscapeWindB },
        { "forlornRuinsWindShaftMockupC", BootstrapForlornEscapeWindC },
        { "forlornRuinsNestC", BootstrapForlornEscapeNest },
        { "forlornRuinsBackgroundC", BootstrapForlornEscapeBackground },
        { "forlornRuinsResurrection", BootstrapForlornRuinsResurrection },
        { "forlornRuinsKuroHideStreamlined", ForlornEntranceHideAndSeek },
    };
}











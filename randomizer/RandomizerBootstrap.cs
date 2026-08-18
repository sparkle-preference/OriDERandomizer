using System;
using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Object = UnityEngine.Object;

public class RandomizerBootstrap {
    public static void Initialize() {
        Events.Scheduler.OnSceneRootPreEnabled.Add(BootstrapScenePreEnabled);
        Events.Scheduler.OnSceneRootEnabledAfterSerialize.Add(BootstrapSceneAfterSerialize);
    }

    public static void FixedUpdate() {
        for (var i = 0; i < s_bootstrappedScenesPreEnabled.Count;) {
            if (Scenes.Manager.GetSceneManagerScene(s_bootstrappedScenesPreEnabled[i]) != null) {
                i++;
            } else {
                s_bootstrappedScenesPreEnabled.RemoveAt(i);
            }
        }

        for (var i = 0; i < s_bootstrappedScenesAfterSerialize.Count;) {
            if (Scenes.Manager.GetSceneManagerScene(s_bootstrappedScenesAfterSerialize[i]) != null) {
                i++;
            } else {
                s_bootstrappedScenesAfterSerialize.RemoveAt(i);
            }
        }
    }

    private static void BootstrapScenePreEnabled(SceneRoot sceneRoot) {
        if (s_bootstrappedScenesPreEnabled.Contains(sceneRoot.name)) {
            return;
        }

        if (s_bootstrapPreEnabled.ContainsKey(sceneRoot.name)) {
            s_bootstrappedScenesPreEnabled.Add(sceneRoot.name);
            s_bootstrapPreEnabled[sceneRoot.name].Invoke(sceneRoot);
        }

        if (RandomizerEnhancedMode.TextBootstrapScenes.ContainsKey(sceneRoot.name) && (Randomizer.EnhancedMode || Randomizer.EnhancedSeinInSeed)) {
            RandomizerEnhancedMode.TextBootstrapScenes[sceneRoot.name].Invoke(sceneRoot);
        }

        if (RandomizerEnhancedMode.WaterBootstrapScenes.ContainsKey(sceneRoot.name)) {
            RandomizerEnhancedMode.BootstrapSceneWater(sceneRoot);
        }
    }

    private static void BootstrapSceneAfterSerialize(SceneRoot sceneRoot) {
        if (s_bootstrappedScenesAfterSerialize.Contains(sceneRoot.name)) {
            return;
        }

        if (s_bootstrapAfterSerialize.ContainsKey(sceneRoot.name)) {
            s_bootstrappedScenesAfterSerialize.Add(sceneRoot.name);
            s_bootstrapAfterSerialize[sceneRoot.name].Invoke(sceneRoot);
            // We also need to process these functions after serialisation not caused by
            // scene loading, e.g. after death. So connect those hooks.
            sceneRoot.SaveSceneManager.sceneRoot = sceneRoot;
            sceneRoot.SaveSceneManager.bootstrapHook = s_bootstrapAfterSerialize[sceneRoot.name];
        }
    }

    private static void TwiddleGuidAndSave(SceneRoot sceneRoot, GuidOwner owner) {
        // this is a horrendous hack but it works well enough to place new, serializable objects without maintaining a giant GUID store
        // MoonGuid is a v4 UUID, which is a 16-byte identifier with two special bit sequences:
        //		* in the 16-bit identifier spanning bytes 6-7, the four most significant bits must be 0100 (4), indicating a v4 UUID
        //		* in byte 8, the two most significant bits must be 10, a special sequence indicating UUID "variant 2"
        // we can ensure "unique" "UUIDs" by just abusing the four version bits, incrementing the "version" by 1 for each clone
        // this results in an invalid UUID but fortunately this literally only matters for differentiating saved object data
        var originalGuid = owner.MoonGuid;
        owner.MoonGuid = new MoonGuid(originalGuid.A, originalGuid.B + 268435456, originalGuid.C, originalGuid.D);

        if (owner is SaveSerialize) {
            (owner as SaveSerialize).RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);
        }
    }

    private static void SetGuidAndSave(SceneRoot sceneRoot, GuidOwner owner, MoonGuid guid) {
        owner.MoonGuid = guid;

        if (owner is SaveSerialize) {
            (owner as SaveSerialize).RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);
        }
    }

    private static Transform CloneObject(SceneRoot sceneRoot, Transform obj, string name = null, bool sibling = true) {
        // temporarily fiddle with the original object's active status to prevent the clone from instantly awaking if it shouldn't
        var originalActive = obj.gameObject.activeSelf;
        if (!obj.gameObject.activeInHierarchy) {
            obj.gameObject.SetActive(false);
        }

        var clone = Object.Instantiate(obj);
        if (name != null) {
            clone.gameObject.name = name;
        }

        if (sibling) {
            clone.parent = obj.parent;
        }

        // reinstate active status after the clone is part of the hierarchy
        obj.gameObject.SetActive(originalActive);
        clone.gameObject.SetActive(originalActive);

        foreach (GuidOwner owner in clone.gameObject.FindComponentsInChildren<GuidOwner>()) {
            TwiddleGuidAndSave(sceneRoot, owner);
        }

        return clone;
    }

    private static void BootstrapTitleScreen(SceneRoot sceneRoot) {
        RandomizerTitleScreen.Bootstrap(sceneRoot.transform.FindChild("ui"));

        SaveSlotsItemsUI itemsUI = sceneRoot.transform.FindChild("ui").GetComponent<TitleScreenManager>().SaveSlotsScreen.ItemsUI;
        foreach (SaveSlotUI saveSlotUI in new Object[2] { itemsUI.SaveSlotUI, itemsUI.SaveSlotCompletedUI }) {
            saveSlotUI.EasyTextMessageProvider = RandomizerText.DifficultyOverrides.Easy.NameOverride;
            saveSlotUI.NormalTextMessageProvider = RandomizerText.DifficultyOverrides.Normal.NameOverride;
            saveSlotUI.HardTextMessageProvider = RandomizerText.DifficultyOverrides.Hard.NameOverride;
            saveSlotUI.OneLifeTestMessageProvider = RandomizerText.DifficultyOverrides.OneLife.NameOverride;
        }

        var difficultyManager = itemsUI.SaveSlotUI.DifficultyScreen.GetComponent<CleverMenuItemSelectionManager>();
        difficultyManager.MenuItems[0].GetComponentInChildren<MessageBox>(true).SetMessageProvider(RandomizerText.DifficultyOverrides.Easy.NameOverride);
        difficultyManager.MenuItems[0].GetComponentInChildren<CleverMenuItemTooltip>(true).Tooltip = RandomizerText.DifficultyOverrides.Easy.DescriptionOverride;
        difficultyManager.MenuItems[1].GetComponentInChildren<MessageBox>(true).SetMessageProvider(RandomizerText.DifficultyOverrides.Normal.NameOverride);
        difficultyManager.MenuItems[1].GetComponentInChildren<CleverMenuItemTooltip>(true).Tooltip = RandomizerText.DifficultyOverrides.Normal.DescriptionOverride;
        difficultyManager.MenuItems[2].GetComponentInChildren<MessageBox>(true).SetMessageProvider(RandomizerText.DifficultyOverrides.Hard.NameOverride);
        difficultyManager.MenuItems[2].GetComponentInChildren<CleverMenuItemTooltip>(true).Tooltip = RandomizerText.DifficultyOverrides.Hard.DescriptionOverride;
        difficultyManager.MenuItems[3].GetComponentInChildren<MessageBox>(true).SetMessageProvider(RandomizerText.DifficultyOverrides.OneLife.NameOverride);
        difficultyManager.MenuItems[3].GetComponentInChildren<CleverMenuItemTooltip>(true).Tooltip = RandomizerText.DifficultyOverrides.OneLife.DescriptionOverride;
        difficultyManager.Index = 0;

        switch (RandomizerSettings.Game.DefaultDifficulty.Value) {
            case RandomizerSettings.Difficulty.Relaxing:
                difficultyManager.Index = 0;
                break;
            case RandomizerSettings.Difficulty.Challenging:
                difficultyManager.Index = 1;
                break;
            case RandomizerSettings.Difficulty.Punishing:
                difficultyManager.Index = 2;
                break;
            case RandomizerSettings.Difficulty.OneLife:
                difficultyManager.Index = 3;
                break;
            default:
                Randomizer.log($"unknown default difficulty {RandomizerSettings.Game.DefaultDifficulty.Value}");
                difficultyManager.Index = 0;
                break;
        }
    }

    private static void BootstrapBlackrootLanternRoom(SceneRoot sceneRoot) {
        var darkPlatforms = sceneRoot.transform.FindChild("*lightDarkPlatforms/darkPlatforms");
        var physicsManager = darkPlatforms.FindChild("physicsManager");

        // add difficulty condition to the platform container; use this to toggle all the platforms
        var condition = darkPlatforms.gameObject.AddComponent<DifficultyCondition>();
        condition.Easy = true;
        condition.Normal = false;
        condition.Hard = false;
        condition.OneLife = false;

        // make a clone of the physics system and set it as a sibling of the original
        var alternateManager = CloneObject(sceneRoot, physicsManager, "physicsManagerRelaxed");

        // activate the clone if the condition is met, else activate the original
        var activation = darkPlatforms.gameObject.AddComponent<ActivationBasedOnCondition>();
        activation.Condition = condition;
        activation.TargetTrue = alternateManager.gameObject;
        activation.TargetFalse = physicsManager.gameObject;

        // force the scene to re-validate since we've added things
        sceneRoot.OnValidate();

        // modify platforms in the clone
        foreach (Transform child in alternateManager) {
            if (child.position.x < 125f) {
                child.rotation *= Quaternion.Euler(0f, 0f, 60f);

                // "unrotate" the swaying movement of the platforms back to Y-axis alignment
                var componentInChildren = child.GetComponentInChildren<SinMovement>();
                var affectorY = componentInChildren.Affectors[0];

                var rotatedRange = MoonMath.Angle.Unrotate(new Vector2(0f, affectorY.Range / 2f), 60f);
                var rotatedRangeRandom = MoonMath.Angle.Unrotate(new Vector2(0f, affectorY.Range / 2f), 60f);

                affectorY.Range = rotatedRange.y;
                affectorY.RangeRandom = rotatedRangeRandom.y;

                var affectorX = new SinMovement.Affect();
                affectorX.Type = SinMovement.Affect.AffectType.X;
                affectorX.Offset = affectorY.Offset;
                affectorX.OffsetRandom = affectorY.OffsetRandom;
                affectorX.Period = affectorY.Period;
                affectorX.PeriodRandom = affectorY.PeriodRandom;
                affectorX.Range = rotatedRange.x;
                affectorX.RangeRandom = rotatedRangeRandom.x;
                componentInChildren.Affectors.Add(affectorX);
            } else if (child.position.x < 135f) {
                child.GetComponentInChildren<SinMovement>().Affectors[0].Range /= 2f;
            } else {
                child.position -= new Vector3(0f, 0.2f, 0f);
                child.GetComponentInChildren<SinMovement>().Affectors[0].Range /= 2f;
            }
        }
    }

    private static void BootstrapBlackrootBoulderArea(SceneRoot sceneRoot) {
        foreach (Transform child in sceneRoot.transform.FindChild("*gateSetup")) {
            if (child.name == "lever") {
                var leverSystem = child.GetComponent<ActionLeverSystem>();
                leverSystem.LeverLeftAction = null;
                leverSystem.LeverMiddleAction = null;
                leverSystem.LeverRightAction = null;
            } else if (child.name == "gate") {
                var doorAnimator = child.GetComponent<LegacyTranslateAnimator>();
                doorAnimator.TimeOffset = doorAnimator.TimeOfLastCurvePoint;
            }
        }
    }

    private static void BootstrapSpiritTree(SceneRoot sceneRoot) {
        // Unlike most other pickups, which are permanent placeholders that spawn an object with a DestroyOnRestoreCheckpoint component,
        // this one is *just* an object with a DestroyOnRestoreCheckpoint component. Disable that to prevent its untimely demise.
        sceneRoot.transform.FindChild("mediumExpOrb").GetComponent<DestroyOnRestoreCheckpoint>().enabled = false;
        // This checks if it is a grove tp spawn. TODO replace this with something tidier later.
        if (Randomizer.SpawnWith.Contains("-159,-114,force")) {
            var transform = sceneRoot.transform.FindChild("*spiritTreeStorySetup");
            var sequence = transform.FindChild("container/actionSequences/01. reachSpiritTreeActionSequence").GetComponent<ActionSequence>();
            sequence.Actions.Clear();
            var sequence2 = transform.FindChild("container/actionSequences/04. returnCameraToPlayerActionSequence").GetComponent<ActionSequence>();
            var action = sequence2.Actions[12];
            sequence.Actions.Add(action);
            var action2 = sceneRoot.transform.FindChild("*spiritTreeStorySetup/container/actionSequences/04. returnCameraToPlayerActionSequence/10. Deactivate *seinAbilityRestrictZones").GetComponent<ActionMethod>();
            sequence.Actions.Add(action2);
            sceneRoot.OnValidate();
        }
    }

    private static void BootstrapValleyEntry(SceneRoot sceneRoot) {
        // Apply open world patches
        if (Randomizer.Inventory.GetRandomizerItem(800) > 0) {
            // Disconnect the stomp post from the door; force it to be already stomped and deactivate the highlight
            var stompPost = sceneRoot.transform.FindChild("*simpleStompPostPuzzle/simpleStompPost").GetComponent<StompPost>();
            stompPost.AllTheWayInAction = null;
            stompPost.ForceActivate();
            stompPost.transform.FindChild("sunkenGladesStompTreeHighlight").gameObject.active = false;

            // Force the door open
            var doorAnimator = sceneRoot.transform.FindChild("*simpleStompPostPuzzle/sunkenGladesStompTree").GetComponent<LegacyTranslateAnimator>();
            doorAnimator.TimeOffset = doorAnimator.TimeOfLastCurvePoint;
        }
    }

    private static void BootstrapValleyThreeBirdArea(SceneRoot sceneRoot) {
        // Apply open world patches
        if (Randomizer.Inventory.GetRandomizerItem(800) > 0) {
            var leverSetup = sceneRoot.transform.FindChild("*leverSetup");

            // Just disconnect the lever from the door; leave the lever itself interact for Ori to play with if they want
            var leverSystem = leverSetup.GetComponentInChildren<ActionLeverSystem>();
            leverSystem.LeverLeftAction = null;
            leverSystem.LeverRightAction = null;

            // Force the door open
            var doorAnimator = leverSetup.FindChild("platformBranchSetup/sunkenGladesStompTree").GetComponent<LegacyTranslateAnimator>();
            doorAnimator.TimeOffset = doorAnimator.TimeOfLastCurvePoint;
        }
    }

    private static void BootstrapThornfeltSwampMain(SceneRoot sceneRoot) {
        // force the music to start up, dang it
        var musicSequence = sceneRoot.transform.FindChild("musicZones/musicActivation").GetComponent<ActionSequence>();
        var runAction = musicSequence.gameObject.AddComponent<OnSceneStartRunAction>();
        runAction.ActionToRun = musicSequence;
        runAction.TriggerOnce = true;
        SetGuidAndSave(sceneRoot, runAction, new MoonGuid(560691571, 1097907217, -1524861543, 276788056));

        // patch the post-Ginso cutscene to fix softlock when Sein's dialogue is auto-skipped
        var seinAnimationSequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/seinSpriteAction").GetComponent<ActionSequence>();
        var waitAction = seinAnimationSequence.Actions[1] as WaitAction;
        waitAction.Duration = 5.0f;
    }

    private static void BootstrapMoonGrottoBridge(SceneRoot sceneRoot) {
        // add an ActionSequenceSerializer to the bridge so that the sequence continues and activates the final colliders even after glitching it,
        // but delay that activation so the skip acts more like the vanilla skip.
        var bridgeSequenceGameObject = sceneRoot.transform.FindChild("*gumoBridgeSetup/group/action").gameObject;
        var serializer = bridgeSequenceGameObject.AddComponent<ActionSequenceSerializer>();
        var bridgeSequence = sceneRoot.transform.FindChild("*gumoBridgeSetup/group/action").GetComponent<ActionSequence>();
        var waitAction = bridgeSequence.gameObject.AddComponent<WaitAction>();
        waitAction.Duration = 10f;
        bridgeSequence.Actions.Insert(16, waitAction);
        SetGuidAndSave(sceneRoot, waitAction, new MoonGuid(705566895, 1206307123, -626862952, 223115723));
        serializer.OnValidate();
        SetGuidAndSave(sceneRoot, serializer, new MoonGuid(1360931587, 1176121670, -1051255642, 855352030));
    }

    private static void BootstrapMountHoruHub(SceneRoot sceneRoot) {
        // add randomized pickup actions for each end of room cutscene
        var lavaDrainParent = sceneRoot.transform.FindChild("*doorSetups/lavaDrainSetups");

        // door1LavaDrain - (L3) mountHoruBreakyPathTop
        var doorSequence = lavaDrainParent.FindChild("*door1LavaDrains/*door1LavaDrain").GetComponent<ActionSequence>();
        var pickupAction = RandomizerLocationManager.AddPickupAction(doorSequence.gameObject, "HoruL3");
        pickupAction.RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);
        doorSequence.Actions.Insert(3, pickupAction);
        ActionSequence.Rename(doorSequence.Actions);

        // door2LavaDrain - (R1) mountHoruStomperSystemsR
        doorSequence = lavaDrainParent.FindChild("*door2LavaDrains/*door2LavaDrain").GetComponent<ActionSequence>();
        pickupAction = RandomizerLocationManager.AddPickupAction(doorSequence.gameObject, "HoruR1");
        pickupAction.RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);
        doorSequence.Actions.Insert(3, pickupAction);
        ActionSequence.Rename(doorSequence.Actions);

        // door3LavaDrain - (R2) mountHoruProjectileCorridor
        doorSequence = lavaDrainParent.FindChild("*door3LavaDrains/*door3LavaDrain").GetComponent<ActionSequence>();
        pickupAction = RandomizerLocationManager.AddPickupAction(doorSequence.gameObject, "HoruR2");
        pickupAction.RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);
        doorSequence.Actions.Insert(3, pickupAction);
        ActionSequence.Rename(doorSequence.Actions);

        // door5LavaDrain - (R3) mountHoruMovingPlatform
        doorSequence = lavaDrainParent.FindChild("*door5LavaDrains/*door5LavaDrain").GetComponent<ActionSequence>();
        pickupAction = RandomizerLocationManager.AddPickupAction(doorSequence.gameObject, "HoruR3");
        pickupAction.RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);
        doorSequence.Actions.Insert(3, pickupAction);
        ActionSequence.Rename(doorSequence.Actions);

        // door7LavaDrain - (L2) mountHoruBigPushBlock
        doorSequence = lavaDrainParent.FindChild("*door7LavaDrains/*door7LavaDrain").GetComponent<ActionSequence>();
        pickupAction = RandomizerLocationManager.AddPickupAction(doorSequence.gameObject, "HoruL2");
        pickupAction.RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);
        doorSequence.Actions.Insert(3, pickupAction);
        ActionSequence.Rename(doorSequence.Actions);

        // door8LavaDrain - (L1) mountHoruBlockableLasers
        doorSequence = lavaDrainParent.FindChild("*door8LavaDrains/*door8LavaDrain").GetComponent<ActionSequence>();
        pickupAction = RandomizerLocationManager.AddPickupAction(doorSequence.gameObject, "HoruL1");
        pickupAction.RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);
        doorSequence.Actions.Insert(3, pickupAction);
        ActionSequence.Rename(doorSequence.Actions);

        // special cases for L4/R4
        var leftPickupAction = RandomizerLocationManager.AddPickupAction(lavaDrainParent.gameObject, "HoruL4", "giveLeftPickup");
        leftPickupAction.RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);

        var rightPickupAction = RandomizerLocationManager.AddPickupAction(lavaDrainParent.gameObject, "HoruR4", "giveRightPickup");
        rightPickupAction.RegisterToSaveSceneManager(sceneRoot.SaveSceneManager);

        // door4LavaDrain - L4/R4, whichever comes first
        doorSequence = lavaDrainParent.FindChild("*door4LavaDrains/*door4LavaDrain").GetComponent<ActionSequence>();
        var obj = new GameObject("pickupAction");
        obj.transform.parent = doorSequence.transform;

        var conditionPickupAction = obj.AddComponent<RunActionCondition>();
        SetGuidAndSave(sceneRoot, conditionPickupAction, new MoonGuid(-1261986975, 1336041250, 1663544246, -817715174));
        conditionPickupAction.Action = leftPickupAction;
        conditionPickupAction.ElseAction = rightPickupAction;
        conditionPickupAction.Condition = (doorSequence.Actions[2] as RunActionCondition).Condition;

        doorSequence.Actions.Insert(3, conditionPickupAction);
        ActionSequence.Rename(doorSequence.Actions);

        // door6LavaDrain - L4/R4, whichever comes second
        doorSequence = lavaDrainParent.FindChild("*door6LavaDrains/*door6LavaDrain").GetComponent<ActionSequence>();
        obj = new GameObject("pickupAction");
        obj.transform.parent = doorSequence.transform;

        conditionPickupAction = obj.AddComponent<RunActionCondition>();
        SetGuidAndSave(sceneRoot, conditionPickupAction, new MoonGuid(-300318401, 1327879929, 1536957364, -1500614911));
        conditionPickupAction.Action = rightPickupAction;
        conditionPickupAction.ElseAction = leftPickupAction;
        conditionPickupAction.Condition = (doorSequence.Actions[2] as RunActionCondition).Condition;

        doorSequence.Actions.Insert(3, conditionPickupAction);
        ActionSequence.Rename(doorSequence.Actions);

        // Apply lava patches unless closed dungeons flag is set
        if (Randomizer.Inventory.GetRandomizerItem(801) == 0) {
            var deactivateObjects = new List<string> {
                "lavaStreamA",
                "lavaStreamB",
                "lavaStreamC",
                "lavaStreamD",
                "lavaStreamE",
                "lavaStreamF",
                "LavaFGElements",
                "uberLavaBottom"
            };

            foreach (var deactivate in deactivateObjects) {
                sceneRoot.transform.FindChild(deactivate).gameObject.active = false;
            }
        }
    }

    private static void BootstrapSunkenGladesKeystoneDoor(SceneRoot sceneRoot) {
        // Apply open world patches
        if (Randomizer.Inventory.GetRandomizerItem(800) > 0) {
            // Open the keystone door and remove the "hey look it's a keystone door" cutscene
            var doorComponent = sceneRoot.transform.FindChild("doorWithTwoSlots/door").GetComponent<DoorWithSlots>();
            doorComponent.NumberOfOrbsUsed = 2;
            doorComponent.CurrentState = DoorWithSlots.State.Opened;

            var animator = doorComponent.transform.FindChild("doorPieces/doorLeft").GetComponent<LegacyTranslateAnimator>();
            animator.TimeOffset = animator.TimeOfLastCurvePoint;
            animator.SampleFirstFrameOnStart = true;
            animator = doorComponent.transform.FindChild("doorPieces/doorRight").GetComponent<LegacyTranslateAnimator>();
            animator.TimeOffset = animator.TimeOfLastCurvePoint;
            animator.SampleFirstFrameOnStart = true;

            var trigger = sceneRoot.transform.FindChild("*allEnemiesKilled/activated/*objectiveSetup/objectiveSetupTrigger").GetComponent<PlayerCollisionStayTrigger>();
            trigger.Active = false;
        }
    }

    private static void BootstrapSunkenGladesSpiritWell(SceneRoot sceneRoot) {
        // forcibly deactivate the collision trigger for the spirit well intro cutscene
        sceneRoot.transform.FindChild("*activatedBySpiritFlame/activated/*spiritWellHintSetup/objectiveSetupTrigger").GetComponent<PlayerCollisionTrigger>().Active = false;
    }

    private static void BootstrapMountHoruLaserPuzzle(SceneRoot sceneRoot) {
        var obj = new GameObject("deactivateSequence");
        obj.transform.parent = sceneRoot.transform.FindChild("laserPuzzle");

        var sequence = obj.AddComponent<ActionSequence>();
        SetGuidAndSave(sceneRoot, sequence, new MoonGuid(-217873041, 1228699831, -192933462, 1616173080));

        var trigger = obj.AddComponent<TriggerByString>();
        trigger.Data = new TriggerByString.StringTriggerData { String = "horuLaserPuzzleSolved", TriggerEvent = TriggerByString.TriggerEvent.Always };
        trigger.TriggerOnce = true;
        trigger.ActionToRun = sequence;
        SetGuidAndSave(sceneRoot, trigger, new MoonGuid(-1643625622, 1244944140, -1378018126, -449882576));

        foreach (Transform child in sceneRoot.transform.FindChild("laserPuzzle/enemyStoppers")) {
            if (child.name == "blockableLaser") {
                var newAction = new GameObject("action");
                newAction.transform.parent = obj.transform;

                var activate = newAction.AddComponent<ActivateAction>();
                activate.Activate = false;
                activate.Target = child.gameObject;
                sequence.Actions.Add(activate);

                if (child.position.x < 265f) {
                    SetGuidAndSave(sceneRoot, activate, new MoonGuid(296308939, 1211527480, -1445804128, 1888526783));
                } else {
                    SetGuidAndSave(sceneRoot, activate, new MoonGuid(83562839, 1305673046, 1379750071, 220123169));
                }
            }
        }

        ActionSequence.Rename(sequence.Actions);
    }

    private static void BootstrapSunkenGladesRunaway(SceneRoot sceneRoot) {
        // This checks if it is a non-default spawn. TODO replace this with something tidier later.
        if (!Randomizer.SpawnWith.Contains("WS")) {
            return;
        }

        var wsLocation = Randomizer.SpawnWith.IndexOf("WS");
        var offset = 2;
        if (Randomizer.SpawnWith.Contains("WS/")) {
            offset = 3;
        }

        var pieces = Randomizer.SpawnWith.Substring(wsLocation + offset).Split(',');
        int.TryParse(pieces[0], out var warpX);
        int.TryParse(pieces[1], out var warpY);
        var position = new Vector3(warpX, warpY, 0);
        // This only takes a position, and loads scenes at that position. Doesn't require the metadata.
        // Definitely not as nice as adding a load to the action sequence, but significantly easier.
        Scenes.Manager.AdditivelyLoadScenesAtPosition(position, true, false, true);

        var actionSequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        var original_list = new List<ActionMethod>(actionSequence.Actions);
        // Remove from "09. Wait 4 seconds" and onwards.
        actionSequence.Actions.RemoveRange(8, 9);
        // Hide letterboxes
        actionSequence.Actions.Add(original_list[11]);
        // Show UI
        actionSequence.Actions.Add(original_list[15]);
        // Unlock player input
        actionSequence.Actions.Add(original_list[10]);
        // Warp
        var setPosition = actionSequence.gameObject.AddComponent<SetCharacterPosition>();
        setPosition.transform.position = position;
        setPosition.Position = setPosition.transform;
        SetGuidAndSave(sceneRoot, setPosition, new MoonGuid(2033807637, 1102752838, 351348109, 1564353675));
        actionSequence.Actions.Add(setPosition);
        // create checkpoint -- should be immediately after warp.
        actionSequence.Actions.Add(original_list[14]);
        // Wait 4 seconds
        actionSequence.Actions.Add(original_list[8]);
        // wait 3.3 sceonds
        actionSequence.Actions.Add(original_list[12]);
        // play sound
        actionSequence.Actions.Add(original_list[13]);
        // Set user status action.
        actionSequence.Actions.Add(original_list[16]);
        sceneRoot.OnValidate();
    }

    private static void BootstrapWallJumpTreeHint(SceneRoot sceneRoot) {
        // This adds a return-to-start hint to the tree animation.
        var treeSequence = sceneRoot.transform.FindChild("*abilityPedestalWallJump/pedestal/actionSequence").GetComponent<ActionSequence>();
        var hint = treeSequence.gameObject.AddComponent<ShowHintAction>();
        var message = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        var text = "Stuck? You can use Warp (" + RandomizerRebinding.ReturnToStart.FirstBindName() + ") to go somewhere else!";
        message.SetMessage(text);
        hint.HintMessage = message;
        hint.Duration = 5f;

        // The hint only shows when we don't have a casual skill set able to get out.
        var condition = treeSequence.gameObject.AddComponent<RandomizerWallJumpHintCondition>();
        var action = treeSequence.gameObject.AddComponent<RunActionCondition>();
        action.Action = hint;
        action.Condition = condition;
        treeSequence.Actions.Add(action);
    }

    private static void BootstrapSeinRoomHint(SceneRoot sceneRoot) {
        if (RandomizerSettings.Customization.HintLevel.Value == RandomizerSettings.HintLevels.Disabled) {
            return;
        }

        // This adds an alt-r hint into the getting-sein animation.
        var getSeinSequence = sceneRoot.transform.FindChild("*setups/*story/findingOri/seinInterestZone/trigger/activateSequence").GetComponent<ActionSequence>();

        var obj = new GameObject("hintAction");
        obj.transform.parent = getSeinSequence.transform;

        var hint = obj.AddComponent<ShowHintAction>();
        var message = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        var text = "Tip: You can Warp (" + RandomizerRebinding.ReturnToStart.FirstBindName() + ") away without fighting these Fronkeys";
        message.SetMessage(text);
        hint.HintMessage = message;
        hint.Duration = 10f;
        getSeinSequence.Actions.Insert(17, hint);

        ActionSequence.Rename(getSeinSequence.Actions);
    }

    private static void BootstrapMoonGrottoMiniboss(SceneRoot sceneRoot) {
        // This function makes it so you don't soft-lock if you alt-r out
        // of the moon grotto miniboss room.
        // Check disable alt-r soft-lock fixes.
        if (Characters.Sein.Inventory.GetRandomizerItem(1103) != 0) {
            return;
        }

        var firstDoorTrigger = sceneRoot.transform.FindChild("*gumoAnimationSummonEnemy/enemyPuzzles/doorASetup/triggerCollider").GetComponent<PlayerCollisionTrigger>();
        var firstDoorAnimator = sceneRoot.transform.FindChild("*gumoAnimationSummonEnemy/enemyPuzzles/doorASetup/moonGrottoBlockingDoorB").GetComponent<LegacyTranslateAnimator>();
        var secondDoorAnimator = sceneRoot.transform.FindChild("*gumoAnimationSummonEnemy/enemyPuzzles/enemyPuzzle/doorSetup/sidewaysDoor/puzzleDoorLeft").GetComponent<LegacyTranslateAnimator>();
        var cameraZone = sceneRoot.transform.FindChild("*gumoAnimationSummonEnemy/cameraWideScreenZone").GetComponent<CameraWideScreenZone>();

        var firstDoorShut = !firstDoorAnimator.AtStart;
        var secondDoorOpen = !secondDoorAnimator.AtStart;
        if (secondDoorOpen) {
            // Note: I don't believe this is required as the other logic should suffice
            // by itself, but it is here just in case.
            // Open the door and disable the trigger and camera zone.
            firstDoorAnimator.Stopped = true;
            firstDoorAnimator.Reversed = false;
            firstDoorAnimator.CurrentTime = 0f;
            firstDoorAnimator.Sample(firstDoorAnimator.CurrentTime);
            firstDoorTrigger.gameObject.active = false;
            firstDoorTrigger.Active = false;
            cameraZone.gameObject.active = false;
            return;
        }

        var minibossRoom = Rect.MinMaxRect(558f, -423f, 628f, -390f);
        var isInRoom = minibossRoom.Contains(Characters.Sein.Position);
        if (firstDoorShut && !isInRoom) {
            // Open the door and enable the trigger and camera zone.
            firstDoorAnimator.Stopped = true;
            firstDoorAnimator.Reversed = false;
            firstDoorAnimator.CurrentTime = 0f;
            firstDoorAnimator.Sample(firstDoorAnimator.CurrentTime);
            firstDoorTrigger.gameObject.active = true;
            firstDoorTrigger.Active = true;
            cameraZone.gameObject.active = true;
        }
    }

    private static void BootstrapSeinRoomWall(SceneRoot sceneRoot) {
        // This removes the invisible blocking wall in the sein room so that
        // after an alt-r we don't soft-lock on the FronkeyFight pickup.
        // We also remove the fronkeys when the wall is not there since they
        // shouldn't be able to leave the room normally.
        // Check disable alt-r soft-lock fixes.
        if (Characters.Sein.Inventory.GetRandomizerItem(1103) != 0) {
            return;
        }

        var blockingWall = sceneRoot.transform.FindChild("blocker");
        var fronkeys = sceneRoot.transform.FindChild("*setups/*story/allEnemiesKilled/group/jumpingSootEnemyPlaceholders");
        var getSeinSequence = sceneRoot.transform.FindChild("*setups/*story/findingOri/seinInterestZone/trigger/activateSequence").GetComponent<ActionSequence>();
        var doorIsClosed = blockingWall.gameObject.active;
        var canSpawnFronkeys = getSeinSequence.Index > 0;
        var seinRoom = Rect.MinMaxRect(-172f, -275f, -81f, -250f);
        var isInRoom = seinRoom.Contains(Characters.Sein.Position);
        if (doorIsClosed && !isInRoom) {
            blockingWall.gameObject.active = false;
            fronkeys.gameObject.active = false;
        } else if (!doorIsClosed && canSpawnFronkeys) {
            fronkeys.gameObject.active = false;
        }
    }

    private static void BootstrapRhinoBeforeSein(SceneRoot sceneRoot) {
        // This changes the rhino before sein to respawn if ori is on screen, and faster, to 
        // make it more intuitive when the rhino is killed.
        var rhino = sceneRoot.transform.FindChild("*crashIntoRocksSetups/rammingEnemySetup/rammingEnemyPlaceholder").GetComponent<RammingEnemyPlaceholder>();
        rhino.RespawnOnScreen = true;
        rhino.RespawnTime = 10f;
    }

    private static void BootstrapGinsoLowerMiniboss(SceneRoot sceneRoot) {
        // This makes it so you can't soft-lock if you alt-r out of the lower ginso miniboss 
        // before killing the boss.
        // Check disable alt-r soft-lock fixes.
        if (Characters.Sein.Inventory.GetRandomizerItem(1103) != 0) {
            return;
        }

        var firstDoorAnimator = sceneRoot.transform.FindChild("ginsoTreeMultiMortar/doorASetup/ginsoTreeBlockingWallA").GetComponent<LegacyTranslateAnimator>();
        var firstDoorTrigger = sceneRoot.transform.FindChild("ginsoTreeMultiMortar/doorASetup/triggerCollider").GetComponent<PlayerCollisionStayTrigger>();
        var firstDoorShut = !firstDoorAnimator.AtStart; // Or shutting.
        var minibossRoom = Rect.MinMaxRect(504f, 235.5f, 545f, 255f);
        var isInRoom = minibossRoom.Contains(Characters.Sein.Position);
        if (firstDoorShut && !isInRoom) {
            firstDoorAnimator.Stopped = true;
            firstDoorAnimator.Reversed = false;
            firstDoorAnimator.CurrentTime = 0f;
            firstDoorAnimator.Sample(firstDoorAnimator.CurrentTime);
            firstDoorTrigger.gameObject.active = true;
            firstDoorTrigger.Active = true;
        }
    }

    private static void BootstrapMistyPedestal(SceneRoot sceneRoot) {
        // So at the start of misty we get a useless press X to interact with sein that tells 
        // us literally nothing because we have silenced some dialog popups, so remove that.
        var initialHint = sceneRoot.transform.FindChild("*storySetup/hintStoryAreaB");
        initialHint.gameObject.active = false;

        // Make it clearer what resetting misty via the "Press X to interact with shrouded 
        // lantern" popup does for the player.
        var changeTrigger = sceneRoot.transform.FindChild("*toggleTorchSetup/toggleTorchSetup/oriInterestTrigger").GetComponent<OriInterestTriggerB>();
        var changeTriggerText = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        changeTriggerText.SetMessage("Press [StructureInteraction] to change the layout of *Misty Woods*!");
        changeTrigger.HintMessage = changeTriggerText;

        var changeToRevisitSequence = sceneRoot.transform.FindChild("*toggleTorchSetup/toggleTorchSetup/oriInterestTrigger/extinguishSequence").GetComponent<ActionSequence>();
        var revisitHint = changeToRevisitSequence.gameObject.AddComponent<ShowHintAction>();
        var revisitText = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        revisitText.SetMessage("*Misty Woods* is now in the #normal# layout.");
        revisitHint.HintMessage = revisitText;
        revisitHint.Duration = 3f;
        changeToRevisitSequence.Actions.Add(revisitHint);

        var changeToFinishedSequence = sceneRoot.transform.FindChild("*toggleTorchSetup/toggleTorchSetup/oriInterestTrigger/igniteSequence").GetComponent<ActionSequence>();
        var finishedHint = changeToFinishedSequence.gameObject.AddComponent<ShowHintAction>();
        var finishedText = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        finishedText.SetMessage("*Misty Woods* is now in the #finished# layout.");
        finishedHint.HintMessage = finishedText;
        finishedHint.Duration = 3f;
        changeToFinishedSequence.Actions.Add(finishedHint);

        // Put a hint on reentry of misty when misty is complete.
        var pedestalTorch = sceneRoot.transform.FindChild("pedestalTorch");
        var reentryHintTransform = CloneObject(sceneRoot, pedestalTorch, "reentryHint");
        // The location of the collision trigger.
        reentryHintTransform.position = new Vector3(-606, -26);
        reentryHintTransform.localScale = new Vector3(5, 20);

        var reentryText = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        reentryText.SetMessage("You can change the Misty layout at the orb pedestal");
        var reentryHint = reentryHintTransform.gameObject.AddComponent<ShowSpiritTreeTextAction>();
        reentryHint.Message = reentryText;
        // Location of the text.
        var reentryTextTarget = CloneObject(sceneRoot, pedestalTorch, "reentryTarget");
        reentryTextTarget.position = new Vector3(-619, -25);
        reentryHint.Target = reentryTextTarget;

        // Make it only show when misty is in the finished state and we are going left 
        // (entering misty again).
        var collisionTrigger = reentryHintTransform.gameObject.AddComponent<PlayerCollisionTrigger>();
        collisionTrigger.ActionToRun = reentryHint;
        var goingLeftCondition = reentryHintTransform.gameObject.AddComponent<RandomizerGoingDirectionCondition>();
        goingLeftCondition.left = true;
        var mistyCompleteCondition = sceneRoot.transform.FindChild("*toggleTorchSetup/toggleTorchSetup/oriInterestTrigger/activateAction").GetComponent<GetWorldEventCondition>();
        var compoundCondition = reentryHintTransform.gameObject.AddComponent<CompoundCondition>();
        var conditionInformation = new CompoundCondition.ConditionInformation();
        conditionInformation.Conditions.Add(mistyCompleteCondition);
        conditionInformation.Conditions.Add(goingLeftCondition);
        compoundCondition.Tests.Add(conditionInformation);
        collisionTrigger.Condition = compoundCondition;
    }

    private static void BootstrapGinsoUpperMiniboss(SceneRoot sceneRoot) {
        // Apply miniboss room patches unless closed dungeons flag is set
        if (Randomizer.Inventory.GetRandomizerItem(801) == 0) {
            // Prevent the entry door from closing when you enter the room
            var trigger = sceneRoot.transform.FindChild("*turretEnemyPuzzle/*doorASetup/triggerCollider").GetComponent<PlayerCollisionStayTrigger>();
            trigger.Active = false;

            // Start the moving platforms moving
            var timeline = sceneRoot.transform.FindChild("*turretEnemyPuzzle/*doorASetup/timelinePlatformsBefore").GetComponent<TimelineSequence>();
            timeline.AnimatorDriver.IsPlaying = true;

            // Force the exit door open and remove the stray ring graphics
            var exitDoor = sceneRoot.transform.FindChild("*turretEnemyPuzzle/*enemyPuzzle/doorSetup/sidewaysDoor");
            var animator = exitDoor.FindChild("puzzleDoorLeftGinso").GetComponent<LegacyTranslateAnimator>();
            animator.TimeOffset = animator.TimeOfLastCurvePoint;
            animator.SampleFirstFrameOnStart = true;
            animator = exitDoor.FindChild("puzzleDoorRightGinso").GetComponent<LegacyTranslateAnimator>();
            animator.TimeOffset = animator.TimeOfLastCurvePoint;
            animator.SampleFirstFrameOnStart = true;
            exitDoor.FindChild("keyrings").gameObject.active = false;

            // Prevent the exit door from reopening after defeating the miniboss
            var sequence = sceneRoot.transform.FindChild("*turretEnemyPuzzle/*enemyPuzzle/*enemyPuzzle/actionSequence").GetComponent<ActionSequence>();
            sequence.Actions.RemoveAt(13);
            ActionSequence.Rename(sequence.Actions);
        }
    }

    private static void BootstrapForlornRuinsBridge(SceneRoot sceneRoot) {
        // Apply bridge room patches unless closed dungeons flag is set
        if (Randomizer.Inventory.GetRandomizerItem(801) == 0) {
            var setupGravity = sceneRoot.transform.FindChild("*setupGravity");

            // Open the door to the laser room
            var doorAnimator = setupGravity.FindChild("solidWallSetup/bombableSolidWallSetup").GetComponent<TransformAnimator>();
            doorAnimator.AnimatorDriver.CurrentTime = doorAnimator.AnimatorDriver.Duration;
            doorAnimator.Initialize();
            doorAnimator.SampleValue(doorAnimator.AnimatorDriver.CurrentTime, true);

            // Deactivate one specific laser animation (not the others with the same name, for some reason)
            var action = setupGravity.FindChild("pedestalAction/*setups/actions/mainAction").GetComponent<ActionSequence>().Actions[15] as ActivateAction;
            action.Target.active = false;

            // Deactivate the cutscene where you give up the orb
            var trigger = setupGravity.FindChild("pedestalAction/*setups/triggers/cutsceneCollisionTrigger").GetComponent<PlayerCollisionStayTrigger>();
            trigger.Active = false;

            // Force the bridge timeline to the end and activate its colliders
            // Everything about TimelineSequence sucks and if I have to sledgehammer every goddamn animator to the end I will so help me god
            var sequence = setupGravity.FindChild("timelineSequence").GetComponent<TimelineSequence>();
            foreach (var sequenceEntry in sequence.Entries) {
                if (sequenceEntry.Animator.GetType() == typeof(TimelineSequence)) {
                    foreach (var subSequenceEntry in (sequenceEntry.Animator as TimelineSequence).Entries) {
                        subSequenceEntry.Animator.AnimatorDriver.IsPlaying = true;
                        subSequenceEntry.Animator.AnimatorDriver.CurrentTime = subSequenceEntry.Animator.AnimatorDriver.Duration;
                    }
                } else {
                    sequenceEntry.Animator.AnimatorDriver.IsPlaying = true;
                    sequenceEntry.Animator.AnimatorDriver.CurrentTime = sequenceEntry.Animator.AnimatorDriver.Duration;
                }
            }

            setupGravity.FindChild("timelineSequence/bridgeColliders").GetComponent<GameObjectActivator>().ActiveAtStart = true;
        }
    }

    private static Dictionary<string, Action<SceneRoot>> s_bootstrapPreEnabled = new Dictionary<string, Action<SceneRoot>> {
        { "moonGrottoRopeBridge", BootstrapMoonGrottoBridge },
        { "mountHoruHubMid", BootstrapMountHoruHub },
        { "mountHoruLaserPuzzle", BootstrapMountHoruLaserPuzzle },
        { "northMangroveFallsLanternIntro", BootstrapBlackrootLanternRoom },
        { "mangroveFallsDashIntro", BootstrapBlackrootBoulderArea },
        { "spiritTreeRefined", BootstrapSpiritTree },
        { "sunkenGladesIntroSplitA", BootstrapSunkenGladesKeystoneDoor },
        { "sunkenGladesIntroSplitB", BootstrapSunkenGladesSpiritWell },
        { "thornfeltSwampActTwoStart", BootstrapThornfeltSwampMain },
        { "titleScreenSwallowsNest", BootstrapTitleScreen },
        { "westGladesMistyWoodsCaveTransition", BootstrapValleyEntry },
        { "westGladesFireflyAreaA", BootstrapValleyThreeBirdArea },
        { "sunkenGladesRunaway", BootstrapSunkenGladesRunaway },
        { "sunkenGladesSpiritCavernWalljumpB", BootstrapWallJumpTreeHint },
        { "sunkenGladesOriRoom", BootstrapSeinRoomHint },
        { "sunkenGladesEnemyIntroductionC", BootstrapRhinoBeforeSein },
        { "sorrowPassForestB", BootstrapMistyPedestal },
        { "ginsoTreeResurrection", BootstrapGinsoUpperMiniboss },
        { "forlornRuinsC", BootstrapForlornRuinsBridge },
    };

    private static List<string> s_bootstrappedScenesPreEnabled = new List<string>();

    // Generally prefer PreEnabled over AfterSerialize. These functions are run after *every* 
    // serialisation of the scene, so after every death and not just the initial load. So don't
    // e.g. unconditionally add things to the scene in these functions, as they will repeat. 
    // But if you need to do things that alter or depend on serialised parts of the scene, 
    // this is the place. Things altered here may be serialised (saved) by the scene. If you 
    // want to make new serialised scene elements you'll need to use PreEnabled.
    private static Dictionary<string, Action<SceneRoot>> s_bootstrapAfterSerialize = new Dictionary<string, Action<SceneRoot>> {
        { "moonGrottoEnemyPuzzle", BootstrapMoonGrottoMiniboss },
        { "sunkenGladesOriRoom", BootstrapSeinRoomWall },
        { "ginsoTreePuzzles", BootstrapGinsoLowerMiniboss },
    };

    private static List<string> s_bootstrappedScenesAfterSerialize = new List<string>();
}

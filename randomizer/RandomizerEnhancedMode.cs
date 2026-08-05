using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public class RandomizerEnhancedMode {
    private static void AddEnhancedModeTextAction(ActionSequence sequence, int index, MessageDescriptor[] messages, bool dontFixWaitAction = false) {
        var obj = new GameObject("textAction");
        obj.transform.parent = sequence.transform;

        var textAction = obj.AddComponent<ShowEnhancedSpiritFlameTextAction>();
        textAction.Messages = messages;
        textAction.FreezeGame = false;

        sequence.Actions[index] = textAction;
        if (dontFixWaitAction) {
            return;
        }

        (sequence.Actions[index + 1] as WaitAction).LastAction = textAction;
    }

    public static void BootstrapSceneWater(SceneRoot sceneRoot) {
        var objectsToDeactivate = WaterBootstrapScenes[sceneRoot.name];

        var conditionObj = new GameObject("enhancedWaterLogic");
        conditionObj.transform.parent = sceneRoot.transform;

        var condition = conditionObj.AddComponent<RandomizerEnhancedCleanWaterCondition>();

        foreach (var path in objectsToDeactivate) {
            var transform = sceneRoot.transform.FindChild(path);

            if (transform != null) {
                var activate = conditionObj.AddComponent<ActivateBasedOnCondition>();
                activate.Target = transform.gameObject;
                activate.Activate = false;
                activate.Condition = condition;
            }
        }

        if (sceneRoot.name == "thornfeltSwampMoonGrottoTransition") {
            // make the drain instantly complete if enhanced water is on
            var sequence = sceneRoot.transform.FindChild("*releaseWaterSequence/actionSequence").GetComponent<ActionSequence>();
            var timeline = sceneRoot.transform.FindChild("*releaseWaterSequence/timelineSequence").gameObject;
            var actionObj = new GameObject("drainAction");
            actionObj.transform.parent = sequence.transform;

            var enhancedChild = new GameObject("enhancedDrainAction");
            enhancedChild.transform.parent = actionObj.transform;
            var enhancedAction = enhancedChild.AddComponent<BaseAnimatorAction>();
            enhancedAction.Target = timeline;
            enhancedAction.AnimatorsMode = BaseAnimatorAction.FindAnimatorsMode.GameObject;
            enhancedAction.Command = BaseAnimatorAction.PlayMode.StopAtEnd;

            var baseChild = new GameObject("baseAction");
            baseChild.transform.parent = actionObj.transform;
            var baseAction = baseChild.AddComponent<BaseAnimatorAction>();
            baseAction.Target = timeline;
            baseAction.AnimatorsMode = BaseAnimatorAction.FindAnimatorsMode.GameObject;
            baseAction.Command = BaseAnimatorAction.PlayMode.Restart;

            var runAction = actionObj.AddComponent<RunActionCondition>();
            runAction.Action = enhancedAction;
            runAction.ElseAction = baseAction;
            runAction.Condition = condition;

            // don't play the sound if enhanced water is on
            var soundActionObj = sequence.Actions[1].gameObject;
            var notCondition = soundActionObj.AddComponent<NotCondition>();
            notCondition.Condition = condition;

            var runSoundAction = soundActionObj.AddComponent<RunActionCondition>();
            runSoundAction.Action = sequence.Actions[1];
            runSoundAction.Condition = notCondition;

            sequence.Actions[1] = runSoundAction;
            sequence.Actions[5] = runAction;
            ActionSequence.Rename(sequence.Actions);
        }
    }

    private static string GetItemHintForPickup(string pickupName) {
        var pickupAction = RandomizerLocationManager.LocationsByName[pickupName].Pickup;

        if (pickupAction == null) {
            return "nothing";
        }

        if (pickupAction.Action == "SK") {
            return "$a skill$";
        }

        if (pickupAction.Action == "EV") {
            var eventPickup = (int)pickupAction.Value;
            if (eventPickup == 0 || eventPickup == 2 || eventPickup == 4) {
                return "#a dungeon key#";
            }

            if (eventPickup == 1 || eventPickup == 3 || eventPickup == 5) {
                return "#an event#";
            }
        } else if (pickupAction.Action == "RB") {
            var bonus = (int)pickupAction.Value;
            if (bonus == 17 || bonus == 19 || bonus == 21) {
                return "#a shard#";
            }

            if (bonus == 28) {
                return "#a fragment#";
            }

            if (bonus >= 300 && bonus <= 311) {
                return "#a keystone#";
            }
        } else if (pickupAction.Action == "TP") {
            return "#a teleporter#";
        } else if (pickupAction.Action == "WT") {
            return "#a relic#";
        } else if (pickupAction.Action == "HC" || pickupAction.Action == "EC" || pickupAction.Action == "AC") {
            return "a cell";
        } else if (pickupAction.Action == "KS") {
            return "a keystone";
        } else if (pickupAction.Action == "MS") {
            return "a mapstone";
        } else if (pickupAction.Action == "EX") {
            return "some experience";
        }

        return "some item";
    }

    private static string GetLocationHintForDungeonKey(int keyID) {
        foreach (var loc in RandomizerLocationManager.LocationsByName.Values) {
            if (loc.Pickup != null && loc.Pickup.Action == "EV" && (int)loc.Pickup.Value == keyID) {
                switch (loc.Type) {
                    case RandomizerLocationManager.Location.LocationType.ExpSmall:
                    case RandomizerLocationManager.Location.LocationType.ExpMedium:
                    case RandomizerLocationManager.Location.LocationType.ExpLarge:
                        return "on an experience orb";
                    case RandomizerLocationManager.Location.LocationType.HealthCell:
                        return "on a health cell";
                    case RandomizerLocationManager.Location.LocationType.EnergyCell:
                        return "on an energy cell";
                    case RandomizerLocationManager.Location.LocationType.AbilityCell:
                        return "on an ability cell";
                    case RandomizerLocationManager.Location.LocationType.Keystone:
                        return "on a keystone";
                    case RandomizerLocationManager.Location.LocationType.Mapstone:
                        return "on a mapstone";
                    case RandomizerLocationManager.Location.LocationType.Skill:
                        return "on a skill location";
                    case RandomizerLocationManager.Location.LocationType.Plant:
                        return "on a petrified plant";
                    case RandomizerLocationManager.Location.LocationType.Event:
                        return "on an event location";
                    case RandomizerLocationManager.Location.LocationType.Cutscene:
                        return "at the end of a room in #Mount Horu#";
                    case RandomizerLocationManager.Location.LocationType.ProgressiveMap:
                        return "on a #Map Altar";
                }
            }
        }

        return "in #Niwen#";
    }

    private static void GenerateSkillTreeText(ActionSequence sequence, int index, string locName, string originalSkill) {
        var hintText = $"This is the ${originalSkill}$ tree, but unfortunately ${originalSkill}$ isn't here this time.";
        var pickupAction = RandomizerLocationManager.LocationsByName[locName].Pickup;

        if (pickupAction != null && pickupAction.Action == "SK") {
            var skillID = (int)pickupAction.Value;
            string hintSkill = null;

            switch (skillID) {
                case 0:
                    hintSkill = "Bash";
                    break;
                case 2:
                    hintSkill = "Charge Flame";
                    break;
                case 3:
                    hintSkill = "Wall Jump";
                    break;
                case 4:
                    hintSkill = "Stomp";
                    break;
                case 5:
                    hintSkill = "Double Jump";
                    break;
                case 8:
                    hintSkill = "Charge Jump";
                    break;
                case 12:
                    hintSkill = "Climb";
                    break;
                case 14:
                    hintSkill = "Glide";
                    break;
                case 50:
                    hintSkill = "Dash";
                    break;
                case 51:
                    hintSkill = "Grenade";
                    break;
            }

            if (hintSkill != null) {
                hintText = $"This is the ${hintSkill}$ tree.";
            }
        }

        AddEnhancedModeTextAction(
            sequence,
            index,
            new[] {
                new MessageDescriptor(hintText),
            }
        );
    }

    public static void BootstrapSeinRoomText(SceneRoot sceneRoot) {
        var seinSequence = sceneRoot.transform.FindChild("*setups/*story/findingOri/seinInterestZone/trigger/activateSequence").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            seinSequence,
            27,
            new[] {
                new MessageDescriptor("My voice... is returning...\nAt last, I can speak once more, after 8 years of silence."),
                new MessageDescriptor("I am #Sein#, the light and eyes of the randomizer developers. I will be happy to waste your time during your journey."),
                new MessageDescriptor("But if you really can't stand my presence, then you can silence me once more by pressing #Shift+Alt+U#."),
            }
        );

        var postFightSequence = sceneRoot.transform.FindChild("*setups/*story/allEnemiesKilled/group/actionSequence").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            postFightSequence,
            10,
            new[] {
                new MessageDescriptor("These poor creatures...they could have lived, if you had just used the randomizer's Warp feature.", EmotionType.Sad, null),
            }
        );
    }

    public static void BootstrapSunkenGladesKeystoneDoorText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*allEnemiesKilled/activated/*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            7,
            new[] {
                new MessageDescriptor("This is a #door#. A #door# is like a wall, except you can go through it."),
                new MessageDescriptor("...what are you looking at me like that for? Of course you can't go through a wall! Don't be silly."),
            }
        );
    }

    public static void BootstrapGladesMapText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();

        if (Randomizer.ForceMaps) {
            var mapsByBadZone = new Dictionary<string, int> {
                { "Blackroot", 0 },
                { "Misty", 0 },
                { "Forlorn", 0 },
                { "Horu", 0 },
            };

            foreach (var loc in RandomizerLocationManager.LocationsByName.Values) {
                if (mapsByBadZone.ContainsKey(loc.Zone) && loc.Pickup?.Action == "MS") {
                    mapsByBadZone[loc.Zone] += 1;
                }
            }

            var hintText = "Don't worry, none of the mapstones are in awful places this time! ...probably.";

            var hintZone = "";
            var hintMapstones = 12;
            foreach (var pair in mapsByBadZone) {
                if (pair.Value > 0 && pair.Value < hintMapstones) {
                    hintZone = pair.Key;
                    hintMapstones = pair.Value;
                }
            }

            if (hintMapstones == 1) {
                hintText = $"Don't worry, only one of the mapstones is in {hintZone} this time!";
            } else if (hintMapstones < 12) {
                hintText = $"Don't worry, only {hintMapstones} of the mapstones are in {hintZone} this time!";
            }

            AddEnhancedModeTextAction(
                sequence,
                8,
                new[] {
                    new MessageDescriptor("Ori, this is a #Map#. Or #Map Stone#? #Map Altar#? Honestly, I'm not even sure anymore."),
                    new MessageDescriptor("Anyway, you're going to have to place mapstones in all 9 #Map Monuments# to finish your journey."),
                    new MessageDescriptor(hintText),
                }
            );
        } else {
            var pickupHint = "some item";
            var hintMap = 9;
            for (; hintMap > 0; --hintMap) {
                var mapPickup = RandomizerLocationManager.ProgressiveMapLocations[hintMap - 1].Pickup;
                if (mapPickup == null) {
                    continue;
                }

                if (mapPickup.Action == "SK") {
                    pickupHint = "$a skill$";
                    break;
                }

                if (mapPickup.Action == "EV") {
                    var eventPickup = (int)mapPickup.Value;
                    if (eventPickup == 0 || eventPickup == 2 || eventPickup == 4) {
                        pickupHint = "#a dungeon key#";
                        break;
                    }

                    if (eventPickup == 1 || eventPickup == 3 || eventPickup == 5) {
                        pickupHint = "#an event#";
                        break;
                    }
                } else if (mapPickup.Action == "RB") {
                    var bonus = (int)mapPickup.Value;
                    if (bonus == 17 || bonus == 19 || bonus == 21) {
                        pickupHint = "#a shard#";
                        break;
                    }

                    if (bonus == 28) {
                        pickupHint = "#a fragment#";
                        break;
                    }

                    if (bonus >= 300 && bonus <= 311) {
                        pickupHint = "#a keystone#";
                        break;
                    }
                } else if (mapPickup.Action == "TP") {
                    pickupHint = "#a teleporter#";
                    break;
                }
            }

            if (hintMap == 0) {
                var firstMapPickup = RandomizerLocationManager.ProgressiveMapLocations[0].Pickup;
                if (firstMapPickup.Action == "HC" || firstMapPickup.Action == "EC" || firstMapPickup.Action == "AC") {
                    pickupHint = "a cell";
                } else if (firstMapPickup.Action == "KS") {
                    pickupHint = "a keystone";
                } else if (firstMapPickup.Action == "MS") {
                    pickupHint = "a mapstone";
                } else if (firstMapPickup.Action == "EX") {
                    pickupHint = "some experience";
                }
            }

            var hintMapAsText = hintMap == 0 || hintMap == 1 ? "one mapstone in a #Map Pedestal#" : $"{hintMap} mapstones in #Map Pedestals#";

            AddEnhancedModeTextAction(
                sequence,
                8,
                new[] {
                    new MessageDescriptor("Ori, this is a #Map#. Or #Map Stone#? #Map Altar#? Honestly, I'm not even sure anymore."),
                    new MessageDescriptor("Anyway, you can receive items by placing mapstones in these #Map Monuments#!"),
                    new MessageDescriptor($"For example, if you place {hintMapAsText}, you'll find {pickupHint}!"),
                }
            );
        }
    }

    public static void BootstrapWallJumpTreeText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*storySetups/storyTextWithTrigger/action").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            7,
            new[] {
                new MessageDescriptor("Ori."),
                new MessageDescriptor("Quaza."),
                new MessageDescriptor("Shaka."),
                new MessageDescriptor("...why are you looking at me like that?"),
            }
        );
    }

    public static void BootstrapSpiritTreeText(SceneRoot sceneRoot) {
        var attackSequence = sceneRoot.transform.FindChild("*spiritTreeStorySetup/container/actionSequences/02. kuroAttackActionSequence").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            attackSequence,
            0,
            new[] {
                new MessageDescriptor("I'm sorry, Ori, but stepping on this #Spirit Well# triggers a lengthy cutscene.", EmotionType.Sad, null),
            },
            true
        );
        (attackSequence.Actions[3] as WaitAction).LastAction = attackSequence.Actions[0] as ShowEnhancedSpiritFlameTextAction;

        var mapSequence = sceneRoot.transform.FindChild("*spiritTreeStorySetup/container/actionSequences/03. worldMapActionSequence").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            mapSequence,
            15,
            new[] {
                new MessageDescriptor("It's possible to skip this cutscene using #Save Anywhere#. This may be a good idea if you're trying to complete your journey as quickly as possible!"),
            }
        );

        var returnSequence = sceneRoot.transform.FindChild("*spiritTreeStorySetup/container/actionSequences/04. returnCameraToPlayerActionSequence").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            returnSequence,
            5,
            new[] {
                new MessageDescriptor("Depending on the circumstances, skipping this cutscene may or not be the best option. Try it out sometimes and see how you feel about it!"),
                new MessageDescriptor("If you don't know how to do #Save Anywhere#, then you can ask on the #Discord#!", EmotionType.Happy, null),
            }
        );
    }

    public static void BootstrapSpiritTreeMapText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*setup/actionSequence").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            4,
            new[] {
                new MessageDescriptor("Watching this cutscene takes around 40 seconds. You could already be checking the $Charge Flame$ tree by now!"),
            }
        );
        AddEnhancedModeTextAction(
            sequence,
            10,
            new[] {
                new MessageDescriptor("Of course, if you skip this cutscene, then you don't activate this #Spirit Well# and you can't teleport to it."),
            }
        );
        AddEnhancedModeTextAction(
            sequence,
            16,
            new[] {
                new MessageDescriptor("But if you only return to this area once or twice, then it may still be faster overall to skip the cutscene."),
            }
        );
        AddEnhancedModeTextAction(
            sequence,
            22,
            new[] {
                new MessageDescriptor("There's also always a chance you get the teleporter as a pickup! That would save even more time!"),
            }
        );
    }

    public static void BootstrapChargeFlameTreeText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        GenerateSkillTreeText(sequence, 8, "ChargeFlameSkillTree", "Charge Flame");
    }

    public static void BootstrapGinsoDoorText(SceneRoot sceneRoot) {
        var itemHint = GetItemHintForPickup("WaterVein");
        var stealSequence = sceneRoot.transform.FindChild("*setups/stealingSetup/setupA/action").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            stealSequence,
            11,
            new[] {
                new MessageDescriptor($"A #Gumon# from the #Forlorn Ruins#! He's running away with {itemHint}! Quickly, after him!", EmotionType.Urgent, null),
            }
        );

        var failSequence = sceneRoot.transform.FindChild("*setups/openingGinsoTree/oriInterestArea/failedAction").GetComponent<ActionSequence>();
        if (!Randomizer.Shards) {
            var keyHint = GetLocationHintForDungeonKey(0);
            AddEnhancedModeTextAction(
                failSequence,
                2,
                new[] {
                    new MessageDescriptor($"Ori, you need the *Water Vein* to open this door. You can find it {keyHint}!"),
                }
            );
        } else {
            AddEnhancedModeTextAction(
                failSequence,
                2,
                new[] {
                    new MessageDescriptor("Ori, you need the *Water Vein* to open this door. Good luck finding the shards!"),
                }
            );
        }
    }

    public static void BootstrapDoubleJumpTreeText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        GenerateSkillTreeText(sequence, 8, "DoubleJumpSkillTree", "Double Jump");
    }

    public static void BootstrapGinsoEntranceText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*setup/*enterScene/action").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            15,
            new[] {
                new MessageDescriptor("I hope you brought some verticality, Ori! Or maybe $Dash$, if you know how to use it."),
            }
        );
    }

    public static void BootstrapBashTreeText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        GenerateSkillTreeText(sequence, 8, "BashSkillTree", "Bash");
    }

    public static void BootstrapWaterElementText(SceneRoot sceneRoot) {
        var pickupHint = GetItemHintForPickup("GinsoEscapeExit");
        var seeHeartSequence = sceneRoot.transform.FindChild("*heartResurrection/seeHeartSetup/seeHeart").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            seeHeartSequence,
            6,
            new[] {
                new MessageDescriptor("Please tell me you'll actually restore the #Element of Waters#! Everyone always skips it."),
            }
        );
        AddEnhancedModeTextAction(
            seeHeartSequence,
            11,
            new[] {
                new MessageDescriptor($"Look...what if I told you that you can get {pickupHint} at the end of the escape? Would you do it then?"),
            }
        );

        var startSequence = sceneRoot.transform.FindChild("*heartResurrection/restoringHeartWaterRising/waterSequenceStartAction").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            startSequence,
            3,
            new[] {
                new MessageDescriptor($"Thanks, Ori! Just so you know, I wasn't lying earlier when I said you would get {pickupHint}."),
                new MessageDescriptor("Or at least, if I was lying, you'll probably be too distracted to remember that I did."),
            }
        );
    }

    public static void BootstrapStompTreeText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        GenerateSkillTreeText(sequence, 8, "StompSkillTree", "Stomp");
    }

    public static void BootstrapValleyMainText(SceneRoot sceneRoot) {
        var lowerSequence = sceneRoot.transform.FindChild("*getFeatherSetupContainer/*kuroCliffLowerHint/*action").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            lowerSequence,
            9,
            new[] {
                new MessageDescriptor("Ori, I'm sensing a large area of instant, guaranteed death ahead! What could be causing it?"),
            }
        );
        AddEnhancedModeTextAction(
            lowerSequence,
            16,
            new[] {
                new MessageDescriptor("Oh heck, it's a bird!", EmotionType.Urgent, null),
            }
        );

        var higherSequence = sceneRoot.transform.FindChild("*getFeatherSetupContainer/*kuroCliffHigherHint/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            higherSequence,
            10,
            new[] {
                new MessageDescriptor("Ori, I know you're thinking of #dropping those rocks on Kuro's head#, but that's extremely dangerous!"),
                new MessageDescriptor("A heavy impact to the head could cause a #concussion#, which is a type of brain injury that could lead to serious complications."),
                new MessageDescriptor("We should consider asking Kuro to put on a #helmet# first. You can never be too careful!"),
            }
        );
    }

    public static void BootstrapMistyTorchText(SceneRoot sceneRoot) {
        var pickupHint = GetItemHintForPickup("GumonSeal");
        var sequence = sceneRoot.transform.FindChild("*storySetups/storyTextWithTrigger/action").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            8,
            new[] {
                new MessageDescriptor($"Look! #Atsu's Torch#! If you were to pick that up and slowly, painfully carry it for a long distance, you could get {pickupHint}!"),
            }
        );
    }

    public static void BootstrapClimbTreeText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        GenerateSkillTreeText(sequence, 8, "ClimbSkillTree", "Climb");
    }

    public static void BootstrapGumonSealText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*storySetup/*liftFogSequence/liftFogAction").GetComponent<ActionSequence>();

        if (Randomizer.SeedMeta.Contains("Limitkeys")) {
            AddEnhancedModeTextAction(
                sequence,
                23,
                new[] {
                    new MessageDescriptor("I hope this is your dungeon key, Ori, because otherwise it's probably in the #Forlorn Ruins#!"),
                }
            );
        } else if (Randomizer.WorldTour) {
            if (Randomizer.RelicZoneLookup.ContainsValue("Misty")) {
                AddEnhancedModeTextAction(
                    sequence,
                    23,
                    new[] {
                        new MessageDescriptor("I hope this is your relic, Ori, because otherwise it's probably in the $Grenade$-locked pickup!"),
                    }
                );
            } else {
                AddEnhancedModeTextAction(
                    sequence,
                    23,
                    new[] {
                        new MessageDescriptor("Ori, you know that there's no relic in Misty Woods this time, right?"),
                    }
                );
            }
        } else if (BingoController.Active) {
            AddEnhancedModeTextAction(
                sequence,
                23,
                new[] {
                    new MessageDescriptor("Do you have a bingo goal to visit this location, Ori? Sorry to hear that."),
                }
            );
        } else {
            AddEnhancedModeTextAction(
                sequence,
                23,
                new[] {
                    new MessageDescriptor("Wow, you're really checking the #Gumon Seal# this time?"),
                }
            );
        }
    }

    public static void BootstrapForlornDoorText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*setups/openingForlornRuins/oriInterestArea/failedAction").GetComponent<ActionSequence>();
        if (!Randomizer.Shards) {
            var keyHint = GetLocationHintForDungeonKey(2);
            AddEnhancedModeTextAction(
                sequence,
                7,
                new[] {
                    new MessageDescriptor($"Ori, you need the #Gumon Seal# to open this door. You can find it {keyHint}!"),
                }
            );
        } else {
            AddEnhancedModeTextAction(
                sequence,
                7,
                new[] {
                    new MessageDescriptor("Ori, you need the #Gumon Seal# to open this door. Good luck finding the shards!"),
                }
            );
        }
    }

    public static void BootstrapForlornEntranceText(SceneRoot sceneRoot) {
        var enterSequence = sceneRoot.transform.FindChild("*enterRuins/enterScene").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            enterSequence,
            11,
            new[] {
                new MessageDescriptor("Ah, these are the #Forlorn Ruins#, home to the #Gumon#. I wonder what makes them so forlorn? And why they're in ruins?"),
            }
        );
        AddEnhancedModeTextAction(
            enterSequence,
            15,
            new[] {
                new MessageDescriptor("Oh. Oh god. They're dead. They're all dead. All #seven# of them!", EmotionType.Sad, null),
            }
        );
        AddEnhancedModeTextAction(
            enterSequence,
            19,
            new[] {
                new MessageDescriptor("Ori, I'm sorry...I didn't realize this game was so dark. If you need to step away for a while, I understand.", EmotionType.Sad, null),
            }
        );

        var orbSequence = sceneRoot.transform.FindChild("*storySetups/storyTextWithTrigger/action").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            orbSequence,
            7,
            new[] {
                new MessageDescriptor("Orb."),
            }
        );
    }

    public static void BootstrapForlornLaserRoomText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            7,
            new[] {
                new MessageDescriptor("This makes #eight#, I guess."),
            }
        );
    }

    public static void BootstrapWindElementText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*story/resurrectionSequence/*activateSequence").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            7,
            new[] {
                new MessageDescriptor("Ori, do you remember when #Gareth Coker# visited an Ori speedrunner's stream?"),
            }
        );

        AddEnhancedModeTextAction(
            sequence,
            12,
            new[] {
                new MessageDescriptor("It's true! He said that the music for this escape sequence was made at the last minute after they were already done recording music for the game."),
                new MessageDescriptor("Pay close attention and you might be able to hear that it's actually just the same music as the #Ginso Tree# escape, digitally pitched up half a step!"),
            }
        );
    }

    public static void BootstrapChargeJumpTreeText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        GenerateSkillTreeText(sequence, 8, "ChargeJumpSkillTree", "Charge Jump");
    }

    public static void BootstrapSunstoneText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*sunStoneSetup/*storySetups/storyTextWithTrigger/action").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            7,
            new[] {
                new MessageDescriptor("Take your business straight to the top of #Nibel# with in-dialogue advertising! Your product could be right here! Contact @Vulajin@ for pricing inquiries."),
            }
        );
    }

    public static void BootstrapBlackrootEntranceText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*naruStatueHintSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            5,
            new[] {
                new MessageDescriptor("Don't forget to save after watching this cutscene! Dying in #Blackroot Burrows# can be frustrating enough without having to watch this again!"),
            }
        );
    }

    public static void BootstrapBlackrootLanternText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*naruStorySetupA/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            7,
            new[] {
                new MessageDescriptor("Did you know that the slime right above us drops a guaranteed energy fragment? *Eiko* taught me that!"),
            }
        );
    }

    public static void BootstrapDashTreeText(SceneRoot sceneRoot) {
        var darknessSequence = sceneRoot.transform.FindChild("*liftDarknessSetup/*pedestalSetup/actions/mainAction").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            darknessSequence,
            21,
            new[] {
                new MessageDescriptor("There are some cool cutscene skips you can do in this room! Some of them involve some tight timing though."),
                new MessageDescriptor("Actually, this dialogue might mess with some of that timing. Hopefully not!"),
            }
        );

        var treeSequence = sceneRoot.transform.FindChild("*enabledByDarknessLifted/*naruStorySetupA/objectiveSetupAction").GetComponent<ActionSequence>();
        GenerateSkillTreeText(treeSequence, 10, "DashSkillTree", "Dash");
    }

    public static void BootstrapBlackrootTeleporterText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*objectiveSetup/container/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();

        if (BingoController.Active) {
            AddEnhancedModeTextAction(
                sequence,
                23,
                new[] {
                    new MessageDescriptor("Don't forget to die to the crushers below here! It might be on your bingo card!"),
                }
            );
        } else {
            AddEnhancedModeTextAction(
                sequence,
                23,
                new[] {
                    new MessageDescriptor("Did you leave the fronkey below here alive? He may be able to help you break the floor to explore deeper!"),
                }
            );
        }
    }

    public static void BootstrapGrenadeTreeText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        GenerateSkillTreeText(sequence, 8, "GrenadeSkillTree", "Grenade");
    }

    public static void BootstrapLostGroveCutsceneText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*setup/objectiveSetupAction").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            39,
            new[] {
                new MessageDescriptor("Remember #those who have passed# and they will live on forever in your heart.", EmotionType.Happy, null),
            }
        );
    }

    public static void BootstrapHoruDoorText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*storySetup(noKey)/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        if (!Randomizer.Shards) {
            var keyHint = GetLocationHintForDungeonKey(4);
            AddEnhancedModeTextAction(
                sequence,
                7,
                new[] {
                    new MessageDescriptor($"Ori, you need the @Sunstone@ to open this door. You can find it {keyHint}!"),
                }
            );
        } else {
            AddEnhancedModeTextAction(
                sequence,
                7,
                new[] {
                    new MessageDescriptor("Ori, you need the @Sunstone@ to open this door. Good luck finding the shards!"),
                }
            );
        }

        // "*setups/openingMountHoru/oriInterestArea/*activateSequence" - index 10
    }

    public static void BootstrapHoruEntranceText(SceneRoot sceneRoot) {
        var pickupHint = GetItemHintForPickup("DoorWarpExp");
        var numTries = new Random(31 * Randomizer.SeedMeta.GetHashCode()).Next(20);
        var tryText = numTries == 1 ? "first" : numTries == 2 ? "second" : numTries == 3 ? "third" : $"{numTries}th";
        var sequence = sceneRoot.transform.FindChild("*enterScene/action").GetComponent<ActionSequence>();

        if (Randomizer.Inventory.GetRandomizerItem(801) > 0) {
            AddEnhancedModeTextAction(
                sequence,
                17,
                new[] {
                    new MessageDescriptor("There's #so much lava# in here..."),
                    new MessageDescriptor("I think it would be nicer with #less lava#."),
                    new MessageDescriptor($"Anyway, good luck on #door warp#! I bet {pickupHint} you get it {tryText} try!"),
                }
            );
        } else {
            AddEnhancedModeTextAction(
                sequence,
                17,
                new[] {
                    new MessageDescriptor("Did you know that this place used to be #filled with lava#?"),
                    new MessageDescriptor("I think it's much nicer like this. You can explore #Mount Horu# however you like!"),
                    new MessageDescriptor($"Anyway, good luck on #door warp#! I bet {pickupHint} you get it {tryText} try!"),
                }
            );
        }
    }

    public static void BootstrapWarmthElementText(SceneRoot sceneRoot) {
        var sequence = sceneRoot.transform.FindChild("*resurrection/resurrectionSequence/*activateSequence").GetComponent<ActionSequence>();
        AddEnhancedModeTextAction(
            sequence,
            2,
            new[] {
                new MessageDescriptor("Ori, it looks like it's all going to come down to execution on the final escape!", EmotionType.Urgent, null),
                new MessageDescriptor("Good luck!"),
            }
        );
    }

    public static Dictionary<string, Action<SceneRoot>> TextBootstrapScenes = new Dictionary<string, Action<SceneRoot>> {
        { "sunkenGladesOriRoom", BootstrapSeinRoomText },
        { "sunkenGladesIntroSplitA", BootstrapSunkenGladesKeystoneDoorText },
        { "sunkenGladesSpiritCavernSaveRoomB", BootstrapGladesMapText },
        { "sunkenGladesSpiritCavernWalljumpB", BootstrapWallJumpTreeText },
        { "spiritTreeRefined", BootstrapSpiritTreeText },
        { "worldMapSpiritTree", BootstrapSpiritTreeMapText },
        { "upperGladesBelowSpiritTree", BootstrapChargeFlameTreeText },
        { "ginsoEntranceSketch", BootstrapGinsoDoorText },
        { "moonGrottoDoubleJump", BootstrapDoubleJumpTreeText },
        { "ginsoEntranceIntro", BootstrapGinsoEntranceText },
        { "ginsoTreeBashRedirectArt", BootstrapBashTreeText },
        { "ginsoTreeResurrection", BootstrapWaterElementText },
        { "thornfeltSwampStompAbility", BootstrapStompTreeText },
        { "valleyOfTheWindBackground", BootstrapValleyMainText },
        { "mistyWoodsRopeBridge", BootstrapMistyTorchText },
        { "mistyWoodsGetClimb", BootstrapClimbTreeText },
        { "sorrowPassForestB", BootstrapGumonSealText },
        { "forlornRuinsEntrancePlaceholder", BootstrapForlornDoorText },
        { "forlornRuinsGetNightberry", BootstrapForlornEntranceText },
        { "forlornRuinsRotatingLaserFlipped", BootstrapForlornLaserRoomText },
        { "forlornRuinsResurrection", BootstrapWindElementText },
        { "valleyOfTheWindGetChargeJump", BootstrapChargeJumpTreeText },
        { "valleyOfTheWindTop", BootstrapSunstoneText },
        { "sunkenGladesIntroSplitB", BootstrapBlackrootEntranceText },
        { "northMangroveFallsLanternIntro", BootstrapBlackrootLanternText },
        { "northMangroveFallsGetDash", BootstrapDashTreeText },
        { "mangroveFallsDashEscalation", BootstrapBlackrootTeleporterText },
        { "mangroveFallsGetGrenade", BootstrapGrenadeTreeText },
        { "southMangroveFallsStoryRoomA", BootstrapLostGroveCutsceneText },
        { "horuFieldsEntranceFrontalFlipped", BootstrapHoruDoorText },
        { "mountHoruHubMid", BootstrapHoruEntranceText },
        { "catAndMouseResurrectionRoom", BootstrapWarmthElementText },
    };

    public static Dictionary<string, List<string>> WaterBootstrapScenes = new Dictionary<string, List<string>> {
        {
            "thornfeltSwampActTwoStart", new List<string> {
                "setups/waterfall",
                "art/center/waterfall",
                "art/center/waterfallGroup",
                "art/center/waterplants",
                "art/center/fire",
                "art/center/masks",
                "art/backgroundFar/waterplants",
                "art/backgroundMid/fire",
                "art/backgroundMid/flowers",
                "art/backgroundMid/lights",
                "art/backgroundMid/waterfall",
                "art/backgroundMid/waterplants",
                "art/backgroundNear/lights",
                "art/backgroundNear/waterfall",
                "art/backgroundNear/waterplants",
                "art/foregroundClose/flowers",
                "art/foregroundClose/waterplants",
                "art/foregroundFar/waterplants",
                "bubbles",
                "water",
                "earlyZParent",
                "kuroMomentRaineffectA",
                "particles",
                "soundSpots",
            }
        }, {
            "thornfeltSwampE", new List<string> {
                "art/backgroundNear/flowers",
                "art/backgroundNear/lights",
                "art/backgroundNear/waterfall",
                "art/center/lights",
                "art/center/waterfall",
                "art/foregroundClose/lights",
                "art/foregroundClose/waterfall",
                "art/foregroundFar/waterfall",
                "water",
            }
        }, {
            "thornfeltSwampMoonGrottoTransition", new List<string> {
                "*releaseWaterSequence/timelineSequence",
                "*unsorted",
                "art/backgroundNear/effects",
                "art/backgroundNear/fog",
                "art/backgroundNear/flowers",
                "water",
            }
        }, {
            "thornfeltSwampStompAbility", new List<string> {
                "waterGroupA",
                "earlyZParent",
                "art/backgroundNear/fog",
                "art/backgroundNear/lights",
                "art/backgroundNear/textures",
                "art/foregroundClose/lights",
                "art/center/effects",
                "art/center/textures",
            }
        }, {
            "thornfeltSwampA", new List<string> {
                "water",
                "soundSpots/drippingWaterMedReverbSoundSpot",
                "soundSpots/drippingWaterSmallSoundSpot",
                "soundSpots/drippingWaterMedSoundSpot",
                "soundSpots/drippingWaterBigSoundSpot",
                "soundSpots/drippingBackgroundWaterSoundSpot",
                "art/backgroundNear/waterplants",
            }
        }, {
            "thornfeltSwampB", new List<string> {
                "water",
            }
        }, {
            "sunkenGladesIntroSplitA", new List<string> {
                "water",
                "soundSpots",
            }
        }, {
            "sunkenGladesSpiritCavernSaveRoomB", new List<string> {
                "water",
                "art/center/fog",
                "art/center/masks",
            }
        }, {
            "sunkenGladesRunning", new List<string> {
                "water",
                "soundSpots",
                "art/backgroundNear/textures",
                "art/foregroundClose/textures",
            }
        }, {
            "moonGrottoDoubleJumpIntroductionArt", new List<string> {
                "uberWater",
                "soundSpots",
                "art/center/textures",
                "art/center/effects",
            }
        }, {
            "moonGrottoDoubleJump", new List<string> {
                "water",
                "soundSpots",
                "art/backgroundNear/mushrooms",
                "art/backgroundNear/fog",
                "art/center/mushrooms",
                "art/foregroundClose/mushrooms",
            }
        }, {
            "moonGrottoLaserPuzzleB", new List<string> {
                "water",
                "waterfall",
                "soundSpots",
                "earlyZParent",
            }
        }, {
            "moonGrottoBackgroundScene", new List<string> {
                "waterfall",
                "diseasedWaterA",
                "soundSpots",
                "earlyZParent",
                "art/backgroundMid/waterfall",
                "art/waterfall",
            }
        }, {
            "moonGrottoLaserPuzzle", new List<string> {
                "soundSpots",
                "art/backgroundMid/textures",
                "particles/particleRainSplash",
                "particles/particleWaterDrops",
            }
        }, {
            "moonGrottoAdvancedDoubleJump", new List<string> {
                "soundSpots",
                "particles/particleRainSplash",
                "particles/particleWaterDrops",
            }
        }, {
            "moonGrottoGumosHideoutB", new List<string> {
                "water",
                "soundSpots",
                "art/backgroundNear/waterfall",
                "particles/particleRainSplash",
                "particles/particleWaterDrops",
            }
        }, {
            "moonGrottoRopeBridge", new List<string> {
                "particles/particleWaterDrops",
                "water",
                "art/backgroundMid/waterfall",
                "art/foregroundClose/effects",
            }
        }, {
            "moonGrottoMovingPlatformVertical", new List<string> {
                "soundSpots",
                "particles/particleRainSplash",
                "particles/particleWaterDrops",
                "art/backgroundMid/textures",
                "art/backgroundMid/waterfall",
                "art/backgroundNear/textures",
            }
        }, {
            "moonGrottoShortcutA", new List<string> {
                "waterGroupA",
            }
        }, {
            "moonGrottoStomperIntroduction", new List<string> {
                "art/center/effects",
                "particles/particleRainSplash",
                "particles/particleWaterDrops",
                "soundSpots",
                "water",
            }
        }, {
            "moonGrottoSecretAreaA", new List<string> {
                "art/center/masks",
                "water",
            }
        }, {
            "moonGrottoBasin", new List<string> {
                "art/backgroundNear/fog",
                "art/backgroundNear/mushrooms/moonGrottoWaterMushroomsA",
                "art/waterfall",
                "particles/particleRainSplash",
                "water",
            }
        }, {
            "westGladesOverworldForestA", new List<string> {
                "waterGroupA",
                "art/center/lights",
            }
        }, {
            "westGladesWaterStomp", new List<string> {
                "water",
                "soundSpots",
                "particles/bubbles",
                "art/backgroundNear/clouds",
                "art/center/clouds",
            }
        }, {
            "westGladesBashCave", new List<string> {
                "art/backgroundNear/fog",
                "art/backgroundMid/fog",
                "art/center/fog",
                "water",
                "soundSpots",
            }
        }, {
            "forlornRuinsKuroHideStreamlined", new List<string> {
                "water",
                "earlyZParent/earyZMesh0",
                "soundSpots",
                "art/backgroundMid/props/forlornRuinsIceBridgeA",
            }
        }, {
            "upperGladesSpiderIntroduction", new List<string> {
                "water",
                "soundSpots",
                "earlyZParent",
                "art/backgroundMid/fog",
                "art/backgroundMid/waterplants",
                "art/backgroundNear/fog",
                "art/backgroundNear/waterplants",
            }
        }, {
            "upperGladesSpiderCavernPuzzle", new List<string> {
                "soundSpots",
            }
        }, {
            "upperGladesHollowTreeSplitA", new List<string> {
                "waterZone",
            }
        }, {
            "upperGladesHollowTreeBackground", new List<string> {
                "uberWater",
            }
        }, {
            "southMangroveFallsGrenadeEscalationB", new List<string> {
                "art/backgroundMid/plants",
                "art/backgroundNear/plants/swallowsNestPlantWaterLily",
                "art/backgroundNear/textures",
                "art/center/lights",
                "art/foregroundClose/textures",
                "art/foregroundFar/textures",
                "water",
            }
        }, {
            "southMangroveFallsBackgroundB", new List<string> {
                "water",
            }
        }, {
            "southMangroveFallsGrenadeEscalationBR", new List<string> {
                "art/backgroundNear/textures",
                "art/foregroundClose/effects",
                "art/foregroundClose/textures",
                "art/foregroundFar/effects",
                "water",
            }
        }, {
            "catAndMouseMid", new List<string> {
                "uberWater",
                "waterzone",
                "art/backgroundMid/masks",
                "art/backgroundNear/fog",
                "art/backgroundNear/masks",
                "art/backgroundNear/waterplants",
                "art/center/masks",
                "earlyZParent",
            }
        },
    };
}

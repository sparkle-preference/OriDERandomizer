using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

public class RandomizerEnhancedMode
{
	private static void AddEnhancedModeTextAction(ActionSequence sequence, int index, MessageDescriptor[] messages)
	{
		GameObject obj = new GameObject("textAction");
		obj.transform.parent = sequence.transform;

		ShowEnhancedSpiritFlameTextAction textAction = obj.AddComponent<ShowEnhancedSpiritFlameTextAction>();
		textAction.Messages = messages;
		textAction.FreezeGame = false;

		sequence.Actions[index] = textAction;
		(sequence.Actions[index+1] as WaitAction).LastAction = textAction;
	}

    public static void BootstrapSceneWater(SceneRoot sceneRoot)
    {
        List<string> objectsToDeactivate = RandomizerEnhancedMode.WaterBootstrapScenes[sceneRoot.name];

        GameObject conditionObj = new GameObject("enhancedWaterLogic");
        conditionObj.transform.parent = sceneRoot.transform;

        RandomizerEnhancedCleanWaterCondition condition = conditionObj.AddComponent<RandomizerEnhancedCleanWaterCondition>();

        foreach (string path in objectsToDeactivate)
        {
            Transform transform = sceneRoot.transform.FindChild(path);

            if (transform != null)
            {
                ActivateBasedOnCondition activate = conditionObj.AddComponent<ActivateBasedOnCondition>();
                activate.Target = transform.gameObject;
                activate.Activate = false;
                activate.Condition = condition;
            }
        }
    }

    private static string GetItemHintForPickup(string pickupName)
    {
        RandomizerAction pickupAction = RandomizerLocationManager.LocationsByName[pickupName].Pickup;

        if (pickupAction == null)
        {
            return "nothing";
        }

        if (pickupAction.Action == "SK")
        {
            return "$a skill$";
        }
        else if (pickupAction.Action == "EV")
        {
            int eventPickup = (int)pickupAction.Value;
            if (eventPickup == 0 || eventPickup == 2 || eventPickup == 4)
            {
                return "#a dungeon key#";
            }
            else if (eventPickup == 1 || eventPickup == 3 || eventPickup == 5)
            {
                return "#an event#";
            }
        }
        else if (pickupAction.Action == "RB")
        {
            int bonus = (int)pickupAction.Value;
            if (bonus == 17 || bonus == 19 || bonus == 21)
            {
                return "#a shard#";
            }
            else if (bonus == 28)
            {
                return "#a fragment#";
            }
            else if (bonus >= 300 && bonus <= 311)
            {
                return "#a keystone#";
            }
        }
        else if (pickupAction.Action == "TP")
        {
            return "#a teleporter#";
        }
        else if (pickupAction.Action == "HC" || pickupAction.Action == "EC" || pickupAction.Action == "AC")
        {
            return "a cell";
        }
        else if (pickupAction.Action == "KS")
        {
            return "a keystone";
        }
        else if (pickupAction.Action == "MS")
        {
            return "a mapstone";
        }
        else if (pickupAction.Action == "EX")
        {
            return "some experience";
        }

        return "some item";
    }

	public static void BootstrapSeinRoomText(SceneRoot sceneRoot)
	{
        ActionSequence seinSequence = sceneRoot.transform.FindChild("*setups/*story/findingOri/seinInterestZone/trigger/activateSequence").GetComponent<ActionSequence>();
		RandomizerEnhancedMode.AddEnhancedModeTextAction(seinSequence, 27, new MessageDescriptor[3] {
			new MessageDescriptor("My voice... is returning...\nAt last, I can speak once more, after 8 years of silence."),
			new MessageDescriptor("I am #Sein#, the light and eyes of the randomizer developers. I will be happy to waste your time during your journey."),
			new MessageDescriptor("But if you really can't stand my presence, then you can silence me once more by pressing #Shift+Alt+U#.")
		});

        ActionSequence postFightSequence = sceneRoot.transform.FindChild("*setups/*story/allEnemiesKilled/group/actionSequence").GetComponent<ActionSequence>();
        RandomizerEnhancedMode.AddEnhancedModeTextAction(postFightSequence, 10, new MessageDescriptor[1] {
            new MessageDescriptor("These poor creatures...they could have lived, if you had just used the randomizer's Warp feature.", EmotionType.Sad, null)
        });
	}

	public static void BootstrapSunkenGladesKeystoneDoorText(SceneRoot sceneRoot)
	{
        ActionSequence sequence = sceneRoot.transform.FindChild("*allEnemiesKilled/activated/*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
		RandomizerEnhancedMode.AddEnhancedModeTextAction(sequence, 7, new MessageDescriptor[2] {
			new MessageDescriptor("This is a #door#. A #door# is like a wall, except you can go through it."),
			new MessageDescriptor("...what are you looking at me like that for? Of course you can't go through a wall! Don't be silly.")
		});
	}

    public static void BootstrapGladesMapText(SceneRoot sceneRoot)
    {
        ActionSequence sequence = sceneRoot.transform.FindChild("*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction").GetComponent<ActionSequence>();
        
        if (Randomizer.ForceMaps)
        {
            Dictionary<string, int> mapsByBadZone = new Dictionary<string, int>{
                { "Blackroot", 0 },
                { "Misty", 0 },
                { "Forlorn", 0 },
                { "Horu", 0 },
            };

            foreach (RandomizerLocationManager.Location loc in RandomizerLocationManager.LocationsByName.Values)
            {
                if (mapsByBadZone.ContainsKey(loc.Zone) && loc.Pickup?.Action == "MS")
                {
                    mapsByBadZone[loc.Zone] = mapsByBadZone[loc.Zone] + 1;
                }
            }

            string hintText = "Don't worry, none of the mapstones are in awful places this time! ...probably.";

            string hintZone = "";
            int hintMapstones = 12;
            foreach (var pair in mapsByBadZone)
            {
                if (pair.Value > 0 && pair.Value < hintMapstones)
                {
                    hintZone = pair.Key;
                    hintMapstones = pair.Value;
                }
            }

            if (hintMapstones == 1)
            {
                hintText = $"Don't worry, only one of the mapstones is in {hintZone} this time!";
            }
            else if (hintMapstones < 12)
            {
                hintText = $"Don't worry, only {hintMapstones} of the mapstones are in {hintZone} this time!";
            }

            RandomizerEnhancedMode.AddEnhancedModeTextAction(sequence, 8, new MessageDescriptor[3] {
                new MessageDescriptor("Ori, this is a #Map. Or #Map Stone#? #Map Altar#? Honestly, I'm not even sure anymore."),
                new MessageDescriptor("Anyway, you're going to have to place mapstones in all 9 #Map Monuments# to finish your journey."),
                new MessageDescriptor(hintText)
            });
        }
        else
        {
            string pickupHint = "some item";
            int hintMap = 9;
            for (; hintMap > 0; --hintMap)
            {
                RandomizerAction mapPickup = RandomizerLocationManager.ProgressiveMapLocations[hintMap-1].Pickup;
                if (mapPickup == null)
                {
                    continue;
                }

                if (mapPickup.Action == "SK")
                {
                    pickupHint = "$a skill$";
                    break;
                }
                else if (mapPickup.Action == "EV")
                {
                    int eventPickup = (int)mapPickup.Value;
                    if (eventPickup == 0 || eventPickup == 2 || eventPickup == 4)
                    {
                        pickupHint = "#a dungeon key#";
                        break;
                    }
                    else if (eventPickup == 1 || eventPickup == 3 || eventPickup == 5)
                    {
                        pickupHint = "#an event#";
                        break;
                    }
                }
                else if (mapPickup.Action == "RB")
                {
                    int bonus = (int)mapPickup.Value;
                    if (bonus == 17 || bonus == 19 || bonus == 21)
                    {
                        pickupHint = "#a shard#";
                        break;
                    }
                    else if (bonus == 28)
                    {
                        pickupHint = "#a fragment#";
                        break;
                    }
                    else if (bonus >= 300 && bonus <= 311)
                    {
                        pickupHint = "#a keystone#";
                        break;
                    }
                }
                else if (mapPickup.Action == "TP")
                {
                    pickupHint = "#a teleporter#";
                    break;
                }
            }

            if (hintMap == 0)
            {
                RandomizerAction firstMapPickup = RandomizerLocationManager.ProgressiveMapLocations[0].Pickup;
                if (firstMapPickup.Action == "HC" || firstMapPickup.Action == "EC" || firstMapPickup.Action == "AC")
                {
                    pickupHint = "a cell";
                }
                else if (firstMapPickup.Action == "KS")
                {
                    pickupHint = "a keystone";
                }
                else if (firstMapPickup.Action == "MS")
                {
                    pickupHint = "a mapstone";
                }
                else if (firstMapPickup.Action == "EX")
                {
                    pickupHint = "some experience";
                }
            }

            string hintMapAsText = (hintMap == 0 || hintMap == 1) ? "one mapstone in a #Map Pedestal#" : $"{hintMap} mapstones in #Map Pedestals#";

            RandomizerEnhancedMode.AddEnhancedModeTextAction(sequence, 8, new MessageDescriptor[3] {
                new MessageDescriptor("Ori, this is a #Map#. Or #Map Stone#? #Map Altar#? Honestly, I'm not even sure anymore."),
                new MessageDescriptor("Anyway, you can receive items by placing mapstones in these #Map Monuments#!"),
                new MessageDescriptor($"For example, if you place {hintMapAsText}, you'll find {pickupHint}!")
            });
        }
    }

    public static void BootstrapWallJumpTreeText(SceneRoot sceneRoot)
    {
        ActionSequence sequence = sceneRoot.transform.FindChild("*storySetups/storyTextWithTrigger/action").GetComponent<ActionSequence>();
        RandomizerEnhancedMode.AddEnhancedModeTextAction(sequence, 7, new MessageDescriptor[4] {
            new MessageDescriptor("Ori."),
            new MessageDescriptor("Quaza."),
            new MessageDescriptor("Shaka."),
            new MessageDescriptor("...why are you looking at me like that?")
        });
    }

    public static void BootstrapSpiritTreeText(SceneRoot sceneRoot)
    {
        // "*spiritTreeStorySetup/container/actionSequences/02. kuroAttackActionSequence" - index 0 (NOTE: wait action is 3!!!)
        // "*spiritTreeStorySetup/container/actionSequences/03. worldMapActionSequence" - index 15
        // "*spiritTreeStorySetup/container/actionSequences/04. returnCameraToPlayerActionSequence" - index 5
    }

    public static void BootstrapSpiritTreeMapText(SceneRoot sceneRoot)
    {
        // "*setup/actionSequence" - indices 4, 10, 16, 22
    }

    public static void BootstrapChargeFlameTreeText(SceneRoot sceneRoot)
    {
        // "*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction" - index 8
    }

    public static void BootstrapGinsoDoorText(SceneRoot sceneRoot)
    {
        string itemHint = RandomizerEnhancedMode.GetItemHintForPickup("WaterVein");
        ActionSequence sequence = sceneRoot.transform.FindChild("*setups/stealingSetup/setupA/action").GetComponent<ActionSequence>();
        RandomizerEnhancedMode.AddEnhancedModeTextAction(sequence, 11, new MessageDescriptor[1] {
            new MessageDescriptor($"A #Gumon# from the #Forlorn Ruins#! He's running away with {itemHint}! Quickly, after him!")
        });
    }

    public static void BootstrapDoubleJumpTreeText(SceneRoot sceneRoot)
    {
        // "*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction" - index 8
    }

    public static void BootstrapGinsoEntranceText(SceneRoot sceneRoot)
    {
        // "*setup/*enterScene/action" - index 15
    }

    public static void BootstrapBashTreeText(SceneRoot sceneRoot)
    {
        // "*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction" - index 8
    }

    public static void BootstrapWaterElementText(SceneRoot sceneRoot)
    {
        // "*heartResurrection/seeHeartSetup/seeHeart" - indices 6, 11
        // "*heartResurrection/restoringHeartWaterRising/waterSequenceStartAction" - index 3
    }

    public static void BootstrapStompTreeText(SceneRoot sceneRoot)
    {
        // "*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction" - index 8
    }

    public static void BootstrapValleyMainText(SceneRoot sceneRoot)
    {
        // "*getFeatherSetupContainer/*kuroCliffLowerHint/*action" - indices 9, 16
        // "*getFeatherSetupContainer/*kuroCliffHigherHint/objectiveSetupTrigger/objectiveSetupAction" - index 10
    }

    public static void BootstrapMistyTorchText(SceneRoot sceneRoot)
    {
        // "*storySetups/storyTextWithTrigger/action" - index 8
    }

    public static void BootstrapClimbTreeText(SceneRoot sceneRoot)
    {
        // "*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction" - index 8
    }

    public static void BootstrapGumonSealText(SceneRoot sceneRoot)
    {
        // "*storySetup/*liftFogSequence/liftFogAction" - index 23
    }

    public static void BootstrapForlornDoorText(SceneRoot sceneRoot)
    {
        // "*setups/openingForlornRuins/oriInterestArea/failedAction" - index 7
    }

    public static void BootstrapForlornEntranceText(SceneRoot sceneRoot)
    {
        // "*enterRuins/enterScene" - indices 11, 15, 19
        // "*storySetups/storyTextWithTrigger/action" - index 7
    }

    public static void BootstrapForlornLaserRoomText(SceneRoot sceneRoot)
    {
        // "*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction" - index 7
    }

    public static void BootstrapWindElementText(SceneRoot sceneRoot)
    {
        // "*story/resurrectionSequence/*activateSequence" - indices 7, 12
    }

    public static void BootstrapChargeJumpTreeText(SceneRoot sceneRoot)
    {
        // "*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction" - index 8
    }

    public static void BootstrapSunstoneText(SceneRoot sceneRoot)
    {
        // "*sunStoneSetup/*storySetups/storyTextWithTrigger/action" - index 7
    }

    public static void BootstrapBlackrootEntranceText(SceneRoot sceneRoot)
    {
        // "*naruStatueHintSetup/objectiveSetupTrigger/objectiveSetupAction" - index 5
    }

    public static void BootstrapBlackrootLanternText(SceneRoot sceneRoot)
    {
        // "*naruStorySetupA/objectiveSetupTrigger/objectiveSetupAction" - index 7
    }

    public static void BootstrapDashTreeText(SceneRoot sceneRoot)
    {
        // "*liftDarknessSetup/*pedestalSetup/actions/mainAction" - index 21
        // "*enabledByDarknessLifted/*naruStorySetupA/objectiveSetupAction" - index 10
    }

    public static void BootstrapBlackrootTeleporterText(SceneRoot sceneRoot)
    {
        // "*objectiveSetup/container/objectiveSetupTrigger/objectiveSetupAction" - index 23
    }

    public static void BootstrapGrenadeTreeText(SceneRoot sceneRoot)
    {
        // "*objectiveSetup/objectiveSetupTrigger/objectiveSetupAction" - index 8
    }

    public static void BootstrapLostGroveCutsceneText(SceneRoot sceneRoot)
    {
        // "*setup/objectiveSetupAction" - index 39
    }

    public static void BootstrapHoruDoorText(SceneRoot sceneRoot)
    {
        // "*storySetup(noKey)/objectiveSetupTrigger/objectiveSetupAction" - index 7
        // "*setups/openingMountHoru/oriInterestArea/*activateSequence" - index 10
    }

    public static void BootstrapHoruEntranceText(SceneRoot sceneRoot)
    {
        // "*enterScene/action" - index 17
    }

    public static void BootstrapWarmthElementText(SceneRoot sceneRoot)
    {
        // "*resurrection/resurrectionSequence/*activateSequence" - index 2
    }

    public static Dictionary<string, Action<SceneRoot>> TextBootstrapScenes = new Dictionary<string, Action<SceneRoot>>
    {
        { "sunkenGladesOriRoom", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapSeinRoomText) },
        { "sunkenGladesIntroSplitA", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapSunkenGladesKeystoneDoorText) },
        { "sunkenGladesSpiritCavernSaveRoomB", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapGladesMapText) },
        { "sunkenGladesSpiritCavernWalljumpB", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapWallJumpTreeText) },
        { "spiritTreeRefined", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapSpiritTreeText) },
        { "worldMapSpiritTree", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapSpiritTreeMapText) },
        { "upperGladesBelowSpiritTree", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapChargeFlameTreeText) },
        { "ginsoEntranceSketch", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapGinsoDoorText) },
        { "moonGrottoDoubleJump", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapDoubleJumpTreeText) },
        { "ginsoEntranceIntro", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapGinsoEntranceText) },
        { "ginsoTreeBashRedirectArt", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapBashTreeText) },
        { "ginsoTreeResurrection", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapWaterElementText) },
        { "thornfeltSwampStompAbility", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapStompTreeText) },
        { "valleyOfTheWindBackground", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapValleyMainText) },
        { "mistyWoodsRopeBridge", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapMistyTorchText) },
        { "mistyWoodsGetClimb", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapClimbTreeText) },
        { "sorrowPassForestB", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapGumonSealText) },
        { "forlornRuinsEntrancePlaceholder", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapForlornDoorText) },
        { "forlornRuinsGetNightberry", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapForlornEntranceText) },
        { "forlornRuinsRotatingLaserFlipped", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapForlornLaserRoomText) },
        { "forlornRuinsResurrection", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapWindElementText) },
        { "valleyOfTheWindGetChargeJump", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapChargeJumpTreeText) },
        { "valleyOfTheWindTop", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapSunstoneText) },
        { "sunkenGladesIntroSplitB", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapBlackrootEntranceText) },
        { "northMangroveFallsLanternIntro", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapBlackrootLanternText) },
        { "northMangroveFallsGetDash", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapDashTreeText) },
        { "mangroveFallsDashEscalation", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapBlackrootTeleporterText) },
        { "mangroveFallsGetGrenade", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapGrenadeTreeText) },
        { "southMangroveFallsStoryRoomA", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapLostGroveCutsceneText) },
        { "horuFieldsEntranceFrontalFlipped", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapHoruDoorText) },
        { "mountHoruHubMid", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapHoruEntranceText) },
        { "catAndMouseResurrectionRoom", new Action<SceneRoot>(RandomizerEnhancedMode.BootstrapWarmthElementText) },
    };

    public static Dictionary<string, List<string>> WaterBootstrapScenes = new Dictionary<string, List<string>>
    {
        { "thornfeltSwampActTwoStart", new List<string>
            {
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
                "soundSpots"
            } },
        { "thornfeltSwampE", new List<string>
            {
                "art/backgroundNear/flowers",
                "art/backgroundNear/lights",
                "art/backgroundNear/waterfall",
                "art/center/lights",
                "art/center/waterfall",
                "art/foregroundClose/lights",
                "art/foregroundClose/waterfall",
                "art/foregroundFar/waterfall",
                "water"
            } },
        { "thornfeltSwampMoonGrottoTransition", new List<string>
            {
                "water",
                "releaseWaterSequence/timelineSequence",
                "unsorted",
                "art/backgroundNear/effects",
                "art/backgroundNear/fog",
                "art/backgroundNear/flowers"
            } },
        { "thornfeltSwampStompAbility", new List<string>
            {
                "waterGroupA",
                "earlyZParent",
                "art/backgroundNear/fog",
                "art/backgroundNear/lights",
                "art/backgroundNear/textures",
                "art/foregroundClose/lights",
                "art/center/effects",
                "art/center/textures"
            } },
        { "thornfeltSwampA", new List<string>
            {
                "water",
                "soundSpots/drippingWaterMedReverbSoundSpot",
                "soundSpots/drippingWaterSmallSoundSpot",
                "soundSpots/drippingWaterMedSoundSpot",
                "soundSpots/drippingWaterBigSoundSpot",
                "soundSpots/drippingBackgroundWaterSoundSpot",
                "art/backgroundNear/waterplants"
            } },
        { "thornfeltSwampB", new List<string>
            {
                "water"
            } },
        { "sunkenGladesIntroSplitA", new List<string>
            {
                "water",
                "soundSpots"
            } },
        { "sunkenGladesSpiritCavernSaveRoomB", new List<string>
            {
                "water",
                "art/center/fog",
                "art/center/masks"
            } },
        { "sunkenGladesRunning", new List<string>
            {
                "water",
                "soundSpots",
                "art/backgroundNear/textures",
                "art/foregroundClose/textures"
            } },
        { "moonGrottoDoubleJumpIntroductionArt", new List<string>
            {
                "uberWater",
                "soundSpots",
                "art/center/textures",
                "art/center/effects"
            } },
        { "moonGrottoDoubleJump", new List<string>
            {
                "water",
                "soundSpots",
                "art/backgroundNear/mushrooms",
                "art/backgroundNear/fog",
                "art/center/mushrooms",
                "art/foregroundClose/mushrooms"
            } },
        { "moonGrottoLaserPuzzleB", new List<string>
            {
                "water",
                "waterfall",
                "soundSpots",
                "earlyZParent"
            } },
        { "moonGrottoBackgroundScene", new List<string>
            {
                "waterfall",
                "diseasedWaterA",
                "soundSpots",
                "earlyZParent",
                "art/backgroundMid/waterfall",
                "art/waterfall"
            } },
        { "moonGrottoLaserPuzzle", new List<string>
            {
                "soundSpots",
                "art/backgroundMid/textures",
                "particles/particleRainSplash",
                "particles/particleWaterDrops"
            } },
        { "moonGrottoAdvancedDoubleJump", new List<string>
            {
                "soundSpots",
                "particles/particleRainSplash",
                "particles/particleWaterDrops"
            } },
        { "moonGrottoGumosHideoutB", new List<string>
            {
                "water",
                "soundSpots",
                "art/backgroundNear/waterfall",
                "particles/particleRainSplash",
                "particles/particleWaterDrops"
            } },
        { "moonGrottoRopeBridge", new List<string>
            {
                "particles/particleWaterDrops",
                "water",
                "art/backgroundMid/waterfall",
                "art/foregroundClose/effects"
            } },
        { "moonGrottoMovingPlatformVertical", new List<string>
            {
                "soundSpots",
                "particles/particleRainSplash",
                "particles/particleWaterDrops",
                "art/backgroundMid/textures",
                "art/backgroundMid/waterfall",
                "art/backgroundNear/textures"
            } },
        { "moonGrottoShortcutA", new List<string>
            {
                "waterGroupA",
            } },
        { "moonGrottoStomperIntroduction", new List<string>
            {
                "water",
                "particles/particleRainSplash",
                "particles/particleWaterDrops",
                "soundSpots"
            } },
        { "moonGrottoSecretAreaA", new List<string>
            {
                "art/center/masks",
                "water"
            } },
        { "moonGrottoBasin", new List<string>
            {
                "art/backgroundNear/fog",
                "art/backgroundNear/mushrooms/moonGrottoWaterMushroomsA",
                "art/waterfall",
                "particles/particleRainSplash",
                "water"
            } },
        { "westGladesOverworldForestA", new List<string>
            {
                "waterGroupA",
                "art/center/lights"
            } },
        { "westGladesWaterStomp", new List<string>
            {
                "water",
                "soundSpots",
                "particles/bubbles",
                "art/backgroundNear/clouds",
                "art/center/clouds"
            } },
        { "westGladesBashCave", new List<string>
            {
                "art/backgroundNear/fog",
                "art/backgroundMid/fog",
                "art/center/fog",
                "water",
                "soundSpots"
            } },
        { "forlornRuinsKuroHideStreamlined", new List<string>
            {
                "water",
                "earlyZParent/earyZMesh0",
                "soundSpots",
                "art/backgroundMid/props/forlornRuinsIceBridgeA"
            } },
        { "upperGladesSpiderIntroduction", new List<string>
            {
                "water",
                "soundSpots",
                "earlyZParent",
                "art/backgroundMid/fog",
                "art/backgroundMid/waterplants",
                "art/backgroundNear/fog",
                "art/backgroundNear/waterplants"
            } },
        { "upperGladesSpiderCavernPuzzle", new List<string>
            {
                "soundSpots"
            } },
        {"upperGladesHollowTreeBackground", new List<string>
            {
                "uberWater"
            } },
        { "southMangroveFallsGrenadeEscalationB", new List<string>
            {
                "art/backgroundMid/plants",
                "art/backgroundNear/plants/swallowsNestPlantWaterLily",
                "art/backgroundNear/textures",
                "art/center/lights",
                "art/foregroundClose/textures",
                "art/foregroundFar/textures",
                "water"
            } },
        { "southMangroveFallsBackgroundB", new List<string>
            {
                "water"
            } },
        { "southMangroveFallsGrenadeEscalationBR", new List<string>
            {
                "art/backgroundNear/textures",
                "art/foregroundClose/effects",
                "art/foregroundClose/textures",
                "art/foregroundFar/effects",
                "water"
            } },
        { "catAndMouseMid", new List<string>
            {
                "uberWater",
                "waterzone",
                "art/backgroundMid/masks",
                "art/backgroundNear/fog",
                "art/backgroundNear/masks",
                "art/backgroundNear/waterplants",
                "art/center/masks",
                "earlyZParent"
            } }
    };
}
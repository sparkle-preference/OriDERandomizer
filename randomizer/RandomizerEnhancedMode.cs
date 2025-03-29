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

	public static void BootstrapSeinRoomText(SceneRoot sceneRoot, ActionSequence seinSequence)
	{
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
            // oh this is going to be miserable but I soooooo wanna do it
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

    public static void BootstrapWallJumpTreeText(SceneRoot sceneRoot, ActionSequence sequence)
    {
        RandomizerEnhancedMode.AddEnhancedModeTextAction(sequence, 7, new MessageDescriptor[4] {
            new MessageDescriptor("Ori."),
            new MessageDescriptor("Quaza."),
            new MessageDescriptor("Shaka."),
            new MessageDescriptor("...why are you looking at me like that?")
        });
    }
}
using System;
using System.Linq;
using Game;
using Sein.World;
using UnityEngine;
using Events = Sein.World.Events;

public static class RandomizerSwitch {
    public static void SkillPointPickup() {
        PickupMessage(RandomizerItems.Message("AC", ""));
        if (Randomizer.ZeroXP) {
            return;
        }

        Characters.Sein.Level.GainSkillPoint();
        Characters.Sein.Inventory.SkillPointsCollected++;
    }

    public static void MaxEnergyContainerPickup() {
        PickupMessage(RandomizerItems.Message("EC", ""));
        if (Characters.Sein.Energy.Max == 0f) {
            Characters.Sein.SoulFlame.FillSoulFlameBar();
        }

        Characters.Sein.Energy.Max += 1.0f;
        if (Characters.Sein.Energy.Current < Characters.Sein.Energy.Max) {
            Characters.Sein.Energy.Current = Characters.Sein.Energy.Max;
        }
    }

    public static void ExpOrbPickup(int Value, int coords) {
        PickupMessage(Value + " " + RandomizerExpNames.ExpName(coords));
        if (Randomizer.ZeroXP) {
            return;
        }

        var earned = RandomizerStatsManager.PickupZone != "offworld";
        Characters.Sein.Level.GainExperience(RandomizerBonus.ExpWithBonuses(Value, earned));
    }

    public static void KeystonePickup() {
        PickupMessage(RandomizerItems.Message("KS", ""));
        Characters.Sein.Inventory.CollectKeystones(1);
        Characters.Sein.Inventory.IncRandomizerItem(70, 1);
    }

    public static void MaxHealthContainerPickup() {
        PickupMessage(RandomizerItems.Message("HC", ""));
        Characters.Sein.Mortality.Health.GainMaxHeartContainer();
    }

    public static void MapStonePickup() {
        PickupMessage(RandomizerItems.Message("MS", ""));
        Characters.Sein.Inventory.MapStones++;
        Characters.Sein.Inventory.IncRandomizerItem(71, 1);
    }

    public static void AbilityPickup(int Ability) {
        switch (Ability) {
            case 0:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(414);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "0"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.Bash, true);
                break;
            case 2:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(412);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "2"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.ChargeFlame, true);
                break;
            case 3:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(411);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "3"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.WallJump, true);
                break;
            case 4:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(415);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "4"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.Stomp, true);
                break;
            case 5:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(413);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "5"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.DoubleJump, true);
                break;
            case 8:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(418);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "8"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.ChargeJump, true);
                break;
            case 12:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(417);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "12"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.Climb, true);
                break;
            case 14:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(416);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "14"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.Glide, true);
                break;
            case 15:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(410);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "15"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.SpiritFlame, true);
                break;
            case 50:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(419);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "50"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.Dash, true);
                break;
            case 51:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(420);
                } else {
                    PickupMessage(RandomizerItems.Message("SK", "51"), 300);
                }

                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.Grenade, true);
                break;
        }

        RandomizerStatsManager.FoundSkill(Ability);
    }

    public static void EventPickup(int Value) {
        switch (Value) {
            case 0:
                PickupMessage(RandomizerItems.Message("EV", "0"), 300);
                Keys.GinsoTree = true;
                break;
            case 1:
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(422);
                } else {
                    PickupMessage(RandomizerItems.Message("EV", "1"), 300);
                }

                Events.WaterPurified = true;
                break;
            case 2:
                PickupMessage(RandomizerItems.Message("EV", "2"), 300);
                Keys.ForlornRuins = true;
                break;
            case 3:
                PickupMessage(RandomizerItems.Message("EV", "3"), 300);
                Events.WindRestored = true;
                break;
            case 4:
                PickupMessage(RandomizerItems.Message("EV", "4"), 300);
                Keys.MountHoru = true;
                break;
            case 5:
                PickupMessage(RandomizerItems.Message("EV", "5"), 300);
                Events.WarmthReturned = true;
                break;
        }

        RandomizerStatsManager.FoundEvent(Value);
    }

    public static void TeleportPickup(string Value) {
        var shardCount = -1;
        var colorChar = ' ';
        var shardPart = "";
        var dungeonAbbr = "";
        if (Value == "Ginso") {
            Characters.Sein.Inventory.SetRandomizerItem(1024, 1);
            shardCount = RandomizerBonus.WaterVeinShards();
            shardPart = "Water Vein";
            dungeonAbbr = "WV";
            colorChar = '*';
        }

        if (Value == "Forlorn") {
            Characters.Sein.Inventory.SetRandomizerItem(1025, 1);
            shardCount = RandomizerBonus.GumonSealShards();
            shardPart = "Gumon Seal";
            dungeonAbbr = "GS";
            colorChar = '#';
        }

        if (Value == "Horu") {
            Characters.Sein.Inventory.SetRandomizerItem(1026, 1);
            shardCount = RandomizerBonus.SunstoneShards();
            shardPart = "Sunstone";
            dungeonAbbr = "SS";
            colorChar = '@';
        }

        if (Randomizer.Shards && shardCount >= 0 && shardCount < 2) {
            if (shardCount == 1) {
                shardPart = "1 more " + shardPart + " shard to activate";
            } else {
                shardPart = "2 " + shardPart + " shards to activate";
            }

            PickupMessage(colorChar + "Broken " + Value + " teleporter\nCollect " + shardPart + colorChar, 300);
            return;
        }

        if (colorChar != ' ' && Randomizer.CluesMode && Randomizer.TeleportersLockedByClues && !RandomizerClues.IsClueActive(dungeonAbbr)) {
            PickupMessage($"{colorChar}Broken {Value} teleporter\nGet the {shardPart} clue to activate{colorChar}", 300);
            return;
        }

        TeleporterController.Activate(Randomizer.TeleportTable[Value].ToString(), false);
        PickupMessage(RandomizerItems.Message("TP", Value));
    }

    public static void GivePickup(RandomizerAction action, int coords, bool found_locally = true) {
        // Reentrant: a nested grant keeps the outermost pickup's zone.
        var outerZone = RandomizerStatsManager.PickupZone;
        if (outerZone == null) {
            RandomizerStatsManager.PickupZone = RandomizerStatsManager.ZoneForPickup(coords);
        }

        try {
            switch (action.Action) {
                case "RP":
                case "MU":
                    foreach (var subpart in action.Decompose()) {
                        GivePickup(subpart, coords, false);
                    }

                    SilentMode = false;
                    break;
                case "AC":
                    if ((int)action.Value < 0) {
                        LoseAC();
                    } else {
                        SkillPointPickup();
                    }

                    break;
                case "EC":
                    if ((int)action.Value < 0) {
                        LoseEC();
                    } else {
                        MaxEnergyContainerPickup();
                    }

                    break;
                case "EX":
                    ExpOrbPickup((int)action.Value, coords);
                    break;
                case "KS":
                    if ((int)action.Value < 0) {
                        LoseKS();
                    } else {
                        KeystonePickup();
                    }

                    break;
                case "HC":
                    if ((int)action.Value < 0) {
                        LoseHC();
                    } else {
                        MaxHealthContainerPickup();
                    }

                    break;
                case "MS":
                    if ((int)action.Value < 0) {
                        LoseMS();
                    } else {
                        MapStonePickup();
                    }

                    break;
                case "SK":
                    AbilityPickup((int)action.Value);
                    break;
                case "EV":
                    EventPickup((int)action.Value);
                    break;
                case "RB":
                    RandomizerBonus.UpgradeID((int)action.Value, coords);
                    break;
                case "TP":
                    TeleportPickup((string)action.Value);
                    break;
                case "SH":
                    var message = ((string)action.Value).Replace("AltR", RandomizerRebinding.ReturnToStart.FirstBindName());
                    if (message.Length > 1 && message[1] == '=') {
                        var parts = message.Split(',').ToList();
                        var flags = parts.FindAll(ele => ele.Length >= 2 && ele[1] == '=');
                        message = String.Join(",", parts.FindAll(ele => ele.Length < 2 || ele[1] != '=').ToArray());
                        var duration = 120;
                        foreach (var flag in flags) {
                            var p = flag.Split('=');
                            if (p.Length != 2) {
                                continue;
                            }

                            if (p[0] == "d") {
                                int.TryParse(p[1], out duration);
                            } else if (p[0] == "s") {
                                SilentMode = p[1].Trim().ToLower() == "true";
                            }
                        }

                        Randomizer.showHint(RandomizerUI.Message.PickupMessage(message, duration / 60f));
                    } else {
                        Randomizer.showHint(RandomizerUI.Message.PickupMessage(message));
                    }

                    break;
                case "WT":
                    RandomizerTrackedDataManager.SetRelic(Randomizer.RelicZoneLookup[(string)action.Value]);
                    var relics = Characters.Sein.Inventory.GetRandomizerItem(402);
                    var relicStr = "\n(" + relics + "/" + Randomizer.RelicCount + ")";
                    if (relics >= Randomizer.RelicCount) {
                        relicStr = "$" + relicStr + "$";
                    }

                    PickupMessage((string)action.Value + relicStr, 480);
                    break;
                case "WS":
                case "WP":
                    // Don't actually warp at spawn, let other code do that.
                    if (coords != 2) {
                        Randomizer.SaveAfterWarp = action.Action == "WS";
                        var xy = ((string)action.Value).Split(',');
                        if (xy.Length > 2 && xy[2] == "force") {
                            Randomizer.WarpTo(new Vector3(float.Parse(xy[0]), float.Parse(xy[1])), 15);
                        } else {
                            Randomizer.WarpTarget = new Vector3(float.Parse(xy[0]), float.Parse(xy[1]));
                            Randomizer.WarpSource = Characters.Sein.Position;
                            Randomizer.CanWarp = 7;
                        }
                    }

                    break;
                case "NO":
                    break;
                case "TW":
                    // TW entries are coord|TW|name,x,y
                    var pieces2 = ((string)action.Value).Split(',');
                    int.TryParse(pieces2[1], out var warpX);
                    int.TryParse(pieces2[2], out var warpY);
                    TeleporterController.AddCustomTeleporter(pieces2[0], warpX, warpY);
                    TeleporterController.Activate(pieces2[0]);
                    PickupMessage(pieces2[0]);
                    break;
                case "NB":
                    // NB entries are coord|NB|x,y
                    var pieces3 = ((string)action.Value).Split(',');
                    int.TryParse(pieces3[0], out var positionX);
                    int.TryParse(pieces3[1], out var positionY);
                    Randomizer.NightBerryWarpPosition = new Vector3(positionX, positionY);
                    Characters.Sein.Inventory.SetRandomizerItem(82, 1);
                    break;
                case "MW":
                    // MW entries are coord|MW|owner,slot,code,id -- another
                    // player's item. Nothing to grant locally: the
                    // found_locally send below tells the server, which flips
                    // the owner's slot bit and their client self-grants.
                    var mwPieces = ((string)action.Value).Split(new[] { ',' }, 3);
                    if (RandomizerMW.ApItems.TryGetValue(coords, out var apItem)) {
                        // Archipelago reserved location: the owner here is our
                        // own shadow player, so field 5 is the only thing that
                        // knows who is actually getting this
                        if (!RandomizerMW.IsSelf(apItem[0])) {
                            SentMwPickupMessage($"{RandomizerMW.ApName(apItem[0])}'s {RandomizerItems.ColorWrap(apItem[1])}");
                        }
                        // ours: grant it here so it reads like an in-seed find.
                        // The room still hands it back; that arrives to find the
                        // slot already granted and does nothing.
                        else {
                            RandomizerMW.GrantSelfItem(coords);
                        }
                    } else if (RandomizerItems.Inner((string)action.Value, Randomizer.PlayerCount, out var mwCode, out var mwId)) {
                        var playerName = int.TryParse(mwPieces[0], out var pid) ? RandomizerMW.PlayerName(pid) : $"Player {mwPieces[0]}";
                        var mwItem = RandomizerItems.Name(mwCode, mwId);
                        SentMwPickupMessage($"{playerName}'s {RandomizerItems.ColorWrap(mwItem)}");
                    } else {
                        SentMwPickupMessage("Unknown Foreign Item");
                    }

                    break;
            }

            BingoController.OnItem(action, coords);
            RandomizerTrackedDataManager.UpdateBitfields();
        } catch (Exception e) {
            Randomizer.LogError($"Give Pickup({action}, {coords}): {e.Message}");
        } finally {
            RandomizerStatsManager.PickupZone = outerZone;
        }

        if (found_locally && Randomizer.Sync) {
            RandomizerSyncManager.FoundPickup(action, coords);
        }

        if (found_locally) {
            Randomizer.OnCoord(coords);
        }
    }

    public static void LoseHC() {
        PickupMessage(RandomizerItems.Message("HC", "-1"));
        Characters.Sein.Mortality.Health.MaxHealth -= 4;
        if (Characters.Sein.Mortality.Health.Amount > Characters.Sein.Mortality.Health.MaxHealth) {
            Characters.Sein.Mortality.Health.Amount = Characters.Sein.Mortality.Health.MaxHealth;
        }
    }

    public static void LoseEC() {
        PickupMessage(RandomizerItems.Message("EC", "-1"));
        Characters.Sein.Energy.Max--;
        if (Characters.Sein.Energy.Current > Characters.Sein.Energy.Max) {
            Characters.Sein.Energy.Current = Characters.Sein.Energy.Max;
        }
    }

    public static void LoseAC() {
        PickupMessage(RandomizerItems.Message("AC", "-1"));
        Characters.Sein.Level.SkillPoints--;
    }

    public static void LoseMS() {
        PickupMessage(RandomizerItems.Message("MS", "-1"));
        Characters.Sein.Inventory.MapStones--;
        Characters.Sein.Inventory.IncRandomizerItem(71, -1);
    }

    public static void LoseKS() {
        PickupMessage(RandomizerItems.Message("KS", "-1"));
        Characters.Sein.Inventory.Keystones--;
        Characters.Sein.Inventory.IncRandomizerItem(70, -1);
    }


    public static bool SilentMode;

    // when set, appended to every pickup message; RandomizerMW uses it to
    // render multiworld grants as "[pickup] from Player N" in one line
    public static string MessageSuffix = null;

    public static void PickupMessage(string text, int frames = 120) {
        if (MessageSuffix != null) {
            text += MessageSuffix;
        }

        if (SilentMode) {
            if (RandomizerSettings.Dev) {
                Randomizer.log(text + " (squelched)");
            }

            return;
        }

        Randomizer.showHint(RandomizerUI.Message.PickupMessage(text, frames / 60f));
    }

    public static void SentMwPickupMessage(string text, int frames = 120) {
        if (SilentMode) {
            if (RandomizerSettings.Dev) {
                Randomizer.log(text + " (squelched)");
            }

            return;
        }

        Randomizer.showHint(RandomizerUI.Message.MwPickupMessage(text, frames / 60f));
    }
}

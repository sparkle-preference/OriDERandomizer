using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core;
using Game;

public static class RandomizerStatsManager {
    public static void Initialize() {
        Offsets = new Dictionary<string, int>();
        Offsets.Add("sunkenGlades", 1);
        Offsets.Add("hollowGrove", 2);
        Offsets.Add("moonGrotto", 3);
        Offsets.Add("mangrove", 4);
        Offsets.Add("thornfeltSwamp", 5);
        Offsets.Add("ginsoTree", 6);
        Offsets.Add("valleyOfTheWind", 7);
        Offsets.Add("mistyWoods", 8);
        Offsets.Add("forlornRuins", 9);
        Offsets.Add("sorrowPass", 10);
        Offsets.Add("mountHoru", 11);
        Offsets.Add("unknown", 12);
        Offsets.Add("offworld", 13);
        Offsets.Add("total", 0);

        ZonePrettyNames = new Dictionary<string, string>();
        ZonePrettyNames.Add("sunkenGlades", "Glades");
        ZonePrettyNames.Add("hollowGrove", "Grove");
        ZonePrettyNames.Add("moonGrotto", "Grotto");
        ZonePrettyNames.Add("mangrove", "Blackroot");
        ZonePrettyNames.Add("thornfeltSwamp", "Swamp");
        ZonePrettyNames.Add("ginsoTree", "Ginso");
        ZonePrettyNames.Add("valleyOfTheWind", "Valley");
        ZonePrettyNames.Add("mistyWoods", "Misty");
        ZonePrettyNames.Add("forlornRuins", "Forlorn");
        ZonePrettyNames.Add("sorrowPass", "Sorrow");
        ZonePrettyNames.Add("mountHoru", "Horu\t");
        ZonePrettyNames.Add("unknown", "Misc\t");
        ZonePrettyNames.Add("offworld", "Offworld");
        ZonePrettyNames.Add("total", "Total\t");

        // Location.Zone speaks pretty names; Offsets speaks area identifiers
        ZoneKeysByPrettyName = new Dictionary<string, string>();
        foreach (var pair in ZonePrettyNames) {
            ZoneKeysByPrettyName[pair.Value.Trim()] = pair.Key;
        }

        PickupCounts = new Dictionary<string, int>();
        PickupCounts.Add("sunkenGlades", 28);
        PickupCounts.Add("hollowGrove", 27);
        PickupCounts.Add("moonGrotto", 34);
        PickupCounts.Add("mangrove", 20);
        PickupCounts.Add("thornfeltSwamp", 21);
        PickupCounts.Add("ginsoTree", 23);
        PickupCounts.Add("valleyOfTheWind", 19);
        PickupCounts.Add("mistyWoods", 16);
        PickupCounts.Add("forlornRuins", 11);
        PickupCounts.Add("sorrowPass", 26);
        PickupCounts.Add("mountHoru", 22);
        PickupCounts.Add("unknown", 9);
        PickupCounts.Add("total", 256);

        KeyItemOffsets = new Dictionary<string, int>();
        KeyItemOffsets.Add("Wall Jump", 0);
        KeyItemOffsets.Add("Charge Flame", 1);
        KeyItemOffsets.Add("Double Jump", 2);
        KeyItemOffsets.Add("Bash", 3);
        KeyItemOffsets.Add("Stomp", 4);
        KeyItemOffsets.Add("Glide", 5);
        KeyItemOffsets.Add("Climb", 6);
        KeyItemOffsets.Add("Charge Jump", 7);
        KeyItemOffsets.Add("Dash", 8);
        KeyItemOffsets.Add("Grenade", 9);
        KeyItemOffsets.Add("Water Vein", 10);
        KeyItemOffsets.Add("Clean Water", 11);
        KeyItemOffsets.Add("Gumon Seal", 12);
        KeyItemOffsets.Add("Wind Restored", 13);
        KeyItemOffsets.Add("Sunstone", 14);
        KeyItemOffsets.Add("Warmth Returned", 15);

        SkillsById = new Dictionary<int, string> {
            { 0, "Bash" },
            { 2, "Charge Flame" },
            { 3, "Wall Jump" },
            { 4, "Stomp" },
            { 5, "Double Jump" },
            { 8, "Charge Jump" },
            { 12, "Climb" },
            { 14, "Glide" },
            { 50, "Dash" },
            { 51, "Grenade" }
        };
        EventsById = new Dictionary<int, string> {
            { 0, "Water Vein" },
            { 1, "Clean Water" },
            { 2, "Gumon Seal" },
            { 3, "Wind Restored" },
            { 4, "Sunstone" },
            { 5, "Warmth Returned" }
        };

        SceneToZone = new Dictionary<string, string>();
        SceneToZone.Add("sunkenGladesOriRoom", "sunkenGlades");
        SceneToZone.Add("sunkenGladesSpiritCavernsPushBlockIntroduction", "sunkenGlades");
        SceneToZone.Add("sunkenGladesSpiritCavernWalljumpB", "sunkenGlades");
        SceneToZone.Add("sunkenGladesSpiritCavernSaveRoomB", "sunkenGlades");
        SceneToZone.Add("sunkenGladesWaterhole", "sunkenGlades");
        SceneToZone.Add("sunkenGladesRunning", "sunkenGlades");
        SceneToZone.Add("sunkenGladesIntroSplitB", "sunkenGlades");
        SceneToZone.Add("sunkenGladesSpiritCavernLaser", "sunkenGlades");
        SceneToZone.Add("sunkenGladesSpiritB", "sunkenGlades");
        SceneToZone.Add("sunkenGladesObstaclesIntroductionStreamlined", "sunkenGlades");

        SceneToZone.Add("horuFieldsB", "hollowGrove");
        SceneToZone.Add("moonGrottoShortcutA", "hollowGrove");
        SceneToZone.Add("spiritTreeRefined", "hollowGrove");
        SceneToZone.Add("worldMapSpiritTree", "hollowGrove");
        SceneToZone.Add("upperGladesSwarmIntroduction", "hollowGrove");
        SceneToZone.Add("upperGladesSpiderCavernPuzzle", "hollowGrove");
        SceneToZone.Add("upperGladesHollowTreeSplitC", "hollowGrove");
        SceneToZone.Add("horuFieldsSlopeTransition", "hollowGrove");
        SceneToZone.Add("upperGladesSpiderIntroduction", "hollowGrove");
        SceneToZone.Add("sunkenGladesLaserStomp", "hollowGrove");

        SceneToZone.Add("moonGrottoLaserIntroduction", "moonGrotto");
        SceneToZone.Add("moonGrottoGumosHideoutB", "moonGrotto");
        SceneToZone.Add("moonGrottoBasin", "moonGrotto");
        SceneToZone.Add("moonGrottoLaserPuzzleB", "moonGrotto");

        SceneToZone.Add("ginsoTreeSprings", "ginsoTree");
        SceneToZone.Add("ginsoTreeSaveRoom", "ginsoTree");
        SceneToZone.Add("ginsoTreePuzzles", "ginsoTree");
        SceneToZone.Add("ginsoTreeBashRedirectArt", "ginsoTree");
        SceneToZone.Add("ginsoTreeWaterRisingBtm", "ginsoTree");
        SceneToZone.Add("ginsoTreeWaterRisingMid", "ginsoTree");
        SceneToZone.Add("ginsoTreeWaterRisingEnd", "ginsoTree");
        SceneToZone.Add("kuroMomentTreeDuplicate", "ginsoTree");

        SceneToZone.Add("upperGladesSwampCliffs", "thornfeltSwamp");
        SceneToZone.Add("thornfeltSwampA", "thornfeltSwamp");
        SceneToZone.Add("thornfeltSwampB", "thornfeltSwamp");
        SceneToZone.Add("thornfeltSwampE", "thornfeltSwamp");
        SceneToZone.Add("thornfeltSwampStompAbility", "thornfeltSwamp");
        SceneToZone.Add("thornfeltSwampActTwoStart", "thornfeltSwamp");
        SceneToZone.Add("thornfeltSwampMoonGrottoTransition", "thornfeltSwamp");

        SceneToZone.Add("sorrowPassForestB", "mistyWoods");
        SceneToZone.Add("mistyWoodsIntro", "mistyWoods");
        SceneToZone.Add("mistyWoodsGlideMazeA", "mistyWoods");
        SceneToZone.Add("mistyWoodsGetClimb", "mistyWoods");
        SceneToZone.Add("mistyWoodsCeilingClimbing", "mistyWoods");
        SceneToZone.Add("mistyWoodsGlideMazeB", "mistyWoods");
        SceneToZone.Add("mistyWoodsMortarBashBlockerA", "mistyWoods");
        SceneToZone.Add("mistyWoodsMortarBash", "mistyWoods");
        SceneToZone.Add("mistyWoodsProjectileBashing", "mistyWoods");
        SceneToZone.Add("mistyWoodsBashUp", "mistyWoods");
        SceneToZone.Add("mistyWoodsConnector", "mistyWoods");
        SceneToZone.Add("mistyWoodsLaserFlipPlatforms", "mistyWoods");
        SceneToZone.Add("mistyWoodsCrissCross", "mistyWoods");
        SceneToZone.Add("mistyWoodsTIntersection", "mistyWoods");
        SceneToZone.Add("mistyWoodsDocks", "mistyWoods");
        SceneToZone.Add("mistyWoodsDocksB", "mistyWoods");
        SceneToZone.Add("mistyWoodsRopeBridge", "mistyWoods");
        SceneToZone.Add("mistyWoodsJumpProjectile", "mistyWoods");

        SceneToZone.Add("sorrowPassEntranceA", "valleyOfTheWind");
        SceneToZone.Add("sorrowPassEntranceB", "valleyOfTheWind");
        SceneToZone.Add("westGladesShaftToBridgeB", "valleyOfTheWind");
        SceneToZone.Add("westGladesMistyWoodsCaveTransition", "valleyOfTheWind");
        SceneToZone.Add("westGladesRollingSootIntroduction", "valleyOfTheWind");
        SceneToZone.Add("forlornRuinsKuroHideStreamlined", "valleyOfTheWind");

        SceneToZone.Add("sorrowPassValleyD", "sorrowPass");
        SceneToZone.Add("valleyOfTheWindGetChargeJump", "sorrowPass");
        SceneToZone.Add("valleyOfTheWindIcePuzzle", "sorrowPass");
        SceneToZone.Add("valleyOfTheWindHubL", "sorrowPass");
        SceneToZone.Add("valleyOfTheWindWideLeft", "sorrowPass");
        SceneToZone.Add("valleyOfTheWindGauntlet", "sorrowPass");
        SceneToZone.Add("valleyOfTheWindLaserShaft", "sorrowPass");

        SceneToZone.Add("forlornRuinsGravityRoomA", "forlornRuins");
        SceneToZone.Add("forlornRuinsGetIceB", "forlornRuins");
        SceneToZone.Add("forlornRuinsNestC", "forlornRuins");
        SceneToZone.Add("forlornRuinsWindShaftMockupB", "forlornRuins");
        SceneToZone.Add("forlornRuinsWindShaftMockupC", "forlornRuins");
        SceneToZone.Add("forlornRuinsGravityFreeFall", "forlornRuins");
        SceneToZone.Add("forlornRuinsGetNightberry", "forlornRuins");
        SceneToZone.Add("forlornRuinsResurrectionAfter", "forlornRuins");
        SceneToZone.Add("forlornRuinsC", "forlornRuins");

        SceneToZone.Add("mangroveFallsDashEscalation", "mangrove");
        SceneToZone.Add("northMangroveFallsIntro", "mangrove");
        SceneToZone.Add("southMangroveFallsGrenadeEscalationB", "mangrove");
        SceneToZone.Add("southMangroveFallsGrenadeEscalationBR", "mangrove");

        SceneToZone.Add("mountHoruMovingPlatform", "mountHoru");
        SceneToZone.Add("mountHoruStomperSystemsL", "mountHoru");
        SceneToZone.Add("mountHoruStomperSystemsR", "mountHoru");
        SceneToZone.Add("catAndMouseRight", "mountHoru");
        SceneToZone.Add("catAndMouseMid", "mountHoru");
        SceneToZone.Add("catAndMouseLeft", "mountHoru");
        SceneToZone.Add("catAndMouseResurrectionRoom", "mountHoru");
        SceneToZone.Add("mountHoruHubBottom", "mountHoru");
        SceneToZone.Add("mountHoruHubTop", "mountHoru");
    }

    public static string CurrentZone(bool pretty) {
        return pretty ? ZonePrettyNames[CurrentZone()].Replace("\t", "") : CurrentZone();
    }

    public static string CurrentZone() {
        if (GameWorld.Instance && Characters.Sein) {
            GameWorldArea area = GameWorld.Instance.WorldAreaAtPosition(Characters.Sein.Position);
            if (area != null) {
                return area.AreaIdentifier;
            }
        }

        if (Scenes.Manager.CurrentScene != null) {
            var scene = Scenes.Manager.CurrentScene.Scene;
            if (SceneToZone.ContainsKey(scene)) {
                return SceneToZone[scene];
            }
        }

        return "unknown";
    }

    public static bool UpdateAndReset(int counter, int max) {
        var _counter = get(counter);
        var _max = get(max);
        var update = _counter > _max;
        if (update) {
            set(max, _counter);
        }

        set(counter, 0);
        return update;
    }

    public static void OnDeath() {
        if (!Active) {
            return;
        }

        try {
            inc(shoof_sum, get(TSLDOS));
            UpdateAndReset(TSLDOS, TSLDOS_max);
            UpdateAndReset(PSLDOS, PSLDOS_max);
            UpdateAndReset(TSLD, TSLD_max);
            inc(DSLS, 1);
            inc(Deaths, 1);
            inc(Deaths + Offsets[CurrentZone()], 1);
        } catch (Exception e) {
            Randomizer.LogError("OnDeath: " + e.Message);
        }
    }

    public static void OnReturnToMenu() {
        try {
            inc(Reloads, 1);
            MenuCache = new Dictionary<int, int>();
            foreach (var single in new[] { DSLS, TSLD, Reloads, AltRCount, shoof_sum, PPM_max, PPM_max_time, PPM_max_count, Saves }) {
                MenuCache[single] = get(single);
            }

            foreach (var group in new[] { Time, Deaths })
                foreach (var offset in Offsets.Values) {
                    MenuCache[group + offset] = get(group + offset);
                }

            WriteFromCache = true;
        } catch (Exception) {
            //pass
        }
    }

    public static void OnSave() {
        OnSave(true);
    }

    public static void OnSave(bool userInitiated) {
        if (!Active) {
            return;
        }

        set(TSLDOS, 0);

        set(PSLDOS, 0);
        UpdateAndReset(DSLS, DSLS_max);
        if (userInitiated) {
            inc(Saves, 1);
        }
    }

    public static void IncTime() {
        if (!Active) {
            return;
        }

        CachedTime++;
        if (Characters.Sein) {
            try {
                if (WriteFromCache) {
                    WriteFromCache = false;
                    foreach (var key in MenuCache.Keys) {
                        set(key, MenuCache[key]);
                    }
                }

                inc(Drought, CachedTime);
                inc(TSLDOS, CachedTime);
                inc(TSLD, CachedTime);
                inc(Time, CachedTime);
                inc(Time + Offsets[CurrentZone()], CachedTime);
                CachedTime = 0;
            } catch (Exception e) {
                Randomizer.LogError("IncTime: " + e.Message);
            }
        }
    }

    public static int GetObtainedPickupCount(string areaName) {
        if (!Characters.Sein?.Inventory) {
            return 0;
        }

        return get(Pickups + Offsets[areaName]);
    }

    public static void IncPickup(int loc) {
        if (Randomizer.HaveCoord(loc)) {
            return;
        }

        // the location's own zone, not wherever the player is standing: a
        // pickup can be collected without being walked to
        IncPickup(ZoneForPickup(loc));
    }

    public static void IncPickup() {
        IncPickup(CurrentZone());
    }

    public static void IncPickup(string zoneKey) {
        if (!Active) {
            return;
        }

        try {
            inc(PSLDOS, 1);
            var count = inc(Pickups, 1);
            var time = get(Time);
            if (UpdateAndReset(Drought, Drought_max)) {
                set(Drought_max_end, time);
            }

            if (count >= 10) {
                var ppm = (int)(Math.Round(count / (time / 60f), 2) * 100);
                if (ppm > get(PPM_max)) {
                    set(PPM_max, ppm);
                    set(PPM_max_time, time);
                    set(PPM_max_count, count);
                }
            }

            inc(Pickups + (Offsets.ContainsKey(zoneKey) ? Offsets[zoneKey] : Offsets["unknown"]), 1);
        } catch (Exception e) {
            Randomizer.LogError("IncPickup: " + e.Message);
        }
    }

    public static void ShowStats(int duration) {
        if (CurrentPage < PageCount) {
            var stats = GetStatsPage(CurrentPage);
            Randomizer.PrintImmediately(stats, duration, false, false, false);
            CurrentPage++;
            StatsTimer = duration;
        } else {
            CurrentPage = 0;
            if (StatsTimer > 0) {
                Randomizer.PrintImmediately("", 1, false, false, false);
                WriteStatsFile();
                if (RandomizerSettings.Dev && BingoController.Active) {
                    Randomizer.log("Bingo payload: " + BingoController.GetJson());
                }
            } else {
                ShowStats(duration);
            }
        }
    }

    public static string GetStatsPage(int page) {
        return GetStatsPage(page, false);
    }

    // Glades unless the seed moves you. A spawn item holding a teleporter and then a warp is
    // what says you start somewhere else, and the teleporter it holds is the one you start on.
    // TP values are TeleportTable keys ("Glades"), not identifiers, hence the hop.
    public static string SpawnTeleporter() {
        var spawn = Randomizer.SpawnWith ?? "";
        var tp = spawn.IndexOf("TP");
        var ws = spawn.IndexOf("WS");
        if (tp < 0 || ws < tp) {
            return "sunkenGlades";
        }

        var named = spawn.Substring(tp + 2, ws - tp - 2).Trim('/');
        return Randomizer.TeleportTable.ContainsKey(named)
            ? Randomizer.TeleportTable[named].ToString()
            : "sunkenGlades";
    }

    // A line for one milestone: what it was, when, and where. Key items and goal-mode
    // milestones are the same shape, so they share this.
    private static string Stamped(string label, int id, out int time) {
        var line = label + ":";
        if (line.Length < 10) {
            line += "\t\t";
        } else if (line.Length < 16) {
            line += "\t";
        }

        line += "\t";
        var raw = get(id);
        time = -1;
        if (raw > 0) {
            time = raw % (1 << 18);
            var zoneName = ZonePrettyNames[Offsets.First(x => x.Value == raw >> 18).Key].Trim();
            line += FormatTime(time);
            if (FormatTime(time).Length < 4) {
                line += "\t";
            }

            line += "\t\t" + zoneName;
        } else {
            line += "   N/A\t\tUnknown";
        }

        return line;
    }

    private static void Collect(SortedDictionary<int, List<string>> into, string label, int id) {
        int time;
        var line = Stamped(label, id, out time);
        if (!into.ContainsKey(time)) {
            into[time] = new List<string>();
        }

        into[time].Add(line);
    }

    public static string GetStatsPage(int page, bool forFile) {
        var statsPage = "";
        switch (page) {
            case 0:
                statsPage += "ALIGNLEFTANCHORTOPPARAMS_12_14_1_Zone		Deaths	Time			Pickups		PPM";
                foreach (var zone in Offsets.Keys) {
                    if (zone == "offworld") {
                        continue;
                    }

                    var offset = Offsets[zone];
                    var line = ZonePrettyNames[zone];
                    if (zone == "unknown") {
                        line += "\t\tN/A";
                    } else {
                        line += "\t\t" + get(Deaths + offset);
                    }

                    var time = get(Time + offset);
                    var timestr = FormatTime(time);
                    line += "\t\t" + timestr;
                    if (timestr.Length < 4) {
                        line += "\t";
                    }

                    if (PickupCounts.ContainsKey(zone)) {
                        var count = get(Pickups + offset);
                        var pickupstr = count + "/" + PickupCounts[zone];
                        line += "\t\t" + pickupstr;
                        if (pickupstr.Length < 5) {
                            line += "\t";
                        }

                        var ppm = count / (time / 60f);
                        if (time == 0 || ppm > 256 || zone == "unknown") {
                            line += "\t\tN/A";
                        } else {
                            line += "\t\t" + Math.Round(ppm, 2);
                        }
                    } else {
                        line += "\t\tN/A\t\t\tN/A";
                    }

                    statsPage += "\n" + line;
                }

                break;
            case 1:
                var ppm_max = get(PPM_max) / 100f;
                statsPage = "ALIGNLEFTANCHORTOPPADDING_0_2_0_0_PARAMS_16_12_1_\nSaves:					" + get(Saves);
                statsPage += "\nReloads:					" + get(Reloads);
                var altrc = get(AltRCount);
                if (altrc > 0) {
                    statsPage += "\nAlt+Rs Used:				" + altrc;
                    statsPage += "\nTeleporters Used:			" + get(TeleporterCount);
                } else {
                    statsPage += "\nTimes Warped:				" + get(TeleporterCount);
                }

                statsPage += "\nEnemies Killed:				" + get(EnemiesKilled);
                statsPage += "\nBy Leveling up:				" + get(LevelUpKills);
                statsPage += "\nExp collected:				" + get(ExpGained);
                if (get(ExpBonus) > 0) {
                    statsPage += " + " + get(ExpBonus) + " bonus";
                }

                statsPage += "\nPeak Pickups Per Minute:		" + ppm_max;
                if (ppm_max > 0) {
                    statsPage += " (" + get(PPM_max_count) + " / " + FormatTime(get(PPM_max_time), false) + ")";
                }

                statsPage += "\nLongest Drought:			" + FormatTime(get(Drought_max), false);
                if (get(Drought_max) > 0) {
                    var startTime = "0:00";
                    var droughtStart = get(Drought_max_end) - get(Drought_max);
                    if (droughtStart > 0) {
                        startTime = FormatTime(droughtStart, false);
                    }

                    statsPage += " (" + startTime + "-" + FormatTime(get(Drought_max_end), false) + ")";
                }

                statsPage += "\nWorst death (time lost):		" + FormatTime(get(TSLDOS_max), false);
                statsPage += "\nWorst death (pickups lost):	" + get(PSLDOS_max);
                statsPage += "\nMost deaths at one save:		" + Math.Max(get(DSLS_max), get(DSLS));
                statsPage += "\nTotal time lost to deaths:		" + FormatTime(get(shoof_sum), false);
                statsPage += "\nLongest time without dying:	" + FormatTime(Math.Max(get(TSLD_max), get(TSLD)), false);
                break;
            case 2:
                statsPage += "ALIGNLEFTANCHORTOPPADDING_0_2_0_0_PARAMS_16_12_1_Item				Found At		Zone";
                var linesByTime = new SortedDictionary<int, List<string>>();
                foreach (var item in KeyItemOffsets.Keys) {
                    var line = item + ":";
                    if (line.Length < 10) {
                        line += "\t\t";
                    } else if (line.Length < 16) {
                        line += "\t";
                    }

                    line += "\t";
                    var offset = KeyItemTime + KeyItemOffsets[item];
                    var raw = get(offset);
                    var time = -1;
                    if (raw > 0) {
                        time = raw % (1 << 18);
                        var zoneOffset = raw >> 18;
                        var zoneName = ZonePrettyNames[Offsets.First(x => x.Value == zoneOffset).Key].Trim();
                        line += FormatTime(time);
                        if (FormatTime(time).Length < 4) {
                            line += "\t";
                        }

                        line += "\t\t" + zoneName;
                    } else {
                        line += "   N/A\t\tUnknown";
                    }

                    if (!linesByTime.ContainsKey(time)) {
                        linesByTime[time] = new List<string>();
                    }

                    linesByTime[time].Add(line);
                }

                // Teleporters, shortened to fit beside the key items. Three are hidden on screen
                // and kept in the file: the spawn one is not something you went and got, and
                // Horu Fields and Lost Grove are noise on a list this long.
                var spawnTp = SpawnTeleporter();
                for (var i = 0; i < BingoController.Teleporters.Length; i++) {
                    var id = BingoController.Teleporters[i];
                    if (!forFile && (id == "horuFields" || id == "mangroveB" || id == spawnTp)) {
                        continue;
                    }

                    Collect(linesByTime, RandomizerMapWarp.ShortName(id) + " TP", TeleporterTime + i);
                }

                List<string> last;
                if (linesByTime.ContainsKey(-1)) {
                    last = linesByTime[-1];
                    linesByTime.Remove(-1);
                } else {
                    last = new List<string>();
                }

                foreach (var lines in linesByTime.Values) {
                    foreach (var line in lines) {
                        statsPage += "\n" + line;
                    }
                }

                // The page used to be sixteen key items and had room to say what was still
                // missing. With teleporters on it that no longer fits, so on screen the
                // unfound simply go unmentioned. The file still lists them.
                if (forFile || linesByTime.Count + last.Count <= ScreenRows) {
                    foreach (var line in last) {
                        statsPage += "\n" + line;
                    }
                }

                break;
            case 3:
                statsPage += "ALIGNLEFTANCHORTOPPADDING_0_2_0_0_PARAMS_16_12_1_Goal				Found At		Zone";
                var goals = new SortedDictionary<int, List<string>>();
                if (Randomizer.ForceTrees) {
                    foreach (var tree in RandomizerTrackedDataManager.Trees) {
                        Collect(goals, tree.Value + " Tree", TreeTime + tree.Key);
                    }
                }

                if (Randomizer.WorldTour) {
                    foreach (var relic in RandomizerTrackedDataManager.RelicFound) {
                        Collect(goals, relic.Key + " Relic", RelicTime + relic.Value);
                    }
                }

                if (Randomizer.ForceMaps) {
                    foreach (var pedestal in RandomizerTrackedDataManager.MapBitsByArea) {
                        Collect(goals, ZonePrettyNames[pedestal.Key].Trim() + " Map",
                            MapstoneTime + pedestal.Value);
                    }
                }

                // Fragments are the one thing the screen never shows: there can be any number of
                // them, and a wall of them would bury the milestones that are actually rare.
                if (forFile) {
                    for (var i = 0; i < get(FragCount); i++) {
                        Collect(goals, "Fragment " + (i + 1), FragFirst + i);
                    }
                }

                List<string> unfound;
                if (goals.ContainsKey(-1)) {
                    unfound = goals[-1];
                    goals.Remove(-1);
                } else {
                    unfound = new List<string>();
                }

                foreach (var lines in goals.Values) {
                    foreach (var line in lines) {
                        statsPage += "\n" + line;
                    }
                }

                if (forFile || goals.Count + unfound.Count <= ScreenRows) {
                    foreach (var line in unfound) {
                        statsPage += "\n" + line;
                    }
                }

                break;
        }

        return statsPage;
    }

    private static int get(int item) {
        return Characters.Sein.Inventory.GetRandomizerItem(item);
    }

    private static int set(int item, int value) {
        return Characters.Sein.Inventory.SetRandomizerItem(item, value);
    }

    private static int inc(int item, int value) {
        return Characters.Sein.Inventory.IncRandomizerItem(item, value);
    }

    public static void Activate() {
        Active = true;
        MenuCache = new Dictionary<int, int>();
        CachedTime = 0;
        WriteFromCache = false;
    }

    public static void Finish() {
        Active = false;
        WriteStatsFile();
    }


    // Squares a tab-separated block up into columns. Written for the key items; the goal
    // milestones are the same three columns, so they get it too.
    private static string Columned(string part) {
        part = part.Replace("   ", "");
        part = Regex.Replace(part, "\t+", "\t");
        var lines = new List<string>(part.Split('\n'));
        var spacing = new List<int> { 0, 0, 0, 0 };
        foreach (var line in lines) {
            var col = 0;
            var lastStart = 0;
            for (var i = 0; i < line.Length; i++) {
                if (line[i] == '\t') {
                    var width = i - lastStart + 2;
                    if (spacing[col] < width) {
                        spacing[col] = width;
                    }

                    col++;
                    lastStart = i;
                }
            }
        }

        var squared = "";
        foreach (var line in lines) {
            var col = 0;
            var paddedLine = "";
            foreach (var linePart in line.Split('\t')) {
                var lpc = linePart;
                while (lpc.Length < spacing[col]) {
                    lpc += " ";
                }

                paddedLine += lpc;
                col++;
            }

            squared += paddedLine + "\n";
        }

        return squared;
    }

    public static void WriteStatsFile() {
        try {
            var flagLine = File.ReadAllLines(Randomizer.SeedFilePath)[0];
            var zonePart = GetStatsPage(0).Substring(33);
            // formatting is garbage
            zonePart = zonePart.Replace("   ", "");
            zonePart = Regex.Replace(zonePart, "\t+", " ");
            var zoneLines = new List<string>(zonePart.Split('\n'));
            var zoneLineSpacing = new List<int> { 0, 0, 0, 0, 0 };
            foreach (var line in zoneLines) {
                var col = 0;
                var lastStart = 0;
                for (var i = 0; i < line.Length; i++) {
                    if (line[i] == ' ') {
                        var spacing = i - lastStart + 2;
                        if (zoneLineSpacing[col] < spacing) {
                            zoneLineSpacing[col] = spacing;
                        }

                        col++;
                        lastStart = i;
                    }
                }
            }

            zonePart = "";
            foreach (var line in zoneLines) {
                var col = 0;
                var paddedLine = "";
                foreach (var linePart in line.Split(' ')) {
                    var lpc = linePart;
                    while (lpc.Length < zoneLineSpacing[col]) {
                        lpc += " ";
                    }

                    paddedLine += lpc;
                    col++;
                }

                zonePart += paddedLine + "\n";
            }

            var miscPart = GetStatsPage(1).Substring(49);
            var miscLines = new List<string>(miscPart.Split('\n'));
            miscPart = "";
            foreach (var line in miscLines) {
                var i = line.IndexOf(":");
                var paddedLine = line.Substring(0, i + 1);
                while (paddedLine.Length < 32) {
                    paddedLine += " ";
                }

                paddedLine += line.Substring(i + 1).Trim();
                miscPart += paddedLine + "\n";
            }

            var keyItemPart = Columned(GetStatsPage(2, true).Substring(49));
            var goalPart = "";
            if (HasGoalPage || get(FragCount) > 0) {
                goalPart = "\n" + Columned(GetStatsPage(3, true).Substring(49));
            }

            var statsFile = flagLine + "\n\n" + zonePart + miscPart + "\n" + keyItemPart + goalPart;
            statsFile = statsFile.Replace("\n", "\r\n");
            File.WriteAllText("stats.txt", statsFile);
        } catch (Exception e) {
            Randomizer.LogError("WriteStatsFile: " + e.Message);
        }
    }

    public static string FormatTime(int seconds, bool padding) {
        if (padding) {
            return FormatTime(seconds);
        }

        return FormatTime(seconds).Trim();
    }

    public static string FormatTime(int seconds) {
        if (seconds == 0) {
            return "   N/A";
        }

        var secondsPart = (seconds % 60).ToString();
        if (secondsPart.Length < 2) {
            secondsPart = "0" + secondsPart;
        }

        var minutes = seconds / 60;
        var minutesPart = (minutes % 60).ToString();
        if (minutesPart.Length < 2) {
            if (minutes >= 60) {
                minutesPart = "0" + minutesPart;
            } else {
                minutesPart = "   " + minutesPart;
            }
        }

        if (minutes >= 60) {
            var hours = minutes / 60;
            return hours + ":" + minutesPart + ":" + secondsPart;
        }

        return minutesPart + ":" + secondsPart;
    }

    public static void OnKill(DamageType source) {
        inc(EnemiesKilled, 1);
        switch (source) {
            case DamageType.LevelUp:
                inc(LevelUpKills, 1);
                break;
        }
    }


    public static void WarpedToStart() {
        inc(AltRCount, 1);
    }

    public static void UsedTeleporter() {
        inc(TeleporterCount, 1);
    }

    public static void FoundMapstone() {
        inc(Pickups, 1);
        inc(Pickups + 12, 1);   // Offsets["unknown"], the Misc pseudo-zone: a progressive turn-in belongs to no zone
    }

    public static void OnExp(int expGained, int expBonus) {
        inc(ExpGained, expGained);
        inc(ExpBonus, expBonus);
    }

    public static void FoundSkill(int skillID) {
        if (SkillsById.ContainsKey(skillID)) {
            FoundKeyItem(SkillsById[skillID]);
        }
    }

    public static void FoundEvent(int eventID) {
        FoundKeyItem(EventsById[eventID]);
    }

    public static void FoundKeyItem(string itemName) {
        if (!KeyItemOffsets.ContainsKey(itemName)) {
            return;
        }

        var offset = KeyItemTime + KeyItemOffsets[itemName];
        if (get(offset) == 0) {
            var time = get(Time);
            var key = PickupZone ?? CurrentZone();
            var zone = Offsets.ContainsKey(key) ? Offsets[key] : Offsets["unknown"];
            set(offset, time + (zone << 18));
        }
    }

    // Zone of the item being granted, held for one GivePickup; null means wherever the player stands.
    public static string PickupZone;

    public static Dictionary<string, string> ZoneKeysByPrettyName;

    /// <summary>Zone a pickup at these coords belongs to, as an Offsets key;
    /// a manifest slot (-2..-257) or a grant (0) is offworld.</summary>
    public static string ZoneForPickup(int coords) {
        if (coords == 0 || (coords <= -2 && coords >= -257)) {
            return "offworld";
        }

        RandomizerLocationManager.Location loc;
        if (RandomizerLocationManager.LocationsByKey.TryGetValue(coords, out loc)
                && loc.Zone != null && ZoneKeysByPrettyName.ContainsKey(loc.Zone)) {
            return ZoneKeysByPrettyName[loc.Zone];
        }

        // a progressive mapstone says "Mapstone", and spawn has no location
        return CurrentZone();
    }

    // Stats used to live in 1500-1599, a hundred slots with no room to grow. They are the same
    // values at the same offsets, 2500 higher, and a save written before the move is copied across
    // the first time it is loaded. Total time is the sentinel: any played seed has one.
    // Stamps one milestone id with the current time and zone, once. Mirrors KeyItemFound.
    private static void Mark(int id) {
        if (Characters.Sein == null || Characters.Sein.Inventory == null || get(id) != 0) {
            return;
        }

        var key = PickupZone ?? CurrentZone();
        var zone = Offsets.ContainsKey(key) ? Offsets[key] : Offsets["unknown"];
        set(id, get(Time) + (zone << 18));
    }

    public static void TeleporterActivated(string identifier) {
        var index = System.Array.IndexOf(BingoController.Teleporters, identifier);
        if (index >= 0) {
            Mark(TeleporterTime + index);
        }
    }

    public static void TreeActivated(int treeNum) {
        Mark(TreeTime + treeNum);
    }

    public static void RelicFound(string zone) {
        int bit;
        if (RandomizerTrackedDataManager.RelicFound.TryGetValue(zone, out bit)) {
            Mark(RelicTime + bit);
        }
    }

    public static void MapstoneTurnedIn(int mapNum) {
        Mark(MapstoneTime + mapNum);
    }

    // Fragments have no fixed count, so they are stamped in the order they turn up. The held
    // count is the high-water mark: only a fragment that pushes it higher takes a new slot.
    public static void FragmentFound() {
        if (Characters.Sein == null || Characters.Sein.Inventory == null) {
            return;
        }

        var held = RandomizerBonus.WarmthFrags();
        var recorded = get(FragCount);
        if (held <= recorded) {
            return;
        }

        Mark(FragFirst + recorded);
        set(FragCount, recorded + 1);
    }

    public static void MoveOldBlock() {
        if (Characters.Sein == null || Characters.Sein.Inventory == null) {
            return;
        }

        var inventory = Characters.Sein.Inventory;
        if (inventory.GetRandomizerItem(Time) != 0 || inventory.GetRandomizerItem(1520) == 0) {
            return;
        }

        // 1587 stays behind: it is Credit Warp's pickup id, shared with the server's
        // pickup table and baked into seeds, so it is not ours to renumber.
        var carried = 0;
        for (var id = 1500; id < 1600; id++) {
            var was = inventory.GetRandomizerItem(id);
            if (was != 0) {
                inventory.SetRandomizerItem(id + 2500, was);
                carried++;
            }
        }

        Randomizer.log("stats: carried " + carried + " values up from the old block");
    }

    // Goal-mode milestones. One id each, holding when and where the same way key items do:
    // the id says which thing, the packed zone says where you were standing for it. Which
    // matters more than it sounds -- co-op can hand you a Wall Jump tree while you are in Horu.
    public static int TeleporterTime = 4100;   // + BingoController.Teleporters index

    public static int TreeTime = 4120;         // + RandomizerTrackedDataManager.Trees key

    public static int RelicTime = 4140;        // + RandomizerTrackedDataManager.RelicFound bit

    public static int MapstoneTime = 4160;     // + RandomizerTrackedDataManager.MapBitsByArea bit

    // How many warmth fragments have been found, doubling as the cursor into the slots below.
    // Taken from the count actually held, so a fragment re-collected after a death restores a
    // number already recorded and moves nothing.
    public static int FragCount = 4500;

    public static int FragFirst = 4501;

    public static int Deaths = 4000;

    public static int DSLS = 4015;
    public static int TSLD = 4016;
    public static int TSLDOS = 4017;
    public static int PSLDOS = 4018;

    public static int Time = 4020;

    public static int DSLS_max = 4035;
    public static int TSLD_max = 4036;
    public static int TSLDOS_max = 4037;
    public static int PSLDOS_max = 4038;
    public static int KeyItemTime = 4040;


    public static int Saves = 4070;
    public static int shoof_sum = 4071;
    public static int EnemiesKilled = 4072;
    public static int ExpGained = 4073;
    public static int ExpBonus = 4074;
    public static int PPM_max = 4075;
    public static int PPM_max_time = 4076;
    public static int PPM_max_count = 4077;
    public static int Reloads = 4078;
    public static int AltRCount = 4079;
    public static int TeleporterCount = 4080;
    public static int Drought = 4081;
    public static int Drought_max = 4082;
    public static int Drought_max_end = 4083;

    public static int Pickups = 1600;
    public static int LevelUpKills = 1650;

    public static int CurrentPage;

    // What the old key-item page held, and so how many lines fit on screen.
    public const int ScreenRows = 16;

    // Bingo has its own board and goal mode none has nothing to say, so neither gets a page.
    public static bool HasGoalPage {
        get {
            return (Randomizer.ForceTrees || Randomizer.WorldTour || Randomizer.ForceMaps)
                && !BingoController.Active;
        }
    }

    public static int PageCount {
        get { return HasGoalPage ? 4 : 3; }
    }
    public static int CachedTime;
    public static bool Active;
    public static bool WriteFromCache;
    public static Dictionary<string, int> KeyItemOffsets;
    public static Dictionary<int, string> SkillsById;
    public static Dictionary<int, string> EventsById;
    public static Dictionary<string, int> Offsets;
    public static Dictionary<string, int> PickupCounts;
    public static Dictionary<string, string> ZonePrettyNames;
    public static Dictionary<string, string> SceneToZone;
    public static Dictionary<int, int> MenuCache;
    public static int StatsTimer;
}

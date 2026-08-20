using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using B83.Win32;
using Core;
using Game;
using Sein.World;
using UnityEngine;
using Events = Game.Events;
using Random = System.Random;

public static class Randomizer {
    public static string VERSION = "4.2.16";

    public static void initialize() {
        try {
            OHKO = false;
            ZeroXP = false;
            BonusActive = true;
            Chaos = false;
            ChaosVerbose = false;
            Returning = false;
            Sync = false;
            SyncId = "";
            ForceMaps = false;
            SyncMode = 4;
            StringKeyPickupTypes = new List<string> { "TP", "SH", "NO", "WT", "MU", "HN", "WP", "RP", "WS", "TW", "NB" };

            RandomizerChaosManager.initialize();
            DamageModifier = 1f;
            GridFactor = 4.0;
            Message = "Good luck on your rando!";
            LastMessageCredits = false;
            PlayedGoodLuckOnce = false;
            MessageProvider = (RandomizerMessageProvider)ScriptableObject.CreateInstance(typeof(RandomizerMessageProvider));
            RandomizerUI.Instance.ClearRecentNotifications();
            ProgressiveMapStones = true;
            ForceTrees = false;
            CluesMode = false;
            Shards = false;
            WorldTour = false;
            SeedMeta = "";
            SeedFormat = MIN_SEED_FORMAT;
            PlayerCount = 0;
            MistySim = new WorldEvents();
            MistySim.MoonGuid = new MoonGuid(1061758509, 1206015992, 824243626, -2026069462);
            TeleportTable = new Hashtable();
            TeleportTable["Forlorn"] = "forlorn";
            TeleportTable["Grotto"] = "moonGrotto";
            TeleportTable["Sorrow"] = "valleyOfTheWind";
            TeleportTable["Grove"] = "spiritTree";
            TeleportTable["Swamp"] = "swamp";
            TeleportTable["Valley"] = "sorrowPass";
            TeleportTable["Ginso"] = "ginsoTree";
            TeleportTable["Horu"] = "mountHoru";
            TeleportTable["Glades"] = "sunkenGlades";
            TeleportTable["Blackroot"] = "mangroveFalls";
            Entrance = false;
            DoorTable = new Hashtable();
            ColorShift = false;
            MessageQueue = new Queue<RandomizerUI.Message>();
            MessageQueueTime = 0;
            QueueBash = false;
            BashWasQueued = false;
            BashTap = false;
            GrenadeJumpQueued = false;
            fragsEnabled = false;
            LastTick = 10000000L;
            LockedCount = 0;
            ResetTrackerCount = 0;
            HotCold = false;
            HotColdTypes = new HashSet<string> { "EV", "RB17", "RB19", "RB21", "RB28", "SK", "TPForlorn", "TPHoru", "TPGinso", "TPValley", "TPSorrow" };
            HotColdItems = new Dictionary<int, RandomizerHotColdItem>();
            HotColdFrags = new Dictionary<int, RandomizerHotColdItem>();
            HotColdMaps = new List<int>();
            HotColdMapsWithFrags = new List<int>();
            HotColdSaveId = 2000;
            OpenMode = true;
            OpenWorld = false;
            RandomizerItems.ParityLog = log;   // TEMP F2CHECK -- remove before merge
            RandomizerColorManager.Initialize();
            RandomizerRebinding.ParseRebinding();
            RandomizerSettings.ParseSettings();
            RandomizerExpNames.ParseExpNames();
            RelicZoneLookup = new Dictionary<string, string>();
            RandomizerTrackedDataManager.Initialize();
            RandomizerStatsManager.Initialize();
            RelicCount = 0;
            GrenadeZone = "MIA";
            StompZone = "MIA";
            GrenadeSlot = -1;
            StompSlot = -1;
            StompTriggers = false;
            GoalModeFinish = false;
            SpawnWith = "";
            IgnoreEnemyExp = false;
            RelicCountOverride = false;
            AllowOrbWarps = false;
            NightBerryWarpPosition = new Vector3(-910f, -300f);
            InLogicWarps = false;
            RandomizerLocationManager.ResetDoorLogic();
            TeleportersLockedByClues = false;
            WarpLogicLocations = new Hashtable();
            Keysanity.Initialize();
            RandomizerMW.Reset();
            RandomizerDeathLink.Reset();
            EnhancedMode = false;
            EnhancedSeinInSeed = false;

            if (SeedFilePath == null) {
                SeedFilePath = "randomizer.dat";
            }

            var lastLine = "";
            var lastLineNum = -1;
            try {
                if (File.Exists(SeedFilePath)) {
                    var allLines = File.ReadAllLines(SeedFilePath).ToList();

                    lastLine = allLines[0];
                    lastLineNum = 0;

                    var flagLine = allLines[0].Split('|');
                    var s = flagLine[1];
                    var flags = flagLine[0].Split(',');
                    SeedMeta = allLines[0];
                    var doBingo = ParseFlags(s, flags);
                    if (doBingo) {
                        Message = "Good luck on your bingo!";
                        BingoController.Init(allLines[allLines.Count - 1]);
                        allLines.RemoveAt(allLines.Count - 1);
                    } else {
                        BingoController.Active = false;
                    }

                    if (CluesMode) {
                        RandomizerClues.initialize();
                    }

                    foreach (var line in allLines.Skip(1)) {
                        lastLine = line;
                        lastLineNum += 1;

                        if (line.StartsWith("//")) {
                            ParseMetaLine(line.Substring(2).Trim());
                            continue;
                        }

                        var lineParts = line.Split('|');
                        int.TryParse(lineParts[0], out var coords);

                        // a cross-world line names its item differently in
                        // format 1, and nothing downstream would notice
                        if (lineParts[1] == "MW" && SeedFormat < 2) {
                            throw new Exception(
                                "this seed's multiworld lines are too old - re-download it");
                        }

                        if (RandomizerMW.IsManifestLine(coords, lineParts[1])) {
                            // multiworld slot manifest: what our slots hold,
                            // not a map location
                            RandomizerMW.AddManifestEntry(coords, lineParts[2], lineParts[3]);
                            continue;
                        }

                        if (lineParts[1] == "MW") {
                            RandomizerMW.AddApLine(coords, lineParts[2]);
                        }

                        // TEMP F2CHECK -- remove before merge
                        if (lineParts[1] == "MW") {
                            var kind = RandomizerMW.IsManifestLine(coords, lineParts[1]) ? "manifest" : "finder";
                            if (RandomizerItems.Inner(lineParts[2], PlayerCount, out var f2c, out var f2i)) {
                                F2Check.Add($"{kind} {coords} [{lineParts[2]}] -> {f2c}|{f2i} = \"{RandomizerItems.Name(f2c, f2i)}\"");
                            } else {
                                F2Check.Add($"{kind} {coords} [{lineParts[2]}] -> UNREADABLE");
                            }
                        }

                        GetDataFromSeedLine(coords, lineParts[1], lineParts[2], lineParts[3]);

                        if (coords == 2) {
                            SpawnWith = lineParts[1] + lineParts[2];
                        } else if (lineParts[1] != "EN") {
                            var repeatable = lineParts[1] == "RP";
                            RandomizerLocationManager.PlacePickup(coords, lineParts[1], lineParts[2], repeatable);
                        }
                    }

                    // TEMP F2CHECK -- remove before merge
                    log($"F2CHECK seed format {SeedFormat}, players {PlayerCount}, "
                        + $"{F2Check.Count} cross-world lines, {HotColdItems.Count} sense items, "
                        + $"{RandomizerMW.Manifest.Count} manifest slots");
                    foreach (var f2line in F2Check) {
                        log("F2CHECK " + f2line);
                    }

                    F2Check.Clear();

                    HotColdMaps.Sort();
                    HotColdMapsWithFrags.Sort();
                    if (CluesMode) {
                        RandomizerClues.FinishClues();
                    }

                    RandomizerLocationManager.InitializeLogic();
                    if (Characters.Sein) {
                        RandomizerLocationManager.UpdateReachable();
                    }
                } else {
                    printInfo("Error: " + SeedFilePath + " not found");
                    SeedFilePath = "randomizer.dat";
                }
            } catch (Exception e) {
                printInfo($"Error parsing {SeedFilePath} at line {lastLineNum}: {e.Message}", 300);
                log($"Couldn't parse \"{lastLine}\" (line {lastLineNum} of {SeedFilePath}): {e.Message}");
                SeedFilePath = "randomizer.dat";
            }

            RandomizerBonusSkill.Reset();
        } catch (Exception e) {
            log("init: " + e.Message);
        }
    }

    public static void InitializeOnce() {
        Events.Scheduler.OnGameSerializeLoad.Add(OnGameSerializeLoad);

        RandomizerSettings.ParseSettings();

        // before the location manager starts the logic thread: extracting the
        // sidecar reads Unity paths only the main thread may touch
        NativeWebSocket.Load();

        RandomizerLocationManager.Initialize();
        RandomizerUI.Initialize();
        RandomizerVersionLabel.Initialize();
        RandomizerUpdater.Initialize();
        RandomizerBootstrap.Initialize();
        Inventory = RandomizerInventory.Initialize();
        Keysanity = new RandomizerKeysanity(Inventory);

        UnityDragAndDropHook.InstallHook();
        UnityDragAndDropHook.OnDroppedFiles += OnDroppedFiles;

        unseededRandom = new Random();
    }

    public static void OnApplicationQuit() {
        UnityDragAndDropHook.UninstallHook();
    }

    public static void OnDroppedFiles(List<string> aFiles, POINT aPos) {
        if (aFiles.Count > 1) {
            return;
        }

        var filePath = aFiles[0];
        var fileName = filePath.Substring(filePath.LastIndexOf('\\') + 1);
        if (fileName.StartsWith("randomizer") && fileName.EndsWith(".dat")) {
            SeedFilePath = aFiles[0];
            initialize();
            showSeedInfo();
        }
    }

    public static void WarpTo(Vector3 position, int warpDelay) {
        Warping = warpDelay;
        WarpTarget = position;
        if (!Characters.Sein.Controller.CanMove || !Characters.Sein.Active || Characters.Sein.IsSuspended) {
            DelayedWarp = true;
            return;
        }

        if (Characters.Sein.Abilities.Carry.IsCarrying && !AllowOrbWarps) {
            Characters.Sein.Abilities.Carry.CurrentCarryable.Drop();
        }

        if (Characters.Sein.Abilities.Dash && Characters.Sein.Abilities.Dash.IsDashingOrChangeDashing) {
            Characters.Sein.Abilities.Dash.StopDashing();
        }

        BingoController.OnWarp();
        Characters.Sein.Position = position;
        Characters.Sein.Speed = new Vector3(0f, 0f);
        Characters.Ori.Position = new Vector3(position.x, position.y + 5);
        Scenes.Manager.SetTargetPositions(Characters.Sein.Position);
        UI.Cameras.Current.CameraTarget.SetTargetPosition(Characters.Sein.Position);
        UI.Cameras.Current.MoveCameraToTargetInstantly();
    }

    public static void returnToStart() {
        if (!Characters.Sein.Controller.CanMove || !Characters.Sein.Active) {
            return;
        }

        if (Items.NightBerry != null) {
            Items.NightBerry.transform.position = new Vector3(-755f, -400f);
        }

        if (Characters.Sein.Abilities.Carry.IsCarrying && !AllowOrbWarps) {
            Characters.Sein.Abilities.Carry.CurrentCarryable.Drop();
        }

        RandomizerStatsManager.WarpedToStart();
        RandomizerBonusSkill.LastAltR = Characters.Sein.Position;
        BingoController.OnWarp();
        Returning = true;
        Characters.Sein.Position = new Vector3(189f, -215f);
        Characters.Sein.Speed = new Vector3(0f, 0f);
        Characters.Ori.Position = new Vector3(190f, -210f);
        Scenes.Manager.SetTargetPositions(Characters.Sein.Position);
        UI.Cameras.Current.CameraTarget.SetTargetPosition(Characters.Sein.Position);
        UI.Cameras.Current.MoveCameraToTargetInstantly();
        var value = World.Events.Find(MistySim).Value;
        if (value != 1 && value != 8) {
            World.Events.Find(MistySim).Value = 10;
        }
    }

    public static void TeleportAnywhere() {
        if (!Characters.Sein.Controller.CanMove || !Characters.Sein.Active) {
            return;
        }

        if (Characters.Sein.IsSuspended || UI.MainMenuVisible) {
            return;
        }

        if (TeleporterController.CanTeleport(null)) {
            var defaultTeleporter = "sunkenGlades";
            var closestTeleporter = Mathf.Infinity;

            var isInGlades = false;
            var isInGrotto = false;

            if (Scenes.Manager.CurrentScene.Scene.StartsWith("sunkenGlades")) {
                isInGlades = true;
            } else if (Scenes.Manager.CurrentScene.Scene.StartsWith("moonGrotto")) {
                isInGrotto = true;
            }

            foreach (var teleporter in TeleporterController.Instance.Teleporters) {
                if (teleporter.Activated) {
                    if (isInGlades && teleporter.Identifier == "sunkenGlades") {
                        defaultTeleporter = teleporter.Identifier;
                        break;
                    }

                    if (isInGrotto && teleporter.Identifier == "moonGrotto") {
                        defaultTeleporter = teleporter.Identifier;
                        break;
                    }

                    var distanceVector = teleporter.WorldPosition - Characters.Sein.Position;
                    if (distanceVector.sqrMagnitude < closestTeleporter) {
                        defaultTeleporter = teleporter.Identifier;
                        closestTeleporter = distanceVector.sqrMagnitude;
                    }
                }
            }

            TeleporterController.Show(defaultTeleporter);
            IsUsingRandomizerTeleportAnywhere = true;
        } else {
            printInfo("No #Spirit Wells# have been activated yet!");
        }
    }

    //  more reliable hook for game end / credit starts
    public static void onNaruDestroyed() {
        if (Scenes.Manager.CurrentScene.Scene == "theSacrifice" && RandomizerStatsManager.Active) {
            RandomizerStatsManager.Finish();
            RandomizerSyncManager.SendGameComplete();
            RandomizerCreditsManager.Initialize();
        }
    }

    public static void showHint(RandomizerUI.Message message) {
        LastMessageCredits = false;
        PlayedGoodLuckOnce = false;
        Message = message.MessageString;
        MessageBgColor = message.BgColor;

        if (RandomizerSettings.Customization.MultiplePickupMessages) {
            RandomizerUI.Instance.QueueSideNotification(message);
        } else {
            MessageQueue.Enqueue(message);
            if (RandomizerUI.Instance != null) {
                RandomizerUI.Instance.RecordRecentNotification(message);
            }
        }
    }

    public static void printInfo(string message) {
        MessageQueue.Enqueue(RandomizerUI.Message.InfoMessage(message));
    }

    public static void printInfo(string message, int frames) {
        MessageQueue.Enqueue(RandomizerUI.Message.InfoMessage(message, frames / 60f));
    }

    public static void playLastMessage() {
        if (LastMessageCredits) {
            showCredits(Message, 5);
        } else if (Message == "Good luck on your rando!" || Message == "Good luck on your bingo!") {
            if (PlayedGoodLuckOnce) {
                var split = Message.Substring(0, Message.Length - 1).ToLower().Split(' ');
                var n = split.Count();
                while (n > 1) {
                    var k = unseededRandom.Next(n--);
                    var value = split[k];
                    split[k] = split[n];
                    split[n] = value;
                }

                split[0] = split[0][0].ToString().ToUpper() + split[0].Substring(1);
                var shuffled = String.Join(" ", split) + "!";
                MessageQueue.Enqueue(RandomizerUI.Message.PickupMessage(shuffled));
            } else {
                MessageQueue.Enqueue(RandomizerUI.Message.PickupMessage(Message));
            }

            PlayedGoodLuckOnce = true;
        } else {
            MessageQueue.Enqueue(new RandomizerUI.Message(Message, MessageBgColor));
        }
    }

    public static void log(string message) {
        var streamWriter = File.AppendText("randomizer.log");
        streamWriter.WriteLine(DateTime.Now + ": " + message);
        streamWriter.Flush();
        streamWriter.Dispose();
    }

    public static bool WindRestored() {
        return Sein.World.Events.WindRestored && Scenes.Manager.CurrentScene != null && Scenes.Manager.CurrentScene.Scene != "forlornRuinsResurrection" && Scenes.Manager.CurrentScene.Scene != "forlornRuinsRotatingLaserFlipped";
    }

    public static void hintAndLog(float x, float y) {
        var message = (int)x + " " + (int)y;
        showHint(RandomizerUI.Message.PickupMessage(message));
        log(message);
    }

    public static void Update() {
        UpdateMessages();
        UpdatePendingWin();
        RandomizerDeathLink.Update();
        RandomizerTempResourceUI.EnsureRows();
        Tick();

        if (GameStateMachine.Instance?.CurrentState == GameStateMachine.State.Prologue) {
            return;
        }

        if (Characters.Sein && SkillTreeManager.Instance != null && SkillTreeManager.Instance.NavigationManager.IsVisible) {
            if (Characters.Sein.IsSuspended) {
                SkillTreeManager.Instance.NavigationManager.FadeAnimator.SetParentOpacity(1f);
                UberPostProcess.Instance.SetDoBlur(true);
            } else {
                SkillTreeManager.Instance.NavigationManager.FadeAnimator.SetParentOpacity(RandomizerSettings.QOL.AbilityMenuOpacity);
                UberPostProcess.Instance.SetDoBlur(!RandomizerSettings.Accessibility.DisableMenuBlur);
            }
        }

        if (Characters.Sein && !Characters.Sein.IsSuspended) {
            RandomizerBonus.Update();
            if (!ColorShift) {
                RandomizerColorManager.UpdateColors();
            }

            if (get(82) > 0 && Items.NightBerry != null) {
                Items.NightBerry.transform.position = NightBerryWarpPosition;
                set(82, 0);
            }

            if (RandomizerBonusSkill.LevelExplosionCooldown > 0) {
                RandomizerBonusSkill.LevelExplosionCooldown--;
                if (RandomizerBonusSkill.LevelExplosionCooldown > 10) {
                    Characters.Sein.Energy.SetCurrent(RandomizerBonusSkill.OldEnergy);
                    Characters.Sein.Mortality.Health.SetAmount(RandomizerBonusSkill.OldHealth);
                }
            }

            if (Chaos) {
                RandomizerChaosManager.Update();
            }

            if (Sync) {
                RandomizerSyncManager.Update();
            }

            if (Warping > 0) {
                if (DelayedWarp) {
                    DelayedWarp = false;
                    WarpTo(WarpTarget, Warping);
                } else {
                    Characters.Sein.Position = WarpTarget;
                    Characters.Sein.Speed = new Vector3(0f, 0f);
                    Characters.Ori.Position = new Vector3(WarpTarget.x, WarpTarget.y - 5);
                    var loading = false;
                    foreach (var sms in Scenes.Manager.ActiveScenes) {
                        if (sms.CurrentState == SceneManagerScene.State.Loading) {
                            loading = true;
                            break;
                        }
                    }

                    if (!loading) {
                        Warping--;
                    }

                    if (Warping == 0 && SaveAfterWarp) {
                        GameController.Instance.CreateCheckpoint();
                        RandomizerStatsManager.OnSave(false);
                        GameController.Instance.SaveGameController.PerformSave();
                        SaveAfterWarp = false;
                    }
                }
            } else if (Returning) {
                Characters.Sein.Position = new Vector3(189f, -215f);
                if (Scenes.Manager.CurrentScene?.Scene == "sunkenGladesRunaway") {
                    Returning = false;
                }
            }
        }

        if (CreditsActive) {
            return;
        }

        if (RandomizerRebinding.ReloadSeed.IsPressed()) {
            initialize();
            if (RandomizerSettings.Dev) {
                log("Reset and loaded seed: " + SeedMeta);
            }

            showSeedInfo();
            return;
        }

        if (Characters.Sein) {
            if (RandomizerRebinding.ShowStats.IsPressed()) {
                RandomizerStatsManager.ShowStats(10);
                if (BingoController.Active) {
                    log("Current bingo state: \n" + BingoController.GetJson());
                }

                return;
            }

            if (RandomizerRebinding.ListTrees.IsPressed()) {
                MessageQueueTime = 0;
                RandomizerTrackedDataManager.ListTrees();
                return;
            }

            if (RandomizerRebinding.ListRelics.IsPressed()) {
                MessageQueueTime = 0;
                RandomizerTrackedDataManager.ListRelics();
                return;
            }

            if (RandomizerRebinding.ListMapAltars.IsPressed()) {
                MessageQueueTime = 0;
                RandomizerTrackedDataManager.ListMapstones();
                return;
            }

            if (RandomizerRebinding.ListTeleporters.IsPressed()) {
                MessageQueueTime = 0;
                RandomizerTrackedDataManager.ListTeleporters();
                return;
            }

            if (RandomizerRebinding.ShowBonuses.IsPressed()) {
                RandomizerBonus.ListBonuses();
                return;
            }

            if (RandomizerRebinding.BonusSwitch.IsPressed()) {
                RandomizerBonusSkill.SwitchBonusSkill();
                return;
            }

            if (RandomizerRebinding.BonusToggle.IsPressed()) {
                RandomizerBonusSkill.ActivateBonusSkill();
                return;
            }

            if (RandomizerRebinding.Bonus1.IsPressed()) {
                RandomizerBonusSkill.BonusSkillSlot(0);
                return;
            }

            if (RandomizerRebinding.Bonus2.IsPressed()) {
                RandomizerBonusSkill.BonusSkillSlot(1);
                return;
            }

            if (RandomizerRebinding.Bonus3.IsPressed()) {
                RandomizerBonusSkill.BonusSkillSlot(2);
                return;
            }

            if (RandomizerRebinding.Bonus4.IsPressed()) {
                RandomizerBonusSkill.BonusSkillSlot(3);
                return;
            }

            if (RandomizerRebinding.Bonus5.IsPressed()) {
                RandomizerBonusSkill.BonusSkillSlot(4);
                return;
            }

            if (RandomizerRebinding.Bonus6.IsPressed()) {
                RandomizerBonusSkill.BonusSkillSlot(5);
                return;
            }

            if (RandomizerRebinding.Bonus7.IsPressed()) {
                RandomizerBonusSkill.BonusSkillSlot(6);
                return;
            }

            if (RandomizerRebinding.Bonus8.IsPressed()) {
                RandomizerBonusSkill.BonusSkillSlot(7);
                return;
            }

            if (RandomizerRebinding.Bonus9.IsPressed()) {
                RandomizerBonusSkill.BonusSkillSlot(8);
                return;
            }
        }

        if (RandomizerRebinding.ReturnToStart.IsPressed() && Characters.Sein && !SafeIsBashing && Warping <= 0) {
            if (CanWarp > 0 && Vector3.Distance(WarpSource, Characters.Sein.Position) < 7) {
                WarpTo(WarpTarget, 15);
                CanWarp = 0;
                return;
            }

            if (AltRDisabled || RandomizerBonus.AltRDisabled()) {
                printInfo("Return to start is disabled!");
                return;
            }

            if (get(1104) > 0) {
                returnToStart();
            } else {
                TeleportAnywhere();
            }

            return;
        }

        if (RandomizerRebinding.ShowProgress.IsPressed() && Characters.Sein) {
            MessageQueueTime = 0;
            showProgress();
            return;
        }

        if (RandomizerRebinding.ColorShift.IsPressed()) {
            var obj = "Color shift enabled";
            if (ColorShift) {
                obj = "Color shift disabled";
            } else {
                changeColor();
            }

            ColorShift = !ColorShift;
            printInfo(obj);
        }

        if (RandomizerRebinding.ShowKeysanityProgress.IsPressed()) {
            Keysanity.ShowKeyProgress();
        }

        if (RandomizerRebinding.ToggleChaos.IsPressed() && Characters.Sein) {
            if (Chaos) {
                showChaosMessage("Chaos deactivated");
                Chaos = false;
                RandomizerChaosManager.ClearEffects();
                return;
            }

            showChaosMessage("Chaos activated");
            Chaos = true;
        } else if (RandomizerRebinding.ChaosVerbosity.IsPressed() && Chaos) {
            ChaosVerbose = !ChaosVerbose;
            if (ChaosVerbose) {
                showChaosMessage("Chaos messages enabled");
                return;
            }

            showChaosMessage("Chaos messages disabled");
        } else {
            if (RandomizerRebinding.ForceChaosEffect.IsPressed() && Chaos && Characters.Sein) {
                RandomizerChaosManager.SpawnEffect();
            }
        }
    }

    public static void showChaosEffect(string message) {
        if (ChaosVerbose) {
            printInfo(message);
        }
    }

    public static void showChaosMessage(string message) {
        printInfo(message);
    }

    // where the escape says Stomp and Grenade are: the Archipelago hint when
    // one was bought, otherwise whatever the seed baked (often "MIA")
    public static string StompHint() {
        return RandomizerMW.ApHintOr(StompSlot, StompZone);
    }

    public static string GrenadeHint() {
        return RandomizerMW.ApHintOr(GrenadeSlot, GrenadeZone);
    }

    public static void showProgress() {
        try {
            var text = "";
            var g = "";
            if (ForceTrees || CluesMode) {
                var trees = RandomizerBonus.SkillTreeProgression();
                g = trees >= 10 ? "$" : "";
                text += $"{g}Trees ({trees}/10){g}  ";
            }

            if (WorldTour && Characters.Sein) {
                var relics = get(402);
                g = relics >= RelicCount ? "$" : "";
                text += $"{g}Relics ({relics}/{RelicCount}){g}  ";
            }

            var maps = RandomizerBonus.MapStoneProgression();
            g = maps >= 9 ? "$" : "";
            text += $"{g}Maps ({maps}/9){g}  ";
            var pickups = get(1600);
            g = pickups >= 256 ? "$" : "";

            text += $"{g}Total ({pickups}/256){g}\n";
            if (CluesMode) {
                text += RandomizerClues.GetClues();
            } else if (Shards) {
                if (Keys.GinsoTree) {
                    text += $"*WV ({RandomizerBonus.WaterVeinShards()}/3)*  ";
                } else {
                    text += $"*WV* ({RandomizerBonus.WaterVeinShards()}/3)  ";
                }

                if (Keys.ForlornRuins) {
                    text += $"#GS ({RandomizerBonus.GumonSealShards()}/3)#  ";
                } else {
                    text += $"#GS# ({RandomizerBonus.GumonSealShards()}/3)  ";
                }

                if (Keys.MountHoru) {
                    text += $"@SS ({RandomizerBonus.SunstoneShards()}/3)@  ";
                } else {
                    text += $"@SS@ ({RandomizerBonus.SunstoneShards()}/3)  ";
                }
            } else // the below is ugly code, but otoh it's also code that will only run for people who are playing clueless shardless seeds, and. :orishrug: they had it coming, or something.
            {
                text += $"*WV{(Keys.GinsoTree ? ": Found*" : "*: ????")}  #GS{(Keys.ForlornRuins ? ": Found#" : "#: ????")}  @SS{(Keys.MountHoru ? ": Found@" : "@: ????")}";
            }

            if (fragsEnabled) {
                var frags = RandomizerBonus.WarmthFrags();
                g = frags >= fragKeyFinish ? "$" : "";
                text += $" {g}Frags: ({RandomizerBonus.WarmthFrags()}/{fragKeyFinish}){g}";
            }

            if (RandomizerBonus.ForlornEscapeHint()) {
                var s = "";
                var g_color = "";
                if (Characters.Sein) {
                    s = Characters.Sein.PlayerAbilities.HasAbility(AbilityType.Stomp) ? "$" : "";
                    g = Characters.Sein && Characters.Sein.PlayerAbilities.HasAbility(AbilityType.Grenade) ? "$" : "";
                }

                text += $"\n{s}Stomp: {StompHint()}{s}{g}    Grenade: {GrenadeHint()}{g}";
            }

            printInfo(text);
        } catch (Exception e) {
            LogError("ShowProgress: " + e.Message);
        }
    }

    public static void showSeedInfo() {
        var problem = SeedFormatProblem;
        if (problem != null) {
            log($"seed format {SeedFormat}, this dll reads {MIN_SEED_FORMAT}-{MAX_SEED_FORMAT}");
            MessageQueue.Enqueue(new RandomizerUI.Message(problem, RandomizerUI.Message.ErrorBgColor, 8f));
            return;
        }

        var seedInfo = "v" + VERSION;
        seedInfo += "- seed loaded: " + SeedMeta;
        printInfo(seedInfo);
    }

    public static string SeedFormatProblem {
        get {
            if (SeedFormat > MAX_SEED_FORMAT) {
                return "Error: loaded seed requires a newer DLL.\nPlease update from the title screen.";
            }

            if (SeedFormat < MIN_SEED_FORMAT) {
                return "Error: loaded seed is too old for this DLL.\nPlease roll a new seed or inform the author.";
            }

            return null;
        }
    }

    public static void changeColor() {
        if (Characters.Sein) {
            Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color = new Color(FixedRandom.Values[0], FixedRandom.Values[1], FixedRandom.Values[2], 0.5f);
        }
    }

    // Win messages wait for the player to be upright and in control: the box is
    // torn down by the death/respawn sequence, and a death goal wins you the game
    // at the exact moment you die. Deliberately NOT cleared by initialize(), so a
    // mid-death alt+L can't eat it; NewGameAction clears it instead.
    public static void QueueWinMessage(string message) {
        PendingWinMessage = message;
        WinMessageStableFrames = 0;
    }

    public static void UpdatePendingWin() {
        if (PendingWinMessage == null) {
            return;
        }

        var stable = Characters.Sein && Characters.Sein.Active && Characters.Sein.Controller.CanMove
            && !Characters.Sein.IsSuspended && !UI.MainMenuVisible;
        // count stable frames, not just one: CanMove goes true before the respawn
        // fade finishes, and a message printed under the fade is a message unseen
        WinMessageStableFrames = stable ? WinMessageStableFrames + 1 : 0;
        if (WinMessageStableFrames >= 60) {
            PrintImmediately(PendingWinMessage, 15, false, true, false);
            PendingWinMessage = null;
        }
    }

    public static void UpdateMessages() {
        if (MessageQueueTime <= 0f) {
            if (MessageQueue.Count == 0) {
                return;
            }

            var queueItem = MessageQueue.Dequeue();
            var message = queueItem.MessageString;
            MessageQueueTime += queueItem.BaseDuration * 0.5f;
            MessageBgColor = queueItem.BgColor;
            if (message != "") {
                MessageProvider.SetMessage(message);
                var msgBox = UI.Hints.Show(MessageProvider, HintLayer.Randomizer, queueItem.BaseDuration + 1f);
                msgBox.SetBackgroundColor(MessageBgColor);
            }
        }

        MessageQueueTime -= Time.unscaledDeltaTime;
    }

    public static void OnDeath() {
        RandomizerBonusSkill.OnDeath();
        RandomizerStatsManager.OnDeath();
        RandomizerDeathLink.OnDeath();

        if (IsUsingRandomizerTeleportAnywhere) {
            TeleporterController.Instance.CancelTeleport();
            UI.Menu.HideMenuScreen();
        }
    }

    public static void OnGameSerializeLoad() {
        ResetTrackerCount = 0;
        // DebugMenuB serializes its own copy of this, so a load can drop what the
        // pickup granted. Only ever turn it on -- the debug menu's own toggle stays
        // usable as the off switch.
        if (Characters.Sein && Characters.Sein.Inventory != null && Characters.Sein.Inventory.GetRandomizerItem(1107) > 0) {
            DebugMenuB.DebugControlsEnabled = true;
        }

        if (Scenes.Manager.CurrentScene?.Scene != "titleScreenSwallowsNest") {
            RandomizerTrackedDataManager.Reset();
            RandomizerTrackedDataManager.UpdateBitfields();

            RandomizerLocationManager.UpdateReachable();
        }
    }

    public static void OnSave() {
        RandomizerBonusSkill.OnSave();
    }

    public static bool canFinalEscape() {
        return canFinalEscape(true);
    }

    public static bool canFinalEscape(bool verbose) {
        if (fragsEnabled && RandomizerBonus.WarmthFrags() < fragKeyFinish) {
            if (verbose) {
                printInfo(
                    string.Concat(
                        "Frags: (",
                        RandomizerBonus.WarmthFrags().ToString(),
                        "/",
                        fragKeyFinish.ToString(),
                        ")"
                    )
                );
            }

            return false;
        }

        if (WorldTour) {
            var relics = get(402);
            if (relics < RelicCount) {
                if (verbose) {
                    printInfo("Relics (" + relics + "/" + RelicCount + ")");
                }

                return false;
            }
        }

        if (ForceTrees && RandomizerBonus.SkillTreeProgression() < 10) {
            if (verbose) {
                printInfo("Trees (" + RandomizerBonus.SkillTreeProgression() + "/10)");
            }

            return false;
        }

        if (ForceMaps && RandomizerBonus.MapStoneProgression() < 9) {
            if (verbose) {
                printInfo("Maps (" + RandomizerBonus.MapStoneProgression() + "/9)");
            }

            return false;
        }

        return true;
    }

    public static void EnterDoor(Vector3 position) {
        if (!Entrance) {
            return;
        }

        var num = (int)(Math.Floor((int)position.x / GridFactor) * GridFactor) * 10000 + (int)(Math.Floor((int)position.y / GridFactor) * GridFactor);
        if (DoorTable.ContainsKey(num)) {
            Characters.Sein.Position = (Vector3)DoorTable[num];
            return;
        }

        for (var i = -1; i <= 1; i++) {
            for (var j = -1; j <= 1; j++) {
                if (DoorTable.ContainsKey(num + (int)GridFactor * (10000 * i + j))) {
                    Characters.Sein.Position = (Vector3)DoorTable[num + (int)GridFactor * (10000 * i + j)];
                    return;
                }
            }
        }

        for (var k = -2; k <= 2; k += 4) {
            for (var l = -1; l <= 1; l++) {
                if (DoorTable.ContainsKey(num + (int)GridFactor * (10000 * k + l))) {
                    Characters.Sein.Position = (Vector3)DoorTable[num + (int)GridFactor * (10000 * k + l)];
                    return;
                }
            }
        }

        printInfo("Error using door at " + (int)position.x + ", " + (int)position.y);
    }

    public static int ordHash(string s) {
        var num = 0;
        foreach (var c in s) {
            num += c;
        }

        return num;
    }

    public static void PrintImmediately(string text, int seconds, bool mute, bool setMessage, bool devOnly) {
        Print(text, seconds, mute, setMessage, devOnly, true);
    }

    public static void Print(string text, int seconds, bool mute, bool setMessage, bool devOnly, bool immediate) {
        if (devOnly && !RandomizerSettings.Dev) {
            return;
        }

        if (immediate) {
            MessageProvider.SetMessage(text);
            if (mute) {
                CachedVolume = Math.Max(GameSettings.Instance.SoundEffectsVolume, CachedVolume);
                GameSettings.Instance.SoundEffectsVolume = 0f;
                ResetVolume = 3;
            }

            UI.Hints.Show(MessageProvider, HintLayer.Randomizer, seconds);
            if (setMessage) {
                Message = text;
                MessageBgColor = RandomizerUI.Message.VanillaBgColor;
                LastMessageCredits = true;
            }
        } else {
            MessageQueue.Enqueue(RandomizerUI.Message.InfoMessage(text, seconds));
            LastMessageCredits = false;
        }
    }

    public static void LogError(string errorText) {
        log(errorText);
        PrintImmediately(errorText, 15, false, false, true);
    }

    public static void showCredits(string text, int seconds) {
        PrintImmediately(text, seconds, true, true, false);
    }

    public static void Tick() {
        try {
            var old_tick = LastTick;
            LastTick = DateTime.Now.Ticks % 10000000L;
            if (LastTick < old_tick) {
                if (RandomizerSettings.QOL.CursorLock) {
                    Cursor.lockState = CursorLockMode.Confined;
                }

                if (GameStateMachine.Instance?.CurrentState == GameStateMachine.State.Prologue) {
                    return;
                }

                BingoController.Tick();
                if (ResetVolume == 1) {
                    ResetVolume = 0;
                    GameSettings.Instance.SoundEffectsVolume = CachedVolume;
                } else if (ResetVolume > 1) {
                    ResetVolume--;
                }

                if (CanWarp > 0) {
                    CanWarp--;
                }

                if (RepeatableCooldown > 0) {
                    RepeatableCooldown--;
                }

                if (RandomizerStatsManager.StatsTimer > 0) {
                    RandomizerStatsManager.StatsTimer--;
                }

                RandomizerStatsManager.IncTime();
                if (Scenes.Manager.CurrentScene != null) {
                    var scene = Scenes.Manager.CurrentScene.Scene;
                    if (scene == "thornfeltSwampActTwoStart" && NeedGinsoEscapeCleanup) {
                        try {
                            GameController.Instance.CreateCheckpoint();
                            RandomizerStatsManager.OnSave(false);
                            GameController.Instance.SaveGameController.PerformSave();
                            GameController.Instance.SaveGameController.PerformLoad();
                        } catch (Exception e) {
                            LogError("GinsoEscapeCleanup: " + e.Message);
                        }

                        NeedGinsoEscapeCleanup = false;
                    }

                    if (scene == "titleScreenSwallowsNest") {
                        ResetTrackerCount++;
                        if (ResetTrackerCount > 10) {
                            RandomizerTrackedDataManager.Reset();
                            ResetTrackerCount = 0;
                        }

                        if (RandomizerCreditsManager.CreditsDone) {
                            RandomizerCreditsManager.CreditsDone = false;
                        }
                    } else if (scene == "forlornRuinsNestC" && get(1105) == 0) {
                        RandomizerBonus.UpgradeID(81);
                    } else if (scene == "creditsScreen") {
                        if (!CreditsActive && !RandomizerCreditsManager.CreditsDone) {
                            CreditsActive = true;
                        }
                    }
                }

                if (CreditsActive && !RandomizerCreditsManager.CreditsDone) {
                    RandomizerCreditsManager.Tick();
                }

                if (Characters.Sein) {
                    if (!Characters.Sein.IsSuspended && Scenes.Manager.CurrentScene != null) {
                        if (GoalModeFinish && RandomizerSyncManager.NetworkFree && canFinalEscape(false)) {
                            RandomizerBonusSkill.UnlockCreditWarp("Goal mode(s) completed!");
                        }

                        RandomizerTrackedDataManager.UpdateBitfields();
                        RandomizerColorManager.UpdateHotColdTarget();
                        if (Characters.Sein.Position.y > 935f && Inventory.FinishedGinsoEscape && Scenes.Manager.CurrentScene.Scene == "ginsoTreeWaterRisingEnd") {
                            if (SafeIsBashing) {
                                Characters.Sein.Abilities.Bash.BashGameComplete(0f);
                            }

                            Characters.Sein.Position = new Vector3(750f, -120f);
                            return;
                        }

                        if (get(1106) > 0 && Characters.Sein.Position.y > -235f && Scenes.Manager.CurrentScene.Scene == "forlornRuinsResurrection") {
                            if (SafeIsBashing) {
                                Characters.Sein.Abilities.Bash.BashGameComplete(0f);
                            }

                            Characters.Sein.Position = new Vector3(-1350f, -410f);
                            return;
                        }

                        if (Scenes.Manager.CurrentScene.Scene == "catAndMouseResurrectionRoom" && !canFinalEscape()) {
                            if (Entrance) {
                                EnterDoor(new Vector3(-242f, 489f));
                                return;
                            }

                            Characters.Sein.Position = new Vector3(20f, 105f);
                            return;
                        }

                        if (!Characters.Sein.Controller.CanMove && Scenes.Manager.CurrentScene.Scene == "moonGrottoGumosHideoutB") {
                            LockedCount++;
                            if (LockedCount >= 4) {
                                GameController.Instance.ResetInputLocks();
                                return;
                            }
                        } else {
                            LockedCount = 0;
                        }
                    }

                    if (RandomizerSyncManager.NetworkFree) {
                        foreach (var gameMapTP in TeleporterController.Instance.Teleporters) {
                            if (gameMapTP.Activated) {
                                continue;
                            }

                            if (gameMapTP.Identifier == "ginsoTree" && get(1024) == 1 && (RandomizerBonus.WaterVeinShards() >= 2 || RandomizerClues.IsClueActive("WV"))) {
                                TeleporterController.Activate(TeleportTable["Ginso"].ToString(), false);
                                if (RandomizerSettings.Customization.MultiplePickupMessages) {
                                    RandomizerSwitch.PickupMessage("*Ginso teleporter activated*");
                                } else {
                                    MessageQueue.Enqueue(RandomizerUI.Message.PickupMessage("*Ginso teleporter activated*"));
                                }
                            } else if (gameMapTP.Identifier == "forlorn" && get(1025) == 1 && (RandomizerBonus.GumonSealShards() >= 2 || RandomizerClues.IsClueActive("GS"))) {
                                TeleporterController.Activate(TeleportTable["Forlorn"].ToString(), false);
                                if (RandomizerSettings.Customization.MultiplePickupMessages) {
                                    RandomizerSwitch.PickupMessage("#Forlorn teleporter activated#");
                                } else {
                                    MessageQueue.Enqueue(RandomizerUI.Message.PickupMessage("#Forlorn teleporter activated#"));
                                }
                            } else if (gameMapTP.Identifier == "mountHoru" && get(1026) == 1 && (RandomizerBonus.SunstoneShards() >= 2 || RandomizerClues.IsClueActive("SS"))) {
                                TeleporterController.Activate(TeleportTable["Horu"].ToString(), false);
                                if (RandomizerSettings.Customization.MultiplePickupMessages) {
                                    RandomizerSwitch.PickupMessage("@Horu teleporter activated@");
                                } else {
                                    MessageQueue.Enqueue(RandomizerUI.Message.PickupMessage("@Horu teleporter activated@"));
                                }
                            }
                        }
                    }
                }
            }
        } catch (Exception e2) {
            LogError("Tick: " + e2.Message);
        }
    }

    public static Vector3 HashKeyToVector(int key) {
        if (key >= 0) {
            if (key % 10000 > 5000) {
                return new Vector3(key / 10000f, -(float)(10000 - key % 10000));
            }

            return new Vector3(key / 10000f, key % 10000);
        }

        if (-key % 10000 > 5000) {
            return new Vector3(key / 10000f, 10000 - -(float)key % 10000);
        }

        return new Vector3(key / 10000f, -(-(float)key % 10000));
    }

    public static bool RepeatableCheck() {
        if (RepeatableCooldown <= 0) {
            RepeatableCooldown = 2;
            return true;
        }

        return false;
    }

    private static string cct(IEnumerable<char> cs) => new String(cs.ToArray());

    public static bool ParseFlags(string seed, string[] rawFlags) {
        var doBingo = false;

        foreach (var rawFlag in rawFlags) {
            var flag = rawFlag.ToLower();

            if (flag == "ohko") {
                OHKO = true;
            } else if (flag == "race") {
                SeedMeta = $"{String.Join(",", rawFlags.Select(f => f.ToLower().StartsWith("sync") ? "Sync" + cct(f.Skip(f.IndexOf('.') - 1)) : f).ToArray())}|{cct(seed.Skip(1).SkipWhile(c => Char.IsLower(c)))}"; // yeah that's right we're LINQ wizards baby anyways this is just a bit of censorship to make things a bit harder for people trying to cheat
            } else if (flag.StartsWith("worldtour")) {
                WorldTour = true;
                if (flag.Contains("=")) {
                    RelicCountOverride = true;
                    RelicCount = int.Parse(flag.Substring(10));
                }
            } else if (flag.StartsWith("sync")) {
                Sync = true;
                SyncId = flag.Substring(4);
                RandomizerSyncManager.Initialize();
            } else if (flag.StartsWith("frags/")) {
                fragsEnabled = true;
                var fragParams = flag.Split('/');
                maxFrags = int.Parse(fragParams[2]);
                fragKeyFinish = int.Parse(fragParams[1]);
            } else if (flag.StartsWith("mode=")) {
                var modeStr = flag.Substring(5).ToLower();
                int syncMode;
                if (modeStr == "shared") {
                    syncMode = 1;
                } else if (modeStr == "none") {
                    syncMode = 4;
                } else if (modeStr == "multiworld") {
                    syncMode = 5;
                } else {
                    syncMode = int.Parse(modeStr);
                }

                SyncMode = syncMode;
            } else if (flag == "bingo") {
                doBingo = true;
            } else if (flag == "noextraexp") {
                IgnoreEnemyExp = true;
            } else if (flag == "0xp") {
                IgnoreEnemyExp = true;
                ZeroXP = true;
            } else if (flag == "nobonus") {
                BonusActive = false;
            } else if (flag == "nonprogressivemapstones") {
                ProgressiveMapStones = false;
            } else if (flag == "forcetrees") {
                ForceTrees = true;
            } else if (flag == "forcemaps") {
                ForceMaps = true;
            } else if (flag == "clues") {
                CluesMode = true;
            } else if (flag == "shards") {
                Shards = true;
            } else if (flag == "entrance") {
                Entrance = true;
            } else if (flag == "closeddungeons") {
                OpenMode = false;
            } else if (flag == "openworld") {
                OpenWorld = true;
            } else if (flag.StartsWith("hotcold=")) {
                HotCold = true;
                HotColdTypes = new HashSet<string>(rawFlag.Substring(8).Split('+').ToList());
            } else if (flag.StartsWith("sense=")) {
                HotColdTypes = new HashSet<string>(rawFlag.Substring(6).Split('+').ToList());
            } else if (flag == "noaltr") {
                AltRDisabled = true;
            } else if (flag == "stomptriggers") {
                StompTriggers = true;
            } else if (flag == "goalmodefinish") {
                GoalModeFinish = true;
            } else if (flag == "orbwarp") {
                AllowOrbWarps = true;
            } else if (flag == "inlogicwarps") {
                InLogicWarps = true;
            } else if (flag == "cluelockedtps") {
                TeleportersLockedByClues = true;
            } else if (flag == "deathlink") {
                RandomizerDeathLink.Enabled = true;
            } else if (flag == "keysanity") {
                Keysanity.IsActive = true;
            } else if (flag == "enhanced") {
                EnhancedMode = true;
            }

            if (flag == "seintalks") {
                EnhancedSeinInSeed = true;
            }
        }

        return doBingo;
    }

    public static void GetSenseFromSeedLine(int coords, string code, string id, string area) {
        // Prepare sense information, but not for pickups at spawn, and we only need at most 1 addition for each coordinate.
        if (coords == 2) {
            return;
        }

        // someone else's Bash is still a Bash: sense classifies a cross-world
        // line by the item it carries, not by the MW that wraps it
        if (code == "MW" && RandomizerItems.Inner(id, PlayerCount, out var innerCode, out var innerId)) {
            // TEMP F2CHECK -- remove before merge
            if (HotColdTypes.Contains(innerCode) || HotColdTypes.Any(t => (innerCode + innerId).StartsWith(t))) {
                F2Check.Add($"sense {coords} <- cross-world {innerCode}|{innerId}");
            }

            GetSenseFromSeedLine(coords, innerCode, innerId, area);
            return;
        }

        if (HotColdTypes.Contains(code) || HotColdTypes.Any(t => (code + id).StartsWith(t))) {
            if (Math.Abs(coords) > 100) {
                if (!HotColdItems.ContainsKey(coords)) {
                    HotColdItems.Add(coords, new RandomizerHotColdItem(HashKeyToVector(coords), HotColdSaveId));
                    HotColdSaveId++;
                }
            } else {
                if (!HotColdMaps.Contains(coords)) {
                    HotColdMaps.Add(coords);
                }

                if (!HotColdMapsWithFrags.Contains(coords)) {
                    HotColdMapsWithFrags.Add(coords);
                }
            }
        } else if (code == "MS") {
            if (Math.Abs(coords) > 100) {
                if (!HotColdFrags.ContainsKey(coords)) {
                    HotColdFrags.Add(coords, new RandomizerHotColdItem(HashKeyToVector(coords), HotColdSaveId));
                    HotColdSaveId++;
                }
            } else {
                if (!HotColdMapsWithFrags.Contains(coords)) {
                    HotColdMapsWithFrags.Add(coords);
                }
            }
        }
    }

    // seed metadata: "//Key=Value" lines after the flagline, invisible to the
    // pickup parse. Unknown keys are ignored, like unknown flags.
    public static void ParseMetaLine(string meta) {
        // "// SEED_FORMAT: n" -- seed file layout, not a dll version
        if (meta.ToLower().StartsWith("seed_format:")) {
            if (int.TryParse(meta.Substring("seed_format:".Length).Trim(), out var format)) {
                SeedFormat = format;
            }

            return;
        }

        if (meta.ToLower().StartsWith("players:")) {
            int.TryParse(meta.Substring("players:".Length).Trim(), out PlayerCount);
            return;
        }

        if (meta.ToLower().StartsWith("keytiers=")) {
            // per-door thresholds for exported-keystone seeds, positional
            // (0 = door absent); a malformed line just disables tiers
            try {
                var tierParts = meta.Substring("keytiers=".Length).Split('+');
                var tiers = new int[tierParts.Length];
                for (var ti = 0; ti < tierParts.Length; ti++) {
                    tiers[ti] = int.Parse(tierParts[ti]);
                }

                RandomizerLocationManager.KeyTiers = tiers;
            } catch (Exception) {
                RandomizerLocationManager.KeyTiers = null;
            }
        }
    }

    public static void GetDataFromSeedLine(int coords, string code, string id, string area) {
        int.TryParse(id, out var id_number);
        // If we are processing a repeatable or multipickup recur over items in them.
        if (code == "RP" || code == "MU") {
            // Check the full pickup code + id for sense. This is for sense=MUEC cases. Otherwise processed in the recursion.
            GetSenseFromSeedLine(coords, code, id, area);
            foreach (var action in new RandomizerAction(code, id).Decompose()) {
                GetDataFromSeedLine(coords, action.Action, action.Value.ToString(), area);
            }

            return;
        }

        GetSenseFromSeedLine(coords, code, id, area);

        if (code == "WT") {
            RelicZoneLookup[id] = area;
            if (!RelicCountOverride) {
                RelicCount++;
            }
        }

        if (code == "EN") {
            // door entries are coord|EN|targetX|targetY
            int.TryParse(area, out var doorY);
            DoorTable[coords] = new Vector3(id_number, doorY);
        }

        if (code == "SK") {
            if (id_number == 51) {
                GrenadeZone = area;
            } else if (id_number == 4) {
                StompZone = area;
            }
        }

        if (CluesMode && code == "EV" && id_number % 2 == 0) {
            RandomizerClues.AddClue(area, id_number / 2);
        }

        if (Keysanity.IsActive && code == "RB") {
            Keysanity.AddClue(id_number, coords, area);
        }

        if (code == "RB" && id_number == 410) {
            EnhancedSeinInSeed = true;
        }

        if (code == "TW") {
            //6399872|TW|Warp to Spirit Cavern AC,-219,-176,SpiritCavernsACWarp|Swamp
            var Pieces = id.Split(',');
            if (Pieces.Length > 3) {
                WarpLogicLocations.Add(Pieces[0], Pieces[3]);
            }
        }
    }

    private static int get(int item) {
        return Characters.Sein.Inventory.GetRandomizerItem(item);
    }

    private static int set(int item, int value) {
        return Characters.Sein.Inventory.SetRandomizerItem(item, value);
    }

    private static HashSet<int> knownUnknowns = new HashSet<int> { -1, 2, -1640264 }; // remove -1640264 once appropriate seedgen changes happen ig?

    public static bool SeenCoord(int coord) {
        if (!RandomizerTrackedDataManager.CoordsMap.ContainsKey(coord)) {
            if (!knownUnknowns.Contains(coord)) {
                LogError("Unknown coord: " + coord);
            }

            return false;
        }

        var locID = 1560 + RandomizerTrackedDataManager.CoordsMap[coord] / 32;
        return 0 != (get(locID) >> (RandomizerTrackedDataManager.CoordsMap[coord] % 32)) % 2;
    }

    public static bool HaveCoord(int coord) {
        if (!RandomizerTrackedDataManager.CoordsMap.ContainsKey(coord)) {
            if (!knownUnknowns.Contains(coord)) {
                LogError("Unknown coord: " + coord);
            }

            return false;
        }

        var locID = 930 + RandomizerTrackedDataManager.CoordsMap[coord] / 32;
        return 0 != (get(locID) >> (RandomizerTrackedDataManager.CoordsMap[coord] % 32)) % 2;
    }

    public static void OnCoord(int coord) {
        if (!RandomizerTrackedDataManager.CoordsMap.ContainsKey(coord)) {
            if (!knownUnknowns.Contains(coord)) {
                LogError("Unknown coord: " + coord);
            }

            return;
        }

        var locID = 1560 + RandomizerTrackedDataManager.CoordsMap[coord] / 32;
        var offset = RandomizerTrackedDataManager.CoordsMap[coord] % 32;
        var current = get(locID);
        // set Seen
        if ((current >> offset) % 2 == 0) {
            set(locID, current + (1 << offset));
        }

        // set Have
        locID = 930 + RandomizerTrackedDataManager.CoordsMap[coord] / 32;
        current = get(locID);
        if ((current >> offset) % 2 == 0) {
            set(locID, current + (1 << offset));
        }
    }

    public static void ApplyGrabForgiveness() {
        if (!RandomizerSettings.DevSettings.BlackrootOrbRoomClimbAssist) {
            GrabForgivenessFrames = 0f;
            return;
        }

        // XP orb jump in Blackroot lantern room, right side (initial crappy slope)
        if (new Rect(152.26f, -298.6f, 0.02f, 0.7f).Contains(Characters.Sein.PlatformBehaviour.PlatformMovement.Position2D)) {
            GrabForgivenessFrames = 4f;
            return;
        }

        // XP orb jump in Blackroot lantern room, left side (*extra* crappy slope)
        if (new Rect(147.2f, -296.5f, 0.1f, 1f).Contains(Characters.Sein.PlatformBehaviour.PlatformMovement.Position2D)) {
            GrabForgivenessFrames = 8f;
            return;
        }

        GrabForgivenessFrames = 0f;
    }

    public static bool DoesGrabForgivenessExpire(float time) {
        var scaledTime = Mathf.Round(time * 120f);
        var expires = GrabForgivenessFrames < scaledTime;
        GrabForgivenessFrames -= Mathf.Min(GrabForgivenessFrames, Mathf.Round(time * 120f));
        return expires;
    }

    public static void SetupNewGame() {
        Inventory.Clear();
        TeleporterController.RemoveCustomTeleporters();
        var spawnHCs = 0;
        var spawnECs = 0;

        // relaxed difficulty players start with +1 health and +1 energy, plus the first ability in each tree
        if (DifficultyController.Instance.Difficulty == DifficultyMode.Easy) {
            spawnHCs += 1;
            spawnECs += 1;
            Characters.Sein.PlayerAbilities.Rekindle.HasAbility = true;
            Characters.Sein.PlayerAbilities.Magnet.HasAbility = true;
            Characters.Sein.PlayerAbilities.QuickFlame.HasAbility = true;
        }

        // flag this save file for OpenWorld/ClosedDungeons flags
        if (OpenWorld) {
            set(800, 1);
            set(72, 1); // mark the first keystone door as already opened
        }

        if (!OpenMode) {
            set(801, 1);
        }

        // grant other spawn items determined by the seed
        if (SpawnWith != "") {
            RandomizerAction spawnItem;

            // horrible idea thanks
            spawnItem = new RandomizerAction(SpawnWith.Substring(0, 2), SpawnWith.Substring(2));

            var spawnItems = spawnItem.Decompose();
            // is it stupid to do it this way? yes. does it technically cover the edge case where your spawn item has HC/1/HC/1/HC/-1? also yes
            // does it make HC|4 valid but literally only on spawn? haha don't even worry about that my friends
            spawnHCs += spawnItems.Where(item => item.Action == "HC").Select(item => (int)item.Value).Aggregate(0, (acc, next) => acc + next);
            spawnECs += spawnItems.Where(item => item.Action == "EC").Select(item => (int)item.Value).Aggregate(0, (acc, next) => acc + next);
            // let the survivors regroup
            spawnItems = spawnItems.Where(item => item.Action != "HC" && item.Action != "EC").ToList();
            if (spawnItems.Count == 1) {
                RandomizerSwitch.GivePickup(spawnItems[0], 2);
            } else if (spawnItems.Count > 1) {
                RandomizerSwitch.GivePickup(RandomizerAction.AsMulti(spawnItems), 2);
            }
        }

        // top off without erasing over-max (temporary) amounts the spawn
        // items just granted (mega/mini health and energy)
        var healthOverflow = Mathf.Max(0f, Characters.Sein.Mortality.Health.Amount - Characters.Sein.Mortality.Health.MaxHealth);
        var energyOverflow = Mathf.Max(0f, Characters.Sein.Energy.Current - Characters.Sein.Energy.Max);
        Characters.Sein.Energy.Max += spawnECs;
        Characters.Sein.Mortality.Health.MaxHealth += 4 * spawnHCs;
        Characters.Sein.Mortality.Health.SetAmount(Characters.Sein.Mortality.Health.MaxHealth + healthOverflow);
        Characters.Sein.Energy.SetCurrent(Characters.Sein.Energy.Max + energyOverflow);
        RandomizerLocationManager.UpdateReachable();
    }

    public static bool SafeIsBashing => (Characters.Sein.Abilities.Bash && Characters.Sein.Abilities.Bash.IsBashing) || false;

    public static RandomizerInventory Inventory { get; private set; }
    public static RandomizerKeysanity Keysanity { get; private set; }

    public static double GridFactor;
    public static RandomizerMessageProvider MessageProvider;
    public static bool OHKO;
    public static bool ZeroXP;
    public static bool BonusActive;
    public static string Message;
    public static bool Chaos;
    public static bool ChaosVerbose;
    public static float DamageModifier;
    public static bool ProgressiveMapStones;
    public static bool ForceTrees;
    public static string SeedMeta;

    // Seed file layout, tracked separately from the dll version: it moves only
    // when the format stops being readable, in either direction.
    public const int MIN_SEED_FORMAT = 1;

    public const int MAX_SEED_FORMAT = 2;

    // seeds predating the SEED_FORMAT line are format 1
    public static int SeedFormat = MIN_SEED_FORMAT;

    // how many worlds this seed was rolled for; 0 when the seed never said.
    // An owner above it on a cross-world line is an Archipelago shadow.
    public static int PlayerCount;

    // TEMP F2CHECK -- remove before merge
    public static List<string> F2Check = new List<string>();
    public static Hashtable TeleportTable;
    public static WorldEvents MistySim;
    public static bool Returning;
    public static bool CluesMode;
    public static bool Shards;
    public static bool ColorShift;
    public static Queue<RandomizerUI.Message> MessageQueue;
    public static float MessageQueueTime;
    public static string PendingWinMessage;
    public static int WinMessageStableFrames;
    public static Color MessageBgColor;
    public static bool Sync;
    public static string SyncId;
    public static int SyncMode;
    public static List<string> StringKeyPickupTypes;
    public static bool ForceMaps;
    public static bool Entrance;
    public static Hashtable DoorTable;
    public static bool QueueBash;
    public static bool BashWasQueued;
    public static bool BashTap;
    public static bool WorldTour;
    public static bool fragsEnabled;
    public static int fragKeyFinish;
    public static int maxFrags;
    public static bool OpenMode;

    public static long LastTick;

    public static int LockedCount;

    public static int ResetTrackerCount;

    public static Vector3 WarpTarget;

    public static int Warping;

    public static bool HotCold;

    public static HashSet<string> HotColdTypes;

    public static Dictionary<int, RandomizerHotColdItem> HotColdItems;

    public static Dictionary<int, RandomizerHotColdItem> HotColdFrags;

    public static List<int> HotColdMaps;

    public static List<int> HotColdMapsWithFrags;

    public static Dictionary<string, string> RelicZoneLookup;

    public static int RelicCount;

    public static string GrenadeZone;

    // welcome to the...
    public static string StompZone;

    // the manifest slots holding them when they left our world, -1 otherwise
    public static int GrenadeSlot;

    public static int StompSlot;

    public static bool CreditsActive;

    public static float CachedVolume;

    public static int ResetVolume;

    public static bool LastMessageCredits;

    public static bool sacrificeStarted;

    public static bool AltRDisabled;

    public static int RepeatableCooldown;

    public static bool OpenWorld;

    public static bool StompTriggers;

    public static string SpawnWith;

    public static bool DelayedWarp;

    public static bool SaveAfterWarp;

    public static bool IgnoreEnemyExp;

    public static bool NeedGinsoEscapeCleanup;

    public static bool RelicCountOverride;

    public static Vector3 WarpSource;

    public static int CanWarp;

    public static bool GoalModeFinish;

    public static bool AllowOrbWarps;

    public static bool GrenadeJumpQueued;

    public static float GrabForgivenessFrames;

    public static bool IsUsingRandomizerTeleportAnywhere;

    public static string SeedFilePath;

    public static Vector3 NightBerryWarpPosition;

    public static int HotColdSaveId;

    public static bool InLogicWarps;

    public static Hashtable WarpLogicLocations;

    public static bool TeleportersLockedByClues;

    public static bool PlayedGoodLuckOnce;

    private static Random unseededRandom;

    public static bool EnhancedMode;

    public static bool EnhancedSeinInSeed;
}

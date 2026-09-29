using System;
using System.Collections.Generic;
using System.IO;
using Game;
using UnityEngine;

// A practice session: three reserved save slots, a real-time clock and its own stat block.
// Active is the flag the rest of the mod checks (sync, normal stats, boxes, the slot scan).
public static class PracticeController {
    // past the fifty slots the vanilla scan walks: on disk, never in a normal file select
    public const int FirstSlot = 50;

    public const int LoadedSlot = 51;

    public const int LastSlot = 52;

    // practice stats, above the slot stamp at 10000
    public const int ElapsedMs = 10001;

    public const int Deaths = 10002;

    // distinct locations collected this attempt, deaths or not: the csv's pickups column
    public const int Touched = 10003;

    public const int Quits = 10004;

    public const int MenuMs = 10005;

    public const int Attempts = 10006;

    // unused: reserved at the top of the practice block
    public const int GoalBoxId = 10999;

    public const int FirstStat = 10001;

    public const int LastStat = 10999;

    // pickups held, for the end count: below 4000, so a death or a reload rolls it back with them
    public const int Held = 1651;

    private const float CountdownSeconds = 3f;

    public enum Phase {
        None,
        Countdown,
        Running,
        Finished,
        Editing,
        // over, but Active until the title is up: the practice save is still in the world
        Ending
    }

    public static bool Active {
        get { return Current != Phase.None; }
    }

    public static Phase Current = Phase.None;

    public static BfrpFile File;

    // milliseconds, accumulated from real time rather than counted in ticks
    public static double Elapsed;

    public static double MenuElapsed;

    public static float Countdown;

    // the clock runs through menus, so this only marks where the time went
    public static bool InMenu;

    public static PracticeSegment Segment;

    public static void Begin(BfrpFile file) {
        Begin(file, file.Variants.Count > 0 ? file.Variants[0] : "");
    }

    public static void Begin(BfrpFile file, string variant) {
        Begin(file, variant, false);
    }

    // A segment with variants always runs one of them. From the title the screen's own load
    // sequence loads the slot, and the world freezes once it finishes.
    public static void Begin(BfrpFile file, string variant, bool fromTitle) {
        // cleared before anything can throw, or a failed parse opens the next segment in the editor
        var wanted = EditNext;
        EditNext = false;
        // parsed before anything changes, so a segment that will not parse leaves things as they were
        var segment = PracticeSegment.Parse(file, variant);
        var placements = PracticeSegment.ResolvePlacements(file, variant);
        File = file;
        File.Variant = variant;
        Segment = segment;
        // nothing to end it means nothing to run: the attempt opens in the editor instead
        editOnLoad = wanted || !Segment.HasEnd;
        if (fromTitle) {
            told = false;
        }

        awaitingStart = false;
        Randomizer.unpinMessage();
        RandomizerBoxes.Use(Segment.Boxes);
        Placements = placements;
        ResetGhosts();
        PracticeEditor.Stop();
        // Active before any slot work: the save code only admits slots 50-52 during a session
        Current = Phase.Countdown;
        PracticeHud.HideTally();
        PracticeHud.Refresh();
        // a warp still in flight from the last attempt would drag Ori off the start
        Randomizer.Warping = 0;
        Randomizer.Returning = false;
        try {
            SeedSlots(!fromTitle);
        } catch (Exception e) {
            // from the title nothing is loaded yet: a start that cannot seed its slots never began
            if (fromTitle) {
                End();
                throw;
            }

            Randomizer.log("practice: could not write the practice save: " + e);
            PracticeSelect.ReopenSaying("PRACTICE stopped: could not write the practice save\n" + e.Message);
            ReturnToTitle(true);
            return;
        }

        if (!fromTitle) {
            Suspend();
        }

        pendingSetup = true;
        Countdown = CountdownSeconds;
        Elapsed = 0;
        MenuElapsed = 0;
        touchedKeys.Clear();
        RandomizerStatsManager.Active = false;
        // from the title there is no Ori to hold the stats yet; zeroed once the save is up
        statsPending = fromTitle;
        if (!fromTitle) {
            ClearStats();
            Inc(Attempts, 1);
        }

        shownCount = -1;
    }

    private static bool statsPending;

    // loading a segment that is on its way to the editor: no run, so nothing to time
    public static bool EditPending { get { return editOnLoad; } }

    private static bool editOnLoad;

    // the next Begin lands in the editor whatever the segment says
    public static bool EditNext;

    public static void Retry() {
        if (!Active || Current == Phase.Ending) {
            return;
        }

        Begin(File, File.Variant);
    }

    // The retry bind doubles as "I have read it": the briefing holds the countdown.
    public static void RetryOrStart() {
        if (!Active) {
            return;
        }

        if (awaitingStart) {
            awaitingStart = false;
            Randomizer.unpinMessage();
            return;
        }

        // in the editor it runs what is on disk, dropping unsaved edits as Ctrl+R does
        if (PracticeEditor.Active && !PracticeEditor.Reload()) {
            return;
        }

        Retry();
    }

    private static bool awaitingStart;

    // the briefing only holds a countdown; anything that leaves the countdown takes it down
    public static void DropBriefing() {
        if (awaitingStart) {
            awaitingStart = false;
            Randomizer.unpinMessage();
        }
    }

    // once a session, not once an attempt
    private static bool told;

    // Ori's position after the load, for the page's map: only a real load knows it
    private static void RememberStart() {
        // saving writes the whole container, so never while the editor holds unsaved boxes
        if (File == null || Characters.Sein == null || PracticeEditor.Active || editOnLoad) {
            return;
        }

        var at = Characters.Sein.Position;
        var start = File.Segment["start"];
        if (start.IsObject && Math.Abs(start["x"].Num - at.x) < 1.0 && Math.Abs(start["y"].Num - at.y) < 1.0) {
            return;
        }

        try {
            var where = JsonValue.NewObject();
            where.Set("x", JsonValue.Of(Math.Round(at.x, 1)));
            where.Set("y", JsonValue.Of(Math.Round(at.y, 1)));
            File.Segment.Set("start", where);
            File.Save();
        } catch (Exception e) {
            Randomizer.log("practice: could not record the start: " + e.Message);
        }
    }

    // after the page swaps the start: begin again from the new save, in the editor if it was
    public static void Restart() {
        if (!Active || Current == Phase.Ending) {
            return;
        }

        EditNext = Current == Phase.Editing;
        Begin(File, File.Variant);
    }

    public static void End() {
        ExportIfEdited();
        DropBriefing();
        RandomizerBoxes.Use(null);
        ResetGhosts();
        PracticeEditor.Stop();
        PracticeServer.SessionEnded();
        PracticeHud.Hide();
        Resume();
        pendingSetup = false;
        Current = Phase.None;
        File = null;
        Elapsed = 0;
        MenuElapsed = 0;
        InMenu = false;
        RandomizerStatsManager.Active = true;
    }

    // a folder this session changed leaves its shareable copy in export/, from what is saved on disk
    private static void ExportIfEdited() {
        if (File == null || !File.IsFolder || !File.Edited) {
            return;
        }

        try {
            var to = PracticeFolder.ExportPath(File.Path);
            if (PracticeFolder.Export(File.Path, to, true)) {
                Randomizer.log("practice: exported " + to);
            }
        } catch (Exception e) {
            Randomizer.LogError("practice: could not export " + File.Path + ": " + e.Message);
        }
    }

    // Edits never land in a given .bfrp: the session carries on in the folder it is extracted to.
    public static bool MakeEditable() {
        if (File == null || File.IsFolder) {
            return File != null;
        }

        try {
            var copy = BfrpFile.Load(PracticeFolder.Extract(File.Path));
            var variants = copy.Variants;
            copy.Variant = variants.Contains(File.Variant) ? File.Variant : variants.Count > 0 ? variants[0] : "";
            File = copy;
            Reparse();
            Randomizer.log("practice: extracted to " + copy.Path);
            return true;
        } catch (Exception e) {
            Randomizer.LogError("practice: could not extract " + File.Path + ": " + e.Message);
            return false;
        }
    }

    // everything the player sees goes now; End waits for the title, which unloads the practice save
    private static void EndAtTitle() {
        DropBriefing();
        ResetGhosts();
        PracticeEditor.Stop();
        PracticeHud.Hide();
        Resume();
        pendingSetup = false;
        Current = Phase.Ending;
    }

    // every attempt: the file's save in slot 51, and 50, 52 and every backup emptied
    private static void SeedSlots(bool load) {
        var saves = GameController.Instance.SaveGameController;
        for (var slot = FirstSlot; slot <= LastSlot; slot++) {
            SaveSlotBackupsManager.DeleteAllBackups(slot);
            var path = saves.GetSaveFilePath(slot);
            if (System.IO.File.Exists(path)) {
                System.IO.File.Delete(path);
            }
        }

        System.IO.File.WriteAllBytes(saves.GetSaveFilePath(LoadedSlot), File.BaseSave);
        SaveSlotsManager.CurrentSlotIndex = LoadedSlot;
        SaveSlotsManager.BackupIndex = -1;
        SaveSlotsManager.PrepareSlots();
        if (load) {
            saves.PerformLoad();
        }
    }

    // the world holds still through the countdown, so a segment's own timers wait for GO
    private static bool suspended;

    private static bool pendingSetup;

    private static int shownCount;

    public static void Freeze() {
        Suspend();
    }

    // Everything but the fader (or the screen stays black) and the menus (so the countdown can
    // be paused). Suspension is counted per object, so Resume must skip the same set.
    private static void Suspend() {
        if (suspended) {
            return;
        }

        suspended = true;
        kept = new HashSet<ISuspendable>();
        if (Game.UI.Menu != null) {
            SuspensionManager.GetSuspendables(kept, true, Game.UI.Menu.gameObject);
        }

        if (Game.UI.Fader != null) {
            kept.Add(Game.UI.Fader);
        }

        SuspensionManager.SuspendExcluding(kept);
    }

    public static void Resume() {
        if (suspended) {
            suspended = false;
            SuspensionManager.ResumeExcluding(kept ?? new HashSet<ISuspendable>());
        }
    }

    private static HashSet<ISuspendable> kept;

    // the return-to-menu prompt's OK sequence, in its order, without the prompt
    public static void ReturnToTitle(bool endSession) {
        if (!Active || Current == Phase.Ending) {
            return;
        }

        try {
            Randomizer.Returning = false;
            Randomizer.Warping = 0;
            RandomizerStatsManager.OnReturnToMenu();
        } catch (Exception e) {
            Randomizer.LogError("practice: return to title: " + e.Message);
        }

        if (endSession) {
            EndAtTitle();
        } else {
            OnQuitToMenu();
        }

        GameController.Instance.RestartGame();
        if (Game.UI.Fader != null) {
            Game.UI.Fader.FadeOut(0.5f);
        }

        Game.UI.Menu.HideMenuScreen();
    }

    // a variant's loadout, granted silently; true when there was one
    private static bool GrantStartingItems() {
        if (Segment == null || Segment.StartingItems.Count == 0) {
            return false;
        }

        var silent = RandomizerSwitch.SilentMode;
        RandomizerSwitch.SilentMode = true;
        try {
            foreach (var item in Segment.StartingItems) {
                RandomizerSwitch.GivePickup(item, 0, false);
            }
        } finally {
            RandomizerSwitch.SilentMode = silent;
            RandomizerSwitch.SeedSilent = false;
        }

        return true;
    }

    public static bool IsPracticeSlot(int slot) {
        return slot >= FirstSlot && slot <= LastSlot;
    }

    // a session, or a practice save left loaded by one: never the seed's game either way
    public static bool InPracticeSave {
        get { return Active || SaveSlotsManager.Instance != null && IsPracticeSlot(SaveSlotsManager.CurrentSlotIndex); }
    }

    // every frame; the clock is real time, so a stutter costs what it costs
    public static void Tick() {
        if (!Active) {
            return;
        }

        // an ending or editing session that reaches the title ends, or it shows an empty save select there
        if ((Current == Phase.Ending || Current == Phase.Editing) && GameController.Instance != null
                && GameController.Instance.GameInTitleScreen) {
            End();
            PracticeSelect.ReopenOnTitle();
            return;
        }

        if (Current == Phase.Ending) {
            return;
        }

        var dt = Time.unscaledDeltaTime * 1000.0;
        PracticeHud.Tick();
        if (Current == Phase.Countdown) {
            // the restore lands frames after PerformLoad; touching the save sooner loses the start
            if (GameController.Instance.IsLoadingGame || InstantLoadScenesController.Instance.LockFinishingLoading) {
                return;
            }

            if (pendingSetup) {
                pendingSetup = false;
                // suspension is per object: a freeze from before the load misses what it streamed in
                Resume();
                Suspend();
                if (statsPending) {
                    statsPending = false;
                    ClearStats();
                    Inc(Attempts, 1);
                }

                // the base save may carry a seed's taken boxes or a held count; this attempt starts clear
                var changed = RandomizerBoxes.ClearOff();
                if (Get(Held) != 0) {
                    Set(Held, 0);
                    changed = true;
                }

                changed = GrantStartingItems() || changed;
                if (Segment != null) {
                    Segment.LogTurnedIn();
                }

                // checkpointed and saved: a death or quit restores those, not the live inventory
                if (changed) {
                    GameController.Instance.CreateCheckpoint();
                    GameController.Instance.SaveGameController.PerformSave();
                }

                RememberStart();
                if (editOnLoad) {
                    editOnLoad = false;
                    PracticeEditor.Begin();
                    return;
                }

                // the briefing, once a session; the countdown waits for the retry bind to answer it
                if (!told && Segment != null && !string.IsNullOrEmpty(Segment.About)) {
                    told = true;
                    awaitingStart = true;
                    Randomizer.pinMessage(Segment.About + "\n\npress [[Retry Practice Segment]] to start");
                }

                return;
            }

            // suspension is counted, so the world stays frozen under a menu; only the numbers hold
            if (Game.UI.MainMenuVisible) {
                return;
            }

            // the world is already still; the numbers start as the black lifts
            if (Fading()) {
                return;
            }

            // held on the briefing until it is answered
            if (awaitingStart) {
                return;
            }

            Countdown -= Time.unscaledDeltaTime;
            var left = Mathf.CeilToInt(Countdown);
            if (left != shownCount) {
                shownCount = left;
                Randomizer.PrintImmediately(left > 0 ? left.ToString() : "GO!", 2, false, false, false);
            }

            if (Countdown <= 0f) {
                Current = Phase.Running;
                Resume();
                StartGhosts();
            }

            return;
        }

        if (Current == Phase.Editing) {
            PracticeEditor.Tick();
            return;
        }

        if (Current != Phase.Running) {
            return;
        }

        Elapsed += dt;
        InMenu = Game.UI.MainMenuVisible;
        if (InMenu) {
            MenuElapsed += dt;
        }

        Set(ElapsedMs, (int)Elapsed);
        Set(MenuMs, (int)MenuElapsed);
        if (recording && Elapsed - lastSampleMs >= SampleGapMs) {
            Record();
        }

        if (Segment == null || Characters.Sein == null) {
            return;
        }

        Vector2 at = Characters.Sein.Position;
        if (Segment.Met(at)) {
            Finish();
        }
    }

    public static void Finish() {
        if (Current != Phase.Running) {
            return;
        }

        Current = Phase.Finished;
        var best = File == null ? -1L : File.BestMs();
        var average = File == null ? -1L : File.AverageMs();
        if (recording) {
            Record();
            recording = false;
        }

        if (File != null) {
            try {
                if (Take.Count > 1) {
                    var ghost = RandomizerGhostPacket.Pack(Take);
                    File.SetGhost(File.Variant, "recent", ghost);
                    if (best < 0 || Elapsed < best) {
                        File.SetGhost(File.Variant, "fastest", ghost);
                    }
                }

                File.AppendRun(DateTime.Now, Get(Touched), (long)Elapsed);
                File.Save();
            } catch (Exception e) {
                Randomizer.LogError("practice: could not record the run: " + e.Message);
            }
        }

        var average_mode = File != null && File.Segment["timing"]["mode"].Str == "average";
        var against = average_mode ? average : best;
        var line = "Finished in " + Clock(Elapsed);
        LastResult = Clock(Elapsed);
        if (against >= 0) {
            var delta = Elapsed - against;
            // green when the number is better than what you had, red when it is worse
            var mark = delta < 0 ? "$" : "@";
            if (delta < 0) {
                line += "   $(" + (average_mode ? "below average!" : "new PB!") + ")$";
            }

            line += "\n" + mark + (delta < 0 ? "-" : "+") + Clock(Math.Abs(delta))
                + (average_mode ? " vs average" : " vs best") + mark;
        } else {
            line += "\nfirst run";
        }

        // the run counted, then the rest of the tally
        if (File != null) {
            var runs = File.Runs.Count;
            var mean = File.AverageMs();
            var deaths = Get(Deaths);
            var quits = Get(Quits);
            line += "\navg " + Clock(mean < 0 ? Elapsed : mean) + " over " + runs + (runs == 1 ? " run" : " runs")
                + "\n" + deaths + (deaths == 1 ? " death, " : " deaths, ") + quits + (quits == 1 ? " quit" : " quits")
                + "\n" + Clock(MenuElapsed) + " in menus";
        }

        // a hint would be hidden by the finish screen opening; a box of our own is not
        PracticeHud.Refresh();
        LastTally = line;
        PracticeHud.ShowTally(line);
        PracticeMenu.Open();
    }

    // the finished time, and the finish screen's whole tally
    public static string LastResult;

    public static string LastTally;

    private static bool Fading() {
        var fader = Game.UI.Fader;
        return fader != null && fader.IsFadingInOrStay();
    }

    // The last run's ghost, kept: the pinned slot is the one retention the player chooses.
    public static bool PinLastGhost() {
        if (File == null) {
            return false;
        }

        var recent = File.GetGhost(File.Variant, "recent");
        if (recent == null) {
            return false;
        }

        try {
            File.SetGhost(File.Variant, "pinned", recent);
            File.Save();
        } catch (Exception e) {
            Randomizer.LogError("practice: could not pin the ghost: " + e.Message);
            return false;
        }

        return true;
    }

    // the vanilla Exit: a quit-to-menu segment keeps the session and its clock; others end
    public static void OnReturnToTitle() {
        PracticeEditor.OnReturnToTitle();
        if (!Active || Current == Phase.Ending) {
            return;
        }

        // an editor or a countdown has no run to park: leaving ends the session regardless
        if (Current == Phase.Editing || Current == Phase.Countdown) {
            EndAtTitle();
            return;
        }

        if (Segment != null && Segment.QuitToMenu && !PracticeMenu.TakeExitRequest()) {
            OnQuitToMenu();
            return;
        }

        EndAtTitle();
    }

    // this attempt's ghost (30 Hz on the practice clock) and the stored one it races
    private static readonly List<RandomizerGhost.Sample> Take = new List<RandomizerGhost.Sample>();

    private static IGhostSource racing;

    private static bool recording;

    private static double lastSampleMs;

    private const double SampleGapMs = 1000.0 / 30.0;

    // Best mode races the stored ghost and records this attempt; average mode does neither.
    private static void StartGhosts() {
        ResetGhosts();
        if (File == null || File.Segment["timing"]["mode"].Str == "average") {
            return;
        }

        recording = true;
        var slot = File.Segment["timing"]["showGhost"].Str;
        // the player's own setting outranks the segment's
        var preference = RandomizerSettings.Practice.Ghost == null
            ? RandomizerSettings.PracticeGhost.Segment : RandomizerSettings.Practice.Ghost.Value;
        if (preference != RandomizerSettings.PracticeGhost.Segment) {
            slot = preference.ToString().ToLowerInvariant();
        }

        if (string.IsNullOrEmpty(slot)) {
            // a pinned run is the player's own choice of pace, over the record
            slot = File.GetGhost(File.Variant, "pinned") != null ? "pinned" : "fastest";
        }

        if (slot == "none") {
            return;
        }

        var samples = RandomizerGhostPacket.Unpack(File.GetGhost(File.Variant, slot));
        if (samples.Count < 2) {
            return;
        }

        racing = new RecordedGhostSource(samples, slot, 0f);
        if (!RandomizerGhost.AddLive(racing)) {
            racing = null;
        }
    }

    private static void ResetGhosts() {
        recording = false;
        Take.Clear();
        lastSampleMs = -SampleGapMs;
        if (racing != null) {
            RandomizerGhost.Remove(racing);
            racing = null;
        }
    }

    private static void Record() {
        RandomizerGhost.Sample sample;
        var at = (float)(Elapsed / 1000.0);
        if (!RandomizerGhost.Capture(at, out sample)) {
            return;
        }

        // a gap with nobody to sample (a menu, a load) replays as a cut, not a glide
        if (Take.Count > 0 && at - Take[Take.Count - 1].Time > GapCut) {
            var hold = Take[Take.Count - 1];
            hold.Time = at - 0.001f;
            Take.Add(hold);
        }

        lastSampleMs = Elapsed;
        Take.Add(sample);
    }

    private const float GapCut = 1f;

    // this attempt's placements (bfr lines, then shuffle groups); an unlisted location is empty
    public static Dictionary<int, RandomizerAction> Placements = new Dictionary<int, RandomizerAction>();

    // this attempt's locations, which a death does not forget
    private static readonly HashSet<int> touchedKeys = new HashSet<int>();

    public static void GiveAt(int key) {
        Inc(Held, 1);
        if (touchedKeys.Add(key)) {
            Inc(Touched, 1);
        }

        RandomizerAction action;
        if (Placements.TryGetValue(key, out action) && action != null) {
            RandomizerSwitch.GivePickup(action, key, false);
        }
    }

    public static void OnQuitToMenu() {
        // a freeze that rode along would hold the title's own menu shut
        Resume();
        Inc(Quits, 1);
        ParkGhostAtLink();
    }

    // the reload puts Ori at the soul link, so the ghost waits there idle rather than gliding
    private static void ParkGhostAtLink() {
        if (!recording || Take.Count == 0) {
            return;
        }

        var last = Take[Take.Count - 1];
        if (float.IsNaN(last.SoulLink.x)) {
            return;
        }

        var parked = last;
        parked.Time = (float)(Elapsed / 1000.0);
        parked.Position = new Vector3(last.SoulLink.x, last.SoulLink.y, last.Position.z);
        parked.Animation = "idle";
        parked.AnimationTime = 0f;
        parked.Charge = 0;
        parked.Triple = false;
        parked.Died = false;
        parked.BashAngle = float.NaN;
        parked.BashTarget = new Vector2(float.NaN, float.NaN);
        parked.GrenadeAim = new Vector2(float.NaN, float.NaN);
        parked.WallAim = float.NaN;
        Take.Add(parked);
        lastSampleMs = Elapsed;
    }

    public static void OnDeath() {
        Inc(Deaths, 1);
    }

    // the boxes were edited: read again, and the new set takes the floor
    public static void Reparse() {
        if (File == null) {
            return;
        }

        var segment = PracticeSegment.Parse(File, File.Variant);
        var placements = PracticeSegment.ResolvePlacements(File, File.Variant);
        Segment = segment;
        RandomizerBoxes.Use(Segment.Boxes);
        Placements = placements;
    }

    // [h:]mm:ss.xx, rounded to hundredths before the split so 59.996 carries into the minute
    public static string Clock(double ms) {
        var centis = (long)Math.Round(ms / 10.0, MidpointRounding.AwayFromZero);
        var hours = centis / 360000;
        var minutes = centis / 6000 % 60;
        var body = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:00}:{1:00}.{2:00}", minutes, centis / 100 % 60, centis % 100);
        return hours > 0 ? hours + ":" + body : body;
    }

    private static void ClearStats() {
        if (Characters.Sein == null || Characters.Sein.Inventory == null) {
            return;
        }

        for (var id = FirstStat; id <= LastStat; id++) {
            if (Characters.Sein.Inventory.GetRandomizerItem(id) != 0) {
                Characters.Sein.Inventory.SetRandomizerItem(id, 0);
            }
        }
    }

    public static int Get(int id) {
        return Characters.Sein != null && Characters.Sein.Inventory != null
            ? Characters.Sein.Inventory.GetRandomizerItem(id) : 0;
    }

    private static void Set(int id, int value) {
        if (Characters.Sein != null && Characters.Sein.Inventory != null) {
            Characters.Sein.Inventory.SetRandomizerItem(id, value);
        }
    }

    private static void Inc(int id, int by) {
        Set(id, Get(id) + by);
    }
}

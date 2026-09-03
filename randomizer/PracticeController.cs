using System;
using System.Collections.Generic;
using System.IO;
using Game;
using UnityEngine;

// A practice session: three reserved save slots, a real-time clock, and its
// own stat block. Active is the one flag the rest of the mod consults --
// netcode, bingo and normal stats all stand down while it is set.
public static class PracticeController {
    // 50-52, past the fifty the vanilla slot scan walks, so the practice
    // triple exists on disk without ever showing up in a normal file select
    public const int FirstSlot = 50;

    public const int LoadedSlot = 51;

    public const int LastSlot = 52;

    // practice stats, above the slot stamp at 10000
    public const int ElapsedMs = 10001;

    public const int Deaths = 10002;

    public const int Pickups = 10003;

    public const int Quits = 10004;

    public const int MenuMs = 10005;

    public const int Attempts = 10006;

    // the goal box's own pickup id, at the top of the practice block
    public const int GoalBoxId = 10999;

    public const int FirstStat = 10001;

    public const int LastStat = 10999;

    private const float CountdownSeconds = 3f;

    public enum Phase {
        None,
        Countdown,
        Running,
        Finished,
        Editing
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

    // A segment with variants has no plain run: one of them is always the attempt, with
    // its own boxes, end condition, items and history. From the title screen the game's
    // own load sequence loads, and the world freezes once it has finished.
    public static void Begin(BfrpFile file, string variant, bool fromTitle) {
        File = file;
        File.Variant = variant;
        Segment = PracticeSegment.Parse(file, variant);
        RandomizerBoxes.Use(Segment.Boxes);
        Placements = PracticeSegment.ResolvePlacements(file, variant);
        ResetGhosts();
        PracticeEditor.Stop();
        // the phase is set first: the save machinery only admits the practice slots
        // exist while a session does
        Current = Phase.Countdown;
        PracticeHud.HideTally();
        PracticeHud.Refresh();
        // a warp still in flight from the last attempt would drag Ori off the start
        Randomizer.Warping = 0;
        Randomizer.Returning = false;
        SeedSlots(!fromTitle);
        if (!fromTitle) {
            Suspend();
        }

        pendingSetup = true;
        Countdown = CountdownSeconds;
        Elapsed = 0;
        MenuElapsed = 0;
        RandomizerStatsManager.Active = false;
        // from the title there is no Ori yet to hold the stats; they are zeroed once
        // the save is up instead
        statsPending = fromTitle;
        if (!fromTitle) {
            ClearStats();
            Inc(Attempts, 1);
        }

        shownCount = -1;
    }

    private static bool statsPending;

    // Loads practice/debug.bfrp and starts it, or ends a running session. The
    // file select will call Begin the same way once it exists.
    public static void BeginDebug() {
        if (Active && Current != Phase.Finished) {
            var ms = Elapsed;
            End();
            Randomizer.printInfo("Practice session ended at " + Clock(ms));
            return;
        }

        // a finished attempt is over; the next press is the next variant
        if (Active) {
            End();
        }

        var path = Path.Combine("practice", "debug.bfrp");
        if (!System.IO.File.Exists(path)) {
            Randomizer.printInfo("No " + path + " to practice");
            return;
        }

        try {
            var file = BfrpFile.Load(path);
            // each start takes the next variant, so the bench can walk all of them
            var variants = file.Variants;
            var variant = "";
            if (variants.Count > 0) {
                variant = variants[debugVariant % variants.Count];
                debugVariant++;
            }

            Begin(file, variant);
            var named = File.Segment["name"].Str;
            if (variant != "") {
                var vname = file.VariantSegment(variant)["name"];
                named += " [" + (vname.IsString ? vname.Str : variant) + "]";
            }

            Randomizer.printInfo("Practice: " + named);
        } catch (Exception e) {
            Randomizer.LogError("practice: " + path + " would not load: " + e.Message);
        }
    }

    public static void Retry() {
        if (!Active) {
            return;
        }

        Begin(File, File.Variant);
    }

    public static void End() {
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

    // Every attempt starts from the file's own save, and the two spare slots
    // start empty: a copy parked there last attempt is last attempt's state.
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

    // The world holds still through the countdown: some segments start on a
    // timer that would otherwise be running while the player reads "3".
    private static bool suspended;

    private static bool pendingSetup;

    private static int debugVariant;

    private static int shownCount;

    public static void Freeze() {
        Suspend();
    }

    // Everything but the fader, which would otherwise hold the screen black, and the
    // menus, so a wrong file can still be paused out of during the countdown. The
    // same set is left alone on resume: suspension is counted per object.
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

    // The return-to-menu prompt's own OK, without the prompt: what its sequence does,
    // in its order, so the way out looks the same.
    public static void ReturnToTitle(bool endSession) {
        if (!Active) {
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
            End();
            PracticeSelect.ReopenOnTitle();
        } else {
            OnQuitToMenu();
        }

        GameController.Instance.RestartGame();
        if (Game.UI.Fader != null) {
            Game.UI.Fader.FadeOut(0.5f);
        }

        Game.UI.Menu.HideMenuScreen();
    }

    // A variant's loadout is part of its starting state, not a pickup: granted
    // without announcement, then saved into the slot so a death keeps them.
    private static void GrantStartingItems() {
        if (Segment == null || Segment.StartingItems.Count == 0) {
            return;
        }

        var silent = RandomizerSwitch.SilentMode;
        RandomizerSwitch.SilentMode = true;
        try {
            foreach (var item in Segment.StartingItems) {
                RandomizerSwitch.GivePickup(item, 0, false);
            }
        } finally {
            RandomizerSwitch.SilentMode = silent;
        }

        GameController.Instance.CreateCheckpoint();
        GameController.Instance.SaveGameController.PerformSave();
    }

    public static bool IsPracticeSlot(int slot) {
        return slot >= FirstSlot && slot <= LastSlot;
    }

    // Called every frame; the clock is real time so a stutter costs what it
    // costs, which is the point of racing against it.
    public static void Tick() {
        if (!Active) {
            return;
        }

        var dt = Time.unscaledDeltaTime * 1000.0;
        PracticeHud.Tick();
        if (Current == Phase.Countdown) {
            // PerformLoad's checkpoint restore lands on a later frame; touching the save
            // before then loses the start position and doubles the respawn.
            if (GameController.Instance.IsLoadingGame || InstantLoadScenesController.Instance.LockFinishingLoading) {
                return;
            }

            if (pendingSetup) {
                pendingSetup = false;
                Suspend();
                if (statsPending) {
                    statsPending = false;
                    ClearStats();
                    Inc(Attempts, 1);
                }

                // the base save may carry a seed's taken boxes; this attempt's start untaken
                RandomizerBoxes.ClearConsumed();
                GrantStartingItems();
                return;
            }

            // Suspension is counted, so a menu opened over the frozen world leaves it
            // frozen when it closes; only the numbers hold while one is up.
            if (Game.UI.MainMenuVisible) {
                return;
            }

            // the world is already still; the numbers start as the black lifts
            if (Fading()) {
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
        Segment.Check(at);
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

                File.AppendRun(DateTime.Now, Get(Pickups), (long)Elapsed);
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
            line += "\n" + (delta < 0 ? "-" : "+") + Clock(Math.Abs(delta)) + (average_mode ? " vs average" : " vs best");
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
                + "\n" + deaths + (deaths == 1 ? " death, " : " deaths, ") + quits + (quits == 1 ? " quit, " : " quits, ")
                + Clock(MenuElapsed) + " in menus";
        }

        // a hint would be hidden by the finish screen opening; a box of our own is not
        PracticeHud.Refresh();
        LastTally = line;
        PracticeHud.ShowTally(line);
        PracticeMenu.Open();
    }

    // the time and the comparison, and the finish screen's whole tally
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

    // The vanilla way out of the pause menu. A segment that allows quitting to the menu
    // keeps the session, and the clock, through it; any other ends here.
    public static void OnReturnToTitle() {
        if (!Active) {
            return;
        }

        if (Segment != null && Segment.QuitToMenu && !PracticeMenu.TakeExitRequest()) {
            OnQuitToMenu();
            return;
        }

        End();
        PracticeSelect.ReopenOnTitle();
    }

    // The attempt as a ghost, sampled at the wire rate, and the stored one it races.
    // Both start when the timer does, so the ghost's clock is the practice clock.
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

        // a gap the clock ran through with nobody to sample -- a menu, a load -- is a
        // cut in the replay, not a glide
        if (Take.Count > 0 && at - Take[Take.Count - 1].Time > GapCut) {
            var hold = Take[Take.Count - 1];
            hold.Time = at - 0.001f;
            Take.Add(hold);
        }

        lastSampleMs = Elapsed;
        Take.Add(sample);
    }

    private const float GapCut = 1f;

    // The placement table for this attempt: what the bfr and the resolved shuffle
    // groups put where. A location nobody filled in is empty, silently.
    public static Dictionary<int, RandomizerAction> Placements = new Dictionary<int, RandomizerAction>();

    public static void GiveAt(int key) {
        Inc(Pickups, 1);
        RandomizerAction action;
        if (Placements.TryGetValue(key, out action) && action != null) {
            RandomizerSwitch.GivePickup(action, key, false);
        }
    }

    public static void OnQuitToMenu() {
        Inc(Quits, 1);
        ParkGhostAtLink();
    }

    // Between the quit and the reload the ghost would glide toward the link, which is
    // where the reload puts Ori; it waits there instead, idle.
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

        Segment = PracticeSegment.Parse(File, File.Variant);
        RandomizerBoxes.Use(Segment.Boxes);
    }

    public static void OnPickup() {
        Inc(Pickups, 1);
    }

    // hh:mm:ss.xx -- two decimals, which is what a practice clock is read at
    public static string Clock(double ms) {
        var total = ms / 1000.0;
        var hours = (int)(total / 3600);
        var minutes = (int)(total / 60) % 60;
        var seconds = total % 60;
        var body = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:00}:{1:00.00}", minutes, seconds);
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

using System;
using System.Collections.Generic;
using System.IO;
using CatlikeCoding.TextBox;
using UnityEngine;

// The practice chooser: the file select with a segment on every card, its header read off the
// container's save.sav. A card's backup rows are its variants; the last card makes a new segment.
public static class PracticeSelect {
    // a name inside the game folder or a full path, from the settings file
    public static string Folder {
        get {
            var setting = RandomizerSettings.Practice.Folder;
            var value = setting == null ? null : setting.Value;
            return string.IsNullOrEmpty(value) ? "practice" : value;
        }
    }

    // the file select is listing segments rather than saves
    public static bool Choosing;

    // the segment cards, and one more that makes a new segment
    public static int Count => Files.Count + 1;

    // whether each segment has anything to end it; one without opens in the editor
    private static readonly List<bool> Ends = new List<bool>();

    private static readonly List<BfrpFile> Files = new List<BfrpFile>();

    private static MessageProvider backupsLegend;

    private static bool reopen;

    private static CleverMenuItemSelectionManager exitScreen;

    private static TitleScreenManager.Screen lastScreen = TitleScreenManager.Screen.Undefined;

    private static MessageBox exitTitle;

    private static MessageProvider exitQuestion;

    private static ActionMethod exitOk;

    private static bool armed;

    // a session that exits to the chooser lands there once the title's main menu is up
    public static void ReopenOnTitle() {
        reopen = true;
    }

    // said in red once the chooser is back up, where the player is looking by then
    public static void ReopenSaying(string error) {
        reopenError = error;
    }

    private static string reopenError;

    public static void Tick() {
        var screen = TitleScreenManager.CurrentScreen;
        if (screen != lastScreen) {
            lastScreen = screen;
            // the quit prompt asks about the run instead while one is parked behind the title
            if (screen == TitleScreenManager.Screen.ExitGame && PracticeController.Active) {
                Arm();
            } else if (armed) {
                Disarm();
            }
        }

        // Escape on the main menu reaches that prompt too
        if (PracticeController.Active && screen == TitleScreenManager.Screen.MainMenu
                && Core.Input.Cancel.OnPressed && !Core.Input.Cancel.Used) {
            Core.Input.Cancel.Used = true;
            TitleScreenManager.SetScreen(TitleScreenManager.Screen.ExitGame);
        }

        if (exitLabelled != PracticeController.Active) {
            LabelExit(PracticeController.Active);
            LabelPractice(PracticeController.Active);
        }

        if (reopen && screen == TitleScreenManager.Screen.MainMenu) {
            reopen = false;
            Open();
            if (reopenError != null) {
                // the editor's legend would hold the queue for its whole run
                Randomizer.clearMessage();
                Randomizer.MessageQueue.Enqueue(new RandomizerUI.Message(reopenError, RandomizerUI.Message.ErrorBgColor, 10f));
                reopenError = null;
            }
        }

        // with a run parked, the main menu opens on START GAME, the way back into it
        if (screen == TitleScreenManager.Screen.MainMenu && PracticeController.Active) {
            if (!pointed) {
                pointed = true;
                PointAtStart();
            }
        } else {
            pointed = false;
        }
    }

    // the main menu's EXIT GAME ends the parked run rather than the game, and says so
    private static void LabelExit(bool active) {
        exitLabelled = active;
        if (mainMenu == null) {
            return;
        }

        CleverMenuItem exit = null;
        foreach (var item in mainMenu.MenuItems) {
            if (item != null && item.gameObject.name.EndsWith("exitGame")) {
                exit = item;
            }
        }

        var box = exit == null ? null : exit.GetComponentInChildren<MessageBox>(true);
        if (box == null) {
            return;
        }

        if (active) {
            if (exitLabel == null) {
                exitLabel = box.MessageProvider;
            }

            box.SetMessage(new MessageDescriptor("END PRACTICE RUN"));
        } else if (exitLabel != null) {
            box.SetMessageProvider(exitLabel);
            exitLabel = null;
        }
    }

    private static void LabelPractice(bool active) {
        var box = practiceItem == null ? null : practiceItem.GetComponentInChildren<MessageBox>(true);
        if (box != null) {
            box.SetMessage(new MessageDescriptor(active ? "CONTINUE PRACTICE" : "PRACTICE MODE"));
        }
    }

    private static CleverMenuItemSelectionManager mainMenu;

    private static CleverMenuItem practiceItem;

    private static MessageProvider exitLabel;

    private static bool exitLabelled;

    private static bool pointed;

    private static void PointAtStart() {
        if (mainMenu == null) {
            return;
        }

        for (var i = 0; i < mainMenu.MenuItems.Count; i++) {
            var item = mainMenu.MenuItems[i];
            if (item != null && item.gameObject.name.EndsWith("startGame")) {
                mainMenu.SetCurrentItem(i);
                return;
            }
        }
    }

    private static void Arm() {
        if (RandomizerUpdater.PromptShown || exitScreen == null || exitScreen.MenuItems.Count < 2) {
            return;
        }

        try {
            foreach (var box in exitScreen.GetComponentsInChildren<MessageBox>(true)) {
                if (box.GetComponentInParent<CleverMenuItem>() == null) {
                    exitTitle = box;
                    exitQuestion = box.MessageProvider;
                    box.SetMessage(new MessageDescriptor("End this practice run?"));
                    break;
                }
            }

            // OK's own press quits the game
            var ok = exitScreen.MenuItems[0];
            exitOk = ok.Pressed;
            ok.Pressed = null;
            ok.PressedCallback += EndFromTitle;
            armed = true;
        } catch (Exception e) {
            Randomizer.LogError("practice: could not borrow the quit prompt: " + e.Message);
            Disarm();
        }
    }

    private static void Disarm() {
        armed = false;
        if (exitTitle != null && exitQuestion != null) {
            exitTitle.SetMessageProvider(exitQuestion);
        }

        exitTitle = null;
        exitQuestion = null;
        if (exitScreen != null && exitScreen.MenuItems.Count > 0) {
            var ok = exitScreen.MenuItems[0];
            ok.PressedCallback -= EndFromTitle;
            if (exitOk != null) {
                ok.Pressed = exitOk;
            }
        }

        exitOk = null;
    }

    // straight from the prompt to the chooser, with no main menu in between
    private static void EndFromTitle() {
        Disarm();
        PracticeController.End();
        Open();
    }

    public static void BindMainMenu(TitleScreenManager titleScreen) {
        var menu = titleScreen == null ? null : titleScreen.MainMenuScreen;
        exitScreen = titleScreen == null ? null : titleScreen.ExitGameScreen;
        mainMenu = menu;
        practiceItem = null;
        armed = false;
        exitTitle = null;
        exitQuestion = null;
        exitOk = null;
        exitLabel = null;
        exitLabelled = false;
        lastScreen = TitleScreenManager.Screen.Undefined;
        if (menu == null || menu.MenuItems.Count < 3) {
            return;
        }

        LabelExit(PracticeController.Active);

        var template = menu.MenuItems[0];
        // a run parked behind the title is continued from here, through its three slots
        menu.AddMenuItem(PracticeController.Active ? "CONTINUE PRACTICE" : "PRACTICE MODE", 2, Open);
        var item = menu.MenuItems[2];
        // AddMenuItem keeps world scale and START GAME's own press; undo both
        item.transform.localScale = template.transform.localScale;
        item.Pressed = null;
        practiceItem = item;
    }

    public static void Open() {
        if (PracticeController.Active) {
            Choosing = false;
            TitleScreenManager.SetScreen(TitleScreenManager.Screen.SaveSlots);
            return;
        }

        Files.Clear();
        try {
            if (!Directory.Exists(Folder)) {
                Directory.CreateDirectory(Folder);
            }

            var skipped = new List<string>();
            foreach (var path in PracticeFolder.List(Folder)) {
                try {
                    Files.Add(BfrpFile.Load(path));
                } catch (Exception e) {
                    Randomizer.log("practice: skipped " + path + ": " + e.Message);
                    var why = e.Message.StartsWith(path + ": ") ? e.Message.Substring(path.Length + 2) : e.Message;
                    skipped.Add("PRACTICE skipped " + Path.GetFileName(path) + ": " + why);
                }
            }

            // a file left off the list says why on screen, or it just vanishes
            if (skipped.Count > 0) {
                Randomizer.printInfo(string.Join("\n", skipped.ToArray()), 600);
            }
        } catch (Exception e) {
            Randomizer.LogError("practice: could not list " + Folder + ": " + e.Message);
        }

        var slots = SaveSlotsManager.Instance.SaveSlots;
        slots.Clear();
        Ends.Clear();
        foreach (var file in Files) {
            slots.Add(Info(file));
            Ends.Add(HasEnd(file));
        }

        // the last card is empty: it makes a new segment
        slots.Add(null);
        Choosing = true;
        Randomizer.log("practice: " + Files.Count + " segment(s) in " + Folder);
        TitleScreenManager.SetScreen(TitleScreenManager.Screen.SaveSlots);
        // a screen still fading out from last time gets no OnEnable, so refresh by hand
        if (SaveSlotsUI.Instance != null) {
            Shown(SaveSlotsUI.Instance);
            SaveSlotsUI.Instance.RefreshSlots();
        }
    }

    // Back from the chooser: the game's own way out; the saves come back once it is hidden
    public static void Leave(SaveSlotsUI screen) {
        if (screen.OnBackPressedAction) {
            screen.OnBackPressedAction.Perform(null);
        }
    }

    // the save header the file select reads off a slot, read off the container instead
    private static SaveSlotInfo Info(BfrpFile file) {
        try {
            using (var reader = new BinaryReader(new MemoryStream(file.BaseSave))) {
                var info = new SaveSlotInfo();
                return info.LoadFromReader(reader) ? info : null;
            }
        } catch (Exception e) {
            Randomizer.log("practice: " + file.Path + " has an unreadable save: " + e.Message);
            return null;
        }
    }

    public static void Shown(SaveSlotsUI screen) {
        Legend(screen, !Choosing);
        if (Choosing) {
            Rewind(screen, Mathf.Clamp(chosen, 0, Mathf.Max(0, Count - 1)));
        } else if (PracticeController.Active) {
            // the run's own slot, the middle card
            screen.SetCurrentItemAndScroll(PracticeController.LoadedSlot - PracticeController.FirstSlot);
            screen.ItemsUI.SnapScroll();
        }
    }

    // the chooser's own position, kept apart from the saves' scroll
    private static void Rewind(SaveSlotsUI screen, int index) {
        if (savedIndex < 0) {
            savedIndex = screen.CurrentSlotIndex;
        }

        screen.SetCurrentItemAndScroll(index);
        screen.ItemsUI.SnapScroll();
    }

    private static int savedIndex = -1;

    // the segment card last picked, so the chooser reopens where it was left
    private static int chosen;

    public static void Hidden(SaveSlotsUI screen) {
        LetGo();
        if (!Choosing) {
            // a segment that started kept the chooser's legend through the fade
            if (chooserLegend) {
                Legend(screen, true);
            }

            return;
        }

        // the slot list held segments: rebuild the saves now, while nothing is shown
        Choosing = false;
        chosen = screen.CurrentSlotIndex;
        Legend(screen, true);
        SaveSlotsManager.PrepareSlots();
        screen.ItemsUI.Refresh();
        if (savedIndex >= 0) {
            screen.SetCurrentItemAndScroll(savedIndex);
            screen.ItemsUI.SnapScroll();
            savedIndex = -1;
        }
    }

    // on segment cards the copy entry is Edit, set per card by EditLegend, and backups is Variants
    private static void Legend(SaveSlotsUI screen, bool saves) {
        chooserLegend = !saves;
        var legend = LegendOf(screen);
        if (legend == null) {
            return;
        }

        var copy = legend.FindChild("copy");
        var copyBox = copy == null ? null : copy.GetComponentInChildren<MessageBox>(true);
        if (copy != null) {
            copy.gameObject.SetActive(saves);
        }

        if (saves && copyBox != null && screen.CopyLegendMessageProvider != null) {
            copyBox.SetMessageProvider(screen.CopyLegendMessageProvider);
        }

        editWords = null;

        // a segment is a file: delete means the same thing it means for a save
        var erase = legend.FindChild("delete");
        if (erase != null) {
            erase.gameObject.SetActive(true);
        }

        var backups = legend.FindChild("backups");
        var box = backups == null ? null : backups.GetComponentInChildren<MessageBox>(true);
        if (box == null) {
            return;
        }

        if (saves) {
            if (backupsLegend != null) {
                box.SetMessageProvider(backupsLegend);
            }
        } else {
            if (backupsLegend == null) {
                backupsLegend = box.MessageProvider;
            }

            if (variantsLabel == null) {
                variantsLabel = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
                variantsLabel.SetMessage("[SaveSlotBackup] Variants");
            }

            box.SetMessageProvider(variantsLabel);
        }
    }

    // the legend is a sibling: the component lives one level under the screen
    private static Transform LegendOf(SaveSlotsUI screen) {
        var root = screen.transform.parent != null ? screen.transform.parent : screen.transform;
        var legend = root.FindChild("legend");
        if (legend == null) {
            Randomizer.log("practice: no legend under " + root.name);
        }

        return legend;
    }

    // Edit's hint: pressed for a folder, held for a given file, and none on the new-segment card
    private static void EditLegend(SaveSlotsUI screen) {
        var index = screen.CurrentSlotIndex;
        var file = index >= 0 && index < Files.Count ? Files[index] : null;
        var words = file == null ? "" : file.IsFolder ? "[SaveSlotCopy] Edit"
            : "[SaveSlotCopy]" + RandomizerKeyIcons.Thin + "(Hold) Extract & Edit";
        if (words == editWords) {
            return;
        }

        editWords = words;
        var legend = LegendOf(screen);
        var copy = legend == null ? null : legend.FindChild("copy");
        if (copy == null) {
            return;
        }

        copy.gameObject.SetActive(words.Length > 0);
        var box = copy.GetComponentInChildren<MessageBox>(true);
        if (box != null && words.Length > 0) {
            if (editLabel == null) {
                editLabel = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
            }

            editLabel.SetMessage(words);
            box.SetMessageProvider(editLabel);
        }
    }

    // what the copy entry says on the chooser, "" when hidden; null until set
    private static string editWords;

    // the legend is the chooser's, and the saves want theirs back
    private static bool chooserLegend;

    private static RandomizerMessageProvider editLabel;

    private static RandomizerMessageProvider variantsLabel;

    // after a card applies its slot; cards number by position (slot 51 on disk is card 2)
    public static void Decorate(SaveSlotUI card, int position) {
        if (card == null) {
            return;
        }

        var number = "*" + (position + 1) + ":* ";
        if (Choosing) {
            var file = position < Files.Count ? Files[position] : null;
            Bare(card, true);
            if (file == null) {
                card.IsSuspended = true;
                card.EmptySlot.SetMessage(new MessageDescriptor(Waiting
                    ? "NEW SEGMENT\nwaiting for base file selection"
                    : "NEW SEGMENT\npick a starting save on the editor page"));
                return;
            }

            // Up/Down unfold the variants; a segment without any has nothing to unfold
            card.IsSuspended = file.Variants.Count == 0;
            if (card.SaveSlot == null) {
                card.EmptySlot.SetMessage(new MessageDescriptor(number + Name(file) + " (unreadable)"));
                return;
            }

            card.AreaName.SetMessage(new MessageDescriptor(number + Name(file)));
            var unfinished = position < Ends.Count && !Ends[position];
            card.Time.SetMessage(new MessageDescriptor(Tally(file, unfinished)));
            if (card.Difficulty) {
                var variants = file.Variants.Count;
                card.Difficulty.SetMessage(new MessageDescriptor(unfinished ? "Unfinished"
                    : variants > 0 ? variants + (variants == 1 ? " variant" : " variants")
                    : (Average(file) ? "Average time" : "Best time")));
            }

            return;
        }

        Bare(card, false);
        card.IsSuspended = false;
        if (!PracticeController.Active) {
            return;
        }

        var info = card.SaveSlot;
        if (info == null) {
            card.EmptySlot.SetMessage(new MessageDescriptor(number + card.EmptySlotTextMessageProvider));
        } else {
            card.AreaName.SetMessage(new MessageDescriptor(number
                + SaveSlotsScreenshotManager.Instance.FindAreaName(info.AreaName) + " - " + info.Completion + "%"));
        }
    }

    // the variants as a card's backup rows; rows lay out by descending Order, so it counts down
    public static SaveSlotBackup Backup(int index) {
        var backup = new SaveSlotBackup(index);
        backup.IsLoaded = true;
        var file = index < Files.Count ? Files[index] : null;
        var info = file == null ? null : SaveSlotsManager.SlotByIndex(index);
        if (file == null || info == null) {
            return backup;
        }

        var variants = file.Variants;
        backup.SaveSlotInfos = new SaveSlotBackupInfo[variants.Count];
        backup.Count = variants.Count;
        for (var i = 0; i < variants.Count; i++) {
            var row = new SaveSlotInfo(info);
            row.Order = variants.Count - 1 - i;
            backup.SaveSlotInfos[i] = new SaveSlotBackupInfo(i, row);
        }

        return backup;
    }

    // the rows print a save's time and area until told the variant's name
    public static void DecorateRows(int index) {
        var screen = SaveSlotsUI.Instance;
        var file = index < Files.Count ? Files[index] : null;
        if (screen == null || file == null || index >= screen.Items.Count || screen.Items[index] == null) {
            return;
        }

        foreach (var row in screen.Items[index].GetComponentsInChildren<BackupSaveSlotUI>(true)) {
            if (row.Index < 0 || row.Index >= file.Variants.Count) {
                continue;
            }

            var variant = file.Variants[row.Index];
            var best = Best(file, variant);
            row.AreaName.SetMessage(new MessageDescriptor(VariantName(file, variant)
                + (best < 0 ? "" : " - " + PracticeController.Clock(best))));
        }
    }

    // A segment has no cells to show: the row is one line about how it has gone, centred.
    private static void Bare(SaveSlotUI card, bool segment) {
        var box = card.Time;
        if (box == null) {
            return;
        }

        var row = box.transform.parent;
        if (row != null) {
            foreach (var name in new[] { "healthIcon", "healthText", "energyIcon", "energyText" }) {
                var part = row.FindChild(name);
                if (part != null) {
                    part.gameObject.SetActive(!segment);
                }
            }
        }

        var text = box.GetComponent<TextBox>();
        if (text != null) {
            text.horizontalAnchor = segment ? HorizontalAnchorMode.Center : HorizontalAnchorMode.Right;
            text.alignment = segment ? AlignmentMode.Center : AlignmentMode.Right;
            // a clock's worth of room wraps the run count onto a second line
            text.width = segment ? 11f : 4f;
        }

        var at = box.transform.localPosition;
        box.transform.localPosition = new Vector3(segment ? 0f : TimeX, at.y, at.z);
    }

    // where the time sits on a save card, which is the right of the row
    private const float TimeX = -0.768f;

    // how it has gone: nothing to run yet, nothing run yet, or the count and the best of them
    private static string Tally(BfrpFile file, bool unfinished) {
        if (unfinished) {
            return file.IsFolder ? "click to edit" : "extract to edit";
        }

        var runs = 0;
        var best = -1L;
        foreach (var each in file.Variants.Count > 0 ? file.Variants : Unnamed) {
            foreach (var run in file.RunsFor(each)) {
                runs++;
                if (best < 0 || run.Ms < best) {
                    best = run.Ms;
                }
            }
        }

        if (runs == 0) {
            return "no runs yet";
        }

        return runs + (runs == 1 ? " run" : " runs") + "     PB " + PracticeController.Clock(best);
    }

    private static readonly List<string> Unnamed = new List<string> { "" };

    private static string Name(BfrpFile file) {
        var name = file.Segment["name"];
        if (name.IsString && name.Str.Length > 0) {
            return name.Str;
        }

        return PracticeFolder.NameOf(file.Path);
    }

    private static string VariantName(BfrpFile file, string variant) {
        var name = file.VariantSegment(variant)["name"];
        return name.IsString && name.Str.Length > 0 ? name.Str : variant;
    }

    private static bool Average(BfrpFile file) {
        return file.Segment["timing"]["mode"].Str == "average";
    }

    // fastest run of one variant, or across all of them for the card
    private static long Best(BfrpFile file, string variant) {
        var best = -1L;
        var variants = variant != null ? new List<string> { variant }
            : (file.Variants.Count > 0 ? file.Variants : new List<string> { "" });
        foreach (var each in variants) {
            foreach (var run in file.RunsFor(each)) {
                if (best < 0 || run.Ms < best) {
                    best = run.Ms;
                }
            }
        }

        return best;
    }

    // A card runs its segment, or the variant picked; one with nothing to end it opens in the editor instead.
    public static void Choose(SaveSlotsUI screen) {
        var card = screen.CurrentSaveSlot;
        var index = screen.CurrentSlotIndex;
        if (index == Files.Count) {
            CreateNew(screen);
            return;
        }

        if (card == null || card.SaveSlot == null || index < 0 || index >= Files.Count) {
            return;
        }

        var file = Files[index];
        var variants = file.Variants;
        var picked = card.BackupIndex;
        // a variant is always a choice; pressing the card itself unfolds them
        if (variants.Count > 0 && (picked < 0 || picked >= variants.Count)) {
            Unfold(card, index);
            return;
        }

        var variant = variants.Count > 0 ? variants[picked] : "";
        var ended = Ended(file, variant);
        if (ended || file.IsFolder) {
            chosen = index;
            Start(screen, file, variant, !ended);
        } else {
            Randomizer.printInfo("Nothing ends this segment yet: extract it to edit it", 300);
        }
    }

    private static void Unfold(SaveSlotUI card, int index) {
        SaveSlotBackupsManager.RequestReadBackups(index, card.OnFinishedReadingBackups);
        if (card.BackupsAnimator) {
            card.BackupsAnimator.AnimatorDriver.ContinueForward();
        }

        card.ChangeSelectionIndex(0);
    }

    // the given file whose card Copy is held on
    private static BfrpFile held;

    private static string heldVariant;

    private static int heldIndex;

    private static float heldSince;

    // Every chooser frame: Copy opens a folder in the editor at once, and extracts a given file once held.
    public static bool Edit(SaveSlotsUI screen) {
        EditLegend(screen);
        var now = Time.realtimeSinceStartup;
        if (held == null) {
            var card = screen.CurrentSaveSlot;
            var index = screen.CurrentSlotIndex;
            if (!Core.Input.Copy.OnPressed || Core.Input.Copy.Used || card == null || card.SaveSlot == null
                    || index < 0 || index >= Files.Count) {
                return false;
            }

            Core.Input.Copy.Used = true;
            var file = Files[index];
            var variants = file.Variants;
            var picked = card.BackupIndex;
            // on the card itself rather than a variant's row, the first variant
            var variant = picked >= 0 && picked < variants.Count ? variants[picked] : variants.Count > 0 ? variants[0] : "";
            if (file.IsFolder) {
                chosen = index;
                Start(screen, file, variant, true);
                return true;
            }

            held = file;
            heldVariant = variant;
            heldIndex = index;
            heldSince = now;
            return true;
        }

        if (!Choosing || screen.CurrentSlotIndex != heldIndex) {
            LetGo();
            return false;
        }

        if (!Core.Input.Copy.IsPressed) {
            LetGo();
            return true;
        }

        var progress = (now - heldSince) / RandomizerHoldRing.Seconds;
        if (progress < 1f) {
            var legend = LegendOf(screen);
            RandomizerHoldRing.Around(RandomizerHoldRing.Glyph(legend == null ? null : legend.FindChild("copy")), progress);
            return true;
        }

        var given = held;
        LetGo();
        Extract(screen, given, heldVariant);
        return true;
    }

    private static void LetGo() {
        held = null;
        RandomizerHoldRing.Hide();
    }

    // the given file unpacked into a folder that takes its card, opened in the editor
    private static void Extract(SaveSlotsUI screen, BfrpFile file, string variant) {
        string folder;
        try {
            folder = PracticeFolder.Extract(file.Path);
        } catch (Exception e) {
            Randomizer.LogError("practice: could not extract " + file.Path + ": " + e.Message);
            return;
        }

        Randomizer.log("practice: extracted " + file.Path + " to " + folder);
        Open();
        var at = IndexOf(folder);
        if (at < 0) {
            return;
        }

        var copy = Files[at];
        var variants = copy.Variants;
        screen.SetCurrentItemAndScroll(at);
        chosen = at;
        Start(screen, copy, variants.Contains(variant) ? variant : variants.Count > 0 ? variants[0] : "", true);
    }

    // The page makes the segment; the chooser holds a CANCEL prompt meanwhile to call it off.
    private static void CreateNew(SaveSlotsUI screen) {
        PracticeServer.Open();
        // without the prompt there is nothing to call the waiting off with, so do not wait
        if (!PracticeServer.Running || !Prompt(screen, new[] { "CANCEL" }, Called)) {
            Randomizer.printInfo("The segment editor page could not be opened", 300);
            return;
        }

        Randomizer.printQuiet("The segment editor page is open in your browser: pick a starting save there", 600);
        Waiting = true;
        screen.ItemsUI.RefreshItem(screen.CurrentSlotIndex);
    }

    private static void Called(int row) {
        var screen = SaveSlotsUI.Instance;
        if (screen != null) {
            screen.ClosePrompt();
        }

        PromptCancelled();
    }

    // The page asks about this: it stops offering to make one when the game stops waiting.
    public static bool Waiting;

    private static Action<int> chose;

    // the difficulty screen with our own rows; rows past ours leave the menu so navigation skips them
    private static bool Prompt(SaveSlotsUI screen, string[] rows, Action<int> pressed) {
        var manager = screen.ShowPrompt();
        if (manager == null) {
            return false;
        }

        chose = pressed;
        var layout = manager.GetComponentInChildren<CleverMenuItemLayout>(true);
        var items = manager.MenuItems;
        for (var i = items.Count - 1; i >= 0; i--) {
            var item = items[i];
            if (item == null || i >= rows.Length) {
                items.RemoveAt(i);
                if (layout != null) {
                    layout.MenuItems.Remove(item);
                }

                if (item != null) {
                    item.gameObject.SetActive(false);
                }

                continue;
            }

            var box = item.GetComponentInChildren<MessageBox>(true);
            if (box != null) {
                box.SetMessage(new MessageDescriptor(rows[i]));
            }

            // gone this frame, not at the end of it: a highlight would print its difficulty
            var tip = item.GetComponent<CleverMenuItemTooltip>();
            if (tip != null) {
                UnityEngine.Object.DestroyImmediate(tip);
            }

            var row = i;
            item.Pressed = null;
            item.PressedCallback += delegate { Pressed(row); };
        }

        // the manager prints a tooltip as it highlights: the controller goes, and its line with it
        var tips = manager.GetComponentInChildren<CleverMenuItemTooltipController>(true);
        if (tips != null) {
            var line = tips.gameObject;
            UnityEngine.Object.DestroyImmediate(tips);
            line.SetActive(false);
        }

        if (layout != null) {
            layout.Sort();
        }

        manager.SetCurrentItem(0);
        return true;
    }

    private static void Pressed(int row) {
        var pick = chose;
        chose = null;
        if (pick != null) {
            pick(row);
        }
    }

    // Back on the prompt, or anything else that takes it down.
    public static void PromptCancelled() {
        chose = null;
        if (!Waiting) {
            return;
        }

        Waiting = false;
        // nothing waits on the page any more, so neither does the message saying so
        Randomizer.clearMessage();
        var screen = SaveSlotsUI.Instance;
        if (Choosing && screen != null && screen.CurrentSlotIndex == Files.Count) {
            screen.ItemsUI.RefreshItem(screen.CurrentSlotIndex);
        }
    }

    // Back inside the unfolded variants closes them instead of the chooser.
    public static bool Fold(SaveSlotsUI screen) {
        var card = screen.CurrentSaveSlot;
        if (card == null || card.BackupIndex < 0) {
            return false;
        }

        // the rows close however far down them the selection is
        for (var guard = 0; card.BackupIndex >= 0 && guard < 16; guard++) {
            card.ChangeSelectionIndex(-1);
        }

        if (card.BackupsAnimator) {
            card.BackupsAnimator.AnimatorDriver.ContinueBackwards();
        }

        return true;
    }

    public static bool Deletable(int index) {
        return Choosing && index >= 0 && index < Files.Count;
    }

    // named when asked, not when answered: the page can rebuild the list under the prompt
    public static void Erasing(int index) {
        erasing = Deletable(index) ? Files[index].Path : null;
    }

    private static string erasing;

    // The card's file, gone; the chooser comes back listing what is left.
    public static void Erase(SaveSlotsUI screen) {
        var index = screen.CurrentSlotIndex;
        var path = erasing;
        erasing = null;
        if (path == null || !Choosing) {
            return;
        }
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            } else {
                System.IO.File.Delete(path);
            }

            Randomizer.log("practice: deleted " + path);
        } catch (Exception e) {
            Randomizer.LogError("practice: could not delete " + path + ": " + e.Message);
            return;
        }

        chosen = Mathf.Max(0, index - 1);
        Open();
    }

    public static bool HasEnd(BfrpFile file) {
        var variants = file.Variants;
        return Ended(file, variants.Count > 0 ? variants[0] : "");
    }

    private static bool Ended(BfrpFile file, string variant) {
        try {
            return PracticeSegment.Ends(file, variant);
        } catch (Exception) {
            return true;
        }
    }

    // "New Segment N", past every N already in the folder
    public static string NextName() {
        var names = new List<string>();
        try {
            // the names on the cards, which is what the player is counting
            if (Choosing) {
                names = Files.ConvertAll(Name);
            } else if (Directory.Exists(Folder)) {
                names = PracticeFolder.List(Folder).ConvertAll(PracticeFolder.NameOf);
            }
        } catch (Exception e) {
            Randomizer.log("practice: could not number the new segment: " + e.Message);
        }

        return PracticeFolder.NextName(names);
    }

    // a new segment's folder, named after it and taken by no other folder or file
    public static string PathFor(string name) {
        if (!Directory.Exists(Folder)) {
            Directory.CreateDirectory(Folder);
        }

        return PracticeFolder.FreePath(Folder, PracticeFolder.SafeName(name));
    }

    // starts a segment the page just made, as its card would; only while the chooser is up
    public static bool StartPath(string path) {
        var screen = SaveSlotsUI.Instance;
        if (!Choosing || screen == null || PracticeController.Active) {
            return false;
        }

        // the prompt hangs off a card the re-list is about to replace
        screen.ClosePrompt();
        PromptCancelled();
        Open();
        var at = IndexOf(path);
        if (at < 0) {
            return false;
        }

        screen.SetCurrentItemAndScroll(at);
        chosen = at;
        Start(screen, Files[at], "", false);
        return true;
    }

    private static int IndexOf(string path) {
        var full = Path.GetFullPath(path).TrimEnd('\\', '/');
        return Files.FindIndex(file => string.Equals(Path.GetFullPath(file.Path).TrimEnd('\\', '/'), full, StringComparison.OrdinalIgnoreCase));
    }

    // The session seeds its slot; the title screen's own load sequence takes it from there.
    private static void Start(SaveSlotsUI screen, BfrpFile file, string variant, bool edit) {
        Choosing = false;
        PracticeController.EditNext = PracticeController.EditNext || edit;
        try {
            PracticeController.Begin(file, variant, true);
        } catch (Exception e) {
            Randomizer.LogError("practice: " + file.Path + " would not start: " + e.Message);
            Legend(screen, true);
            SaveSlotsManager.PrepareSlots();
            return;
        }

        screen.Active = false;
        screen.UsedSaveSlotPressedAction.Perform(null);
    }
}

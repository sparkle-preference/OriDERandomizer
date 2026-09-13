using System;
using System.Collections.Generic;
using System.IO;
using CatlikeCoding.TextBox;
using UnityEngine;

// The practice chooser: the file select with a segment on every card. A container's
// save.sav is a real save, so the card's area, health, energy and art are the segment's
// own start; only the name, time and variant line are written over. Variants use the
// rows a card unfolds for its backups: Up opens them, Up/Down picks one.
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
        }

        if (reopen && screen == TitleScreenManager.Screen.MainMenu) {
            reopen = false;
            Open();
        }

        // A run parked behind the title is picked back up by loading its slot, so the menu
        // opens pointing at the way there rather than at whatever was last used.
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

    private static CleverMenuItemSelectionManager mainMenu;

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
        if (exitScreen == null || exitScreen.MenuItems.Count < 2) {
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
        menu.AddMenuItem(PracticeController.Active ? "CONTINUE PRACTICE" : "PRACTICE", 2, Open);
        var item = menu.MenuItems[2];
        // AddMenuItem reparents keeping world scale, and its clone of "start game" keeps
        // that entry's own press
        item.transform.localScale = template.transform.localScale;
        item.Pressed = null;
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

            var paths = Directory.GetFiles(Folder, "*.bfrp");
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths) {
                try {
                    Files.Add(BfrpFile.Load(path));
                } catch (Exception e) {
                    Randomizer.log("practice: skipped " + path + ": " + e.Message);
                }
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

        // the last card is empty on purpose: it makes a new segment
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
            // the run's own slot, the middle card: proceed twice from the title is the way
            // back into a quit-out
            screen.SetCurrentItemAndScroll(PracticeController.LoadedSlot - PracticeController.FirstSlot);
            screen.ItemsUI.SnapScroll();
        }
    }

    // The screen keeps the saves' scroll position; a short segment list seen from slot 7 looks
    // empty. The chooser keeps its own, so it comes back on the segment you left it on.
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
        if (!Choosing) {
            return;
        }

        // the slot list was the segments; give the next screen the saves back, rebuilt
        // now, while nothing is shown
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

    // copy and delete mean nothing on a segment card, and the backups row is the variants
    private static void Legend(SaveSlotsUI screen, bool saves) {
        // the legend is a sibling: the component lives one level under the screen
        var root = screen.transform.parent != null ? screen.transform.parent : screen.transform;
        var legend = root.FindChild("legend");
        if (legend == null) {
            Randomizer.log("practice: no legend under " + root.name);
            return;
        }

        var copy = legend.FindChild("copy");
        if (copy != null) {
            copy.gameObject.SetActive(saves);
        }

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

            box.SetMessage(new MessageDescriptor("Variants"));
        }
    }

    // Called after a card applies its slot. Cards are numbered by position: a practice
    // slot is 51 on disk and 2 on screen.
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

    // The variants as a card's backup list: one row each, the first nearest the card.
    // Rows are laid out by descending Order, so the order counts down.
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
            return "click to edit";
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

        return Path.GetFileNameWithoutExtension(file.Path);
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
        var variant = "";
        if (variants.Count > 0) {
            // a variant is always a choice; pressing the card itself unfolds them
            var picked = card.BackupIndex;
            if (picked < 0 || picked >= variants.Count) {
                SaveSlotBackupsManager.RequestReadBackups(index, card.OnFinishedReadingBackups);
                if (card.BackupsAnimator) {
                    card.BackupsAnimator.AnimatorDriver.ContinueForward();
                }

                card.ChangeSelectionIndex(0);
                return;
            }

            variant = variants[picked];
        }

        chosen = index;
        // a segment with nothing to end it has nothing to run: it goes straight to the editor
        if (!Ended(file, variant)) {
            Start(screen, file, variant, true);
            return;
        }

        pending = file;
        pendingVariant = variant;
        Prompt(screen, new[] { "RUN", "EDIT" }, RunOrEdit);
    }

    private static void RunOrEdit(int row) {
        var screen = SaveSlotsUI.Instance;
        var file = pending;
        pending = null;
        if (screen == null || file == null) {
            return;
        }

        screen.ClosePrompt();
        Start(screen, file, pendingVariant, row == 1);
    }

    // The page does the making: it lists the game's saves and asks for a name. The chooser
    // holds a prompt meanwhile, so the screen says what it is waiting for and can call it off.
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

    private static BfrpFile pending;

    private static string pendingVariant = "";

    private static Action<int> chose;

    // The difficulty screen with our own rows: two words about the segment you just picked.
    // Rows past the ones we want leave the menu entirely, so navigation cannot land on them.
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
        pending = null;
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

    // Named when the prompt goes up, not when it is answered: the list underneath can be
    // rebuilt by the page while the question is on screen.
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
            System.IO.File.Delete(path);
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
            return PracticeSegment.Parse(file, variant).HasEnd;
        } catch (Exception) {
            return true;
        }
    }

    // "New Segment N", past every N already in the folder
    public static string NextName() {
        var taken = 0;
        try {
            // the names on the cards, which is what the player is counting
            if (Choosing) {
                foreach (var file in Files) {
                    taken = Math.Max(taken, Numbered(Name(file)));
                }

                return "New Segment " + (taken + 1);
            }

            if (Directory.Exists(Folder)) {
                foreach (var path in Directory.GetFiles(Folder, "*.bfrp")) {
                    taken = Math.Max(taken, Numbered(Path.GetFileNameWithoutExtension(path)));
                }
            }
        } catch (Exception e) {
            Randomizer.log("practice: could not number the new segment: " + e.Message);
        }

        return "New Segment " + (taken + 1);
    }

    // The N in "New Segment N", or zero for a name that is not one. A trailing " (2)" is the
    // file name dodging one already taken, not a segment of its own.
    private static int Numbered(string name) {
        var bracket = name.IndexOf(" (");
        var stem = bracket > 0 ? name.Substring(0, bracket) : name;
        int n;
        return stem.StartsWith("New Segment ", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(stem.Substring("New Segment ".Length).Trim(), out n) ? n : 0;
    }

    // a file name from a segment name, unique in the folder
    public static string PathFor(string name) {
        var safe = name;
        foreach (var bad in Path.GetInvalidFileNameChars()) {
            safe = safe.Replace(bad, '-');
        }

        if (!Directory.Exists(Folder)) {
            Directory.CreateDirectory(Folder);
        }

        var path = Path.Combine(Folder, safe + ".bfrp");
        for (var n = 2; System.IO.File.Exists(path); n++) {
            path = Path.Combine(Folder, safe + " (" + n + ").bfrp");
        }

        return path;
    }

    // A segment the page just made, started the way its card would start it. Only from the
    // chooser: anywhere else the file simply waits in the list.
    public static bool StartPath(string path) {
        var screen = SaveSlotsUI.Instance;
        if (!Choosing || screen == null || PracticeController.Active) {
            return false;
        }

        // the prompt hangs off a card the re-list is about to replace
        screen.ClosePrompt();
        PromptCancelled();
        Open();
        for (var i = 0; i < Files.Count; i++) {
            if (string.Equals(Path.GetFullPath(Files[i].Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) {
                screen.SetCurrentItemAndScroll(i);
                chosen = i;
                Start(screen, Files[i], "", false);
                return true;
            }
        }

        return false;
    }

    // The session seeds its slot; the title screen's own load sequence takes it from there.
    private static void Start(SaveSlotsUI screen, BfrpFile file, string variant, bool edit) {
        Choosing = false;
        Legend(screen, true);
        PracticeController.EditNext = PracticeController.EditNext || edit;
        try {
            PracticeController.Begin(file, variant, true);
        } catch (Exception e) {
            Randomizer.LogError("practice: " + file.Path + " would not start: " + e.Message);
            SaveSlotsManager.PrepareSlots();
            return;
        }

        screen.Active = false;
        screen.UsedSaveSlotPressedAction.Perform(null);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
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

    public static int Count => Files.Count;

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

            box.SetMessage(new MessageDescriptor("EXIT PRACTICE SESSION"));
        } else if (exitLabel != null) {
            box.SetMessageProvider(exitLabel);
            exitLabel = null;
        }
    }

    private static CleverMenuItemSelectionManager mainMenu;

    private static MessageProvider exitLabel;

    private static bool exitLabelled;

    private static void Arm() {
        if (exitScreen == null || exitScreen.MenuItems.Count < 2) {
            return;
        }

        try {
            foreach (var box in exitScreen.GetComponentsInChildren<MessageBox>(true)) {
                if (box.GetComponentInParent<CleverMenuItem>() == null) {
                    exitTitle = box;
                    exitQuestion = box.MessageProvider;
                    box.SetMessage(new MessageDescriptor("End the practice session?"));
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
        foreach (var file in Files) {
            slots.Add(Info(file));
        }

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
            Rewind(screen);
        }
    }

    // The screen keeps the saves' scroll position; a short segment list seen from slot 7 looks empty.
    private static void Rewind(SaveSlotsUI screen) {
        if (savedIndex < 0) {
            savedIndex = screen.CurrentSlotIndex;
        }

        screen.SetCurrentItemAndScroll(0);
    }

    private static int savedIndex = -1;

    public static void Hidden(SaveSlotsUI screen) {
        if (!Choosing) {
            return;
        }

        // the slot list was the segments; give the next screen the saves back, rebuilt
        // now, while nothing is shown
        Choosing = false;
        Legend(screen, true);
        SaveSlotsManager.PrepareSlots();
        screen.ItemsUI.Refresh();
        if (savedIndex >= 0) {
            screen.SetCurrentItemAndScroll(savedIndex);
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

        foreach (var name in new[] { "copy", "delete" }) {
            var entry = legend.FindChild(name);
            if (entry != null) {
                entry.gameObject.SetActive(saves);
            }
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
            if (file == null) {
                return;
            }

            // Up/Down unfold the variants; a segment without any has nothing to unfold
            card.IsSuspended = file.Variants.Count == 0;
            if (card.SaveSlot == null) {
                card.EmptySlot.SetMessage(new MessageDescriptor(number + Name(file) + " (unreadable)"));
                return;
            }

            card.AreaName.SetMessage(new MessageDescriptor(number + Name(file)));
            var best = Best(file, null);
            card.Time.SetMessage(new MessageDescriptor(best < 0 ? "no runs yet" : "best " + PracticeController.Clock(best)));
            if (card.Difficulty) {
                var variants = file.Variants.Count;
                card.Difficulty.SetMessage(new MessageDescriptor(variants > 0
                    ? variants + (variants == 1 ? " variant" : " variants")
                    : (Average(file) ? "Average time" : "Best time")));
            }

            return;
        }

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
        if (card == null || card.SaveSlot == null || index < 0 || index >= Files.Count) {
            return;
        }

        var file = Files[index];
        var variants = file.Variants;
        if (variants.Count == 0) {
            Start(screen, file, "");
            return;
        }

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

        Start(screen, file, variants[picked]);
    }

    // The session seeds its slot; the title screen's own load sequence takes it from there.
    private static void Start(SaveSlotsUI screen, BfrpFile file, string variant) {
        Choosing = false;
        Legend(screen, true);
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

using System;
using System.Collections.Generic;
using fsm;
using Game;
using UnityEngine;

// The pause menu (the inventory screen) dressed for a session: its Difficulty row
// becomes Retry, its cutscene Skip row becomes Pin once a run has finished, Continue
// goes with it, and Exit reads as the way back to the chooser. Rows are re-dressed
// rather than added: the screen navigates by a cage of edges, and a new row would have
// none. A segment that allows quitting to the menu keeps the vanilla screen for the
// player's muscle memory and reaches this one by its keybind. Undressed on every show.
public static class PracticeMenu {
    private class Row {
        public CleverMenuItem Item;
        public ActionMethod Pressed;
        public Condition Visible;
        public Condition Activated;
        public bool WasInactive;
        public PracticeHiddenCondition Hidden;
        public readonly List<MessageBox> Boxes = new List<MessageBox>();
        public readonly List<MessageProvider> Providers = new List<MessageProvider>();
        public Action Handler;
    }

    private static readonly List<Row> dressed = new List<Row>();

    private static CleverMenuItem pinItem;

    private static RandomizerMessageProvider retryLabel;

    // the next show is dressed whatever the segment says
    private static bool wanted;

    // Exit was pressed from the dressed menu, so the prompt's OK ends the session
    private static bool exitRequested;

    public static void OnPauseShown(InventoryManager screen) {
        try {
            Restore(screen);
            var segment = PracticeController.Segment;
            if (PracticeController.Active && (wanted || segment == null || !segment.QuitToMenu)) {
                Decorate(screen);
            }
        } catch (Exception e) {
            Randomizer.LogError("practice menu: " + e.Message);
        }

        wanted = false;
    }

    // from the keybind, and the finish line
    public static void Open() {
        if (!PracticeController.Active || Game.UI.Menu == null || Game.UI.MainMenuVisible
                || GameController.Instance == null || GameController.Instance.GameInTitleScreen) {
            return;
        }

        try {
            wanted = true;
            if (InventoryManager.Instance == null) {
                Randomizer.log("practice menu: no inventory screen yet, the cutscene pause will show instead");
            }

            Game.UI.Menu.ShowInventoryOrPauseMenu();
        } catch (Exception e) {
            wanted = false;
            Randomizer.LogError("practice menu: " + e.Message);
        }
    }

    public static bool TakeExitRequest() {
        var requested = exitRequested;
        exitRequested = false;
        return requested;
    }

    private static void Decorate(InventoryManager screen) {
        var manager = screen.NavigationManager;
        var finished = PracticeController.Current == PracticeController.Phase.Finished;

        // the row's text is its value box, rewritten from its provider every frame
        if (retryLabel == null) {
            retryLabel = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
            retryLabel.SetMessage("RETRY");
        }

        var retry = Take(manager, "difficulty", Retry, false);
        if (retry != null && screen.Difficulty != null) {
            retry.Boxes.Add(screen.Difficulty);
            retry.Providers.Add(screen.Difficulty.MessageProvider);
            screen.Difficulty.SetMessageProvider(retryLabel);
            foreach (var box in retry.Item.GetComponentsInChildren<MessageBox>(true)) {
                if (box != screen.Difficulty) {
                    retry.Boxes.Add(box);
                    retry.Providers.Add(box.MessageProvider);
                    box.SetMessage(new MessageDescriptor(""));
                }
            }
        }

        // Mid-run the vanilla press stays, prompt and all; a finished run leaves without
        // being asked, along the same path.
        var exit = finished ? Take(manager, "exit", ExitNow, false) : Take(manager, "exit", RequestExit, true);
        Label(exit, "EXIT TO PRACTICE MENU");

        // the cutscene skip row is the practice row: edit mid-run, save while editing,
        // pin once the run is over
        if (PracticeController.Current == PracticeController.Phase.Editing) {
            Label(Take(manager, "skip", SaveAndRetry, false), "SAVE AND RETRY");
        } else if (!finished) {
            Label(Take(manager, "skip", Edit, false), "EDIT PRACTICE SEGMENT");
        }

        if (finished) {
            var pin = Take(manager, "skip", Pin, false);
            Label(pin, "PIN GHOST");
            pinItem = pin == null ? null : pin.Item;
            // the run is over: the skill wheel's column is the tally's now
            HideColumn(screen);
            PracticeHud.ShowTally(PracticeController.LastTally ?? "");
        }

        // What is left is help and options, which a run keeps out of reach; a finished
        // run has no Resume either. The layout lists the rows, the manager everything.
        var layout = manager.GetComponentInChildren<CleverMenuItemLayout>(true);
        if (layout != null) {
            foreach (var item in layout.MenuItems) {
                if (item != null && item.gameObject.activeSelf && !Taken(item) && (finished || item.gameObject.name != "continue")) {
                    Hide(item);
                }
            }
        }

        // the editor row goes last, under Exit
        Arrange(manager, finished ? null : Find(manager, "skip"));
        Relayout(manager);
    }

    // the tally is the finish screen's; Back takes both away until the screen is back
    public static void OnPauseHidden() {
        if (PracticeController.Current == PracticeController.Phase.Finished) {
            PracticeHud.HideTally();
        }
    }

    private static readonly List<GameObject> hiddenObjects = new List<GameObject>();

    // the left third of the screen, and the skill wheel's items wherever they sit
    private static void HideColumn(InventoryManager screen) {
        foreach (Transform child in screen.transform) {
            if (child.gameObject.activeSelf && child.position.x < -2f) {
                hiddenObjects.Add(child.gameObject);
                child.gameObject.SetActive(false);
            }
        }

        foreach (var item in screen.GetComponentsInChildren<InventoryAbilityItem>(true)) {
            if (item.gameObject.activeSelf) {
                hiddenObjects.Add(item.gameObject);
                item.gameObject.SetActive(false);
            }
        }
    }


    private static List<CleverMenuItem> layoutOrder;

    private static List<CleverMenuItemSelectionManager.NavigationData> navigation;

    // The layout lists rows top to bottom, so moving one is moving it in that list.
    // The cage's edges are replaced with plain up/down ones between the visible rows:
    // an edge into a hidden row is a dead end.
    private static void Arrange(CleverMenuItemSelectionManager manager, CleverMenuItem bottom) {
        var layout = manager.GetComponentInChildren<CleverMenuItemLayout>(true);
        if (layout == null) {
            return;
        }

        if (layoutOrder == null) {
            layoutOrder = new List<CleverMenuItem>(layout.MenuItems);
            navigation = manager.Navigation;
        }

        if (bottom != null && layout.MenuItems.Remove(bottom)) {
            layout.MenuItems.Add(bottom);
        }

        var edges = new List<CleverMenuItemSelectionManager.NavigationData>();
        CleverMenuItem above = null;
        CleverMenuItem first = null;
        foreach (var item in layout.MenuItems) {
            if (item == null || !item.IsVisible || !item.gameObject.activeSelf) {
                continue;
            }

            if (above != null) {
                edges.Add(new CleverMenuItemSelectionManager.NavigationData { From = above, To = item });
                edges.Add(new CleverMenuItemSelectionManager.NavigationData { From = item, To = above });
            }

            if (first == null) {
                first = item;
            }

            above = item;
        }

        manager.Navigation = edges;
        // The screen picks its opening row as the first activated one in the manager's
        // list, which is not the layout's order; the top visible row goes first there.
        if (first != null && manager.MenuItems.Count > 0 && manager.MenuItems[0] != first) {
            if (managerOrder == null) {
                managerOrder = new List<CleverMenuItem>(manager.MenuItems);
            }

            manager.MenuItems.Remove(first);
            manager.MenuItems.Insert(0, first);
        }

        var current = manager.CurrentMenuItem;
        if (first != null && (current == null || !current.IsVisible || !current.gameObject.activeSelf)) {
            manager.SetCurrentMenuItem(first);
        }
    }

    private static List<CleverMenuItem> managerOrder;

    private static void Unarrange(CleverMenuItemSelectionManager manager) {
        if (layoutOrder == null) {
            return;
        }

        var layout = manager.GetComponentInChildren<CleverMenuItemLayout>(true);
        if (layout != null) {
            layout.MenuItems.Clear();
            layout.MenuItems.AddRange(layoutOrder);
        }

        manager.Navigation = navigation;
        if (managerOrder != null) {
            manager.MenuItems.Clear();
            manager.MenuItems.AddRange(managerOrder);
            managerOrder = null;
        }

        layoutOrder = null;
        navigation = null;
    }

    private static bool Taken(CleverMenuItem item) {
        foreach (var row in dressed) {
            if (row.Item == item) {
                return true;
            }
        }

        return false;
    }

    // the layout closes the gap, and the cage walks past a row that is not visible
    private static void Hide(CleverMenuItem item) {
        var row = new Row();
        row.Item = item;
        row.Pressed = item.Pressed;
        row.Visible = item.Visible;
        row.Activated = item.Activated;
        row.Hidden = item.gameObject.AddComponent<PracticeHiddenCondition>();
        item.Visible = row.Hidden;
        // the screen's own SetIndexToFirst skips a row that is not activated
        item.Activated = row.Hidden;
        dressed.Add(row);
    }

    // Takes a row for the session: its press replaced or kept, a hiding condition
    // lifted, its text remembered for later.
    private static Row Take(CleverMenuItemSelectionManager manager, string name, Action handler, bool keepPress) {
        var item = Find(manager, name);
        if (item == null) {
            return null;
        }

        var row = new Row();
        row.Item = item;
        row.Pressed = item.Pressed;
        row.Visible = item.Visible;
        row.Activated = item.Activated;
        row.Handler = handler;
        row.WasInactive = !item.gameObject.activeSelf;
        if (!keepPress) {
            item.Pressed = null;
        }

        item.Visible = null;
        item.Activated = null;
        item.gameObject.SetActive(true);
        if (handler != null) {
            item.PressedCallback += handler;
        }

        dressed.Add(row);
        return row;
    }

    private static void Label(Row row, string label) {
        if (row == null) {
            return;
        }

        var box = row.Item.GetComponentInChildren<MessageBox>(true);
        if (box != null) {
            row.Boxes.Add(box);
            row.Providers.Add(box.MessageProvider);
            box.SetMessage(new MessageDescriptor(label));
        }
    }

    private static void Restore(InventoryManager screen) {
        foreach (var row in dressed) {
            if (row.Item == null) {
                continue;
            }

            if (row.Handler != null) {
                row.Item.PressedCallback -= row.Handler;
            }

            row.Item.Pressed = row.Pressed;
            row.Item.Visible = row.Visible;
            row.Item.Activated = row.Activated;
            if (row.WasInactive) {
                row.Item.gameObject.SetActive(false);
            }

            if (row.Hidden != null) {
                UnityEngine.Object.Destroy(row.Hidden);
                row.Item.gameObject.SetActive(true);
            }

            for (var i = 0; i < row.Boxes.Count; i++) {
                if (row.Boxes[i] != null && row.Providers[i] != null) {
                    row.Boxes[i].SetMessageProvider(row.Providers[i]);
                }
            }
        }

        foreach (var hidden in hiddenObjects) {
            if (hidden != null) {
                hidden.SetActive(true);
            }
        }

        hiddenObjects.Clear();
        Unarrange(screen.NavigationManager);
        if (dressed.Count > 0) {
            Relayout(screen.NavigationManager);
        }

        dressed.Clear();
        pinItem = null;
        exitRequested = false;
    }

    private static void Relayout(CleverMenuItemSelectionManager manager) {
        var layout = manager.GetComponentInChildren<CleverMenuItemLayout>(true);
        if (layout != null) {
            layout.Sort();
        }

        manager.RefreshVisible();
    }

    private static CleverMenuItem Find(CleverMenuItemSelectionManager manager, string name) {
        foreach (var item in manager.MenuItems) {
            if (item != null && item.gameObject.name == name) {
                return item;
            }
        }

        Randomizer.log("practice menu: no pause row named " + name);
        return null;
    }

    private static void Retry() {
        // immediate: a fading menu is frozen mid-fade by the countdown
        Game.UI.Menu.HideMenuScreen(true);
        PracticeController.Retry();
    }

    private static void Pin() {
        var box = pinItem == null ? null : pinItem.GetComponentInChildren<MessageBox>(true);
        if (box != null) {
            box.SetMessage(new MessageDescriptor(PracticeController.PinLastGhost() ? "GHOST PINNED" : "NO RUN TO PIN"));
        }
    }

    private static void RequestExit() {
        exitRequested = true;
    }

    private static void ExitNow() {
        PracticeController.ReturnToTitle(true);
    }

    private static void Edit() {
        Game.UI.Menu.HideMenuScreen(true);
        PracticeEditor.Begin();
    }

    private static void SaveAndRetry() {
        Game.UI.Menu.HideMenuScreen(true);
        PracticeEditor.SaveAndRetry();
    }
}

// a row's Visible condition that says no
public class PracticeHiddenCondition : Condition {
    public override bool Validate(IContext context) {
        return false;
    }
}

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public abstract class CustomSettingsScreen : MonoBehaviour {
    public void OnEnable() {
        FitOptional();
        // each visit is its own baseline, so keeping changes and coming back starts clean
        SnapshotBinds();
        // the file can be reloaded under a page built earlier
        foreach (var refresh in rowRefresh) {
            refresh();
        }

        // a flyout open when the screen closed still has the tooltip
        RestoreTooltip();
        // its key caps follow binds changed since it was drawn
        BindLegend();
    }

    public void OnDisable() {
        Hold(false);
        saving = -1f;
        HideHold();
        // Will only write if there have been changes
        RandomizerSettings.WriteSettings();
    }

    // Escape is also Pause, which the menu manager reads first. Hold it and the tab list off
    // during a prompt or edit and SettleFrames after: the manager sees that press a frame late.
    public void HoldMenu() {
        if (Editing || prompt != null) {
            settle = SettleFrames;
        } else if (settle > 0) {
            settle--;
        }

        Hold(prompt != null || Editing || settle > 0 || (selectionManager.IsActive && BindsDirty));
        // the cursor must not move the selection, or swap the tooltip, under an edit or prompt
        selectionManager.IsLocked = prompt != null || Editing;
    }

    private void Hold(bool held) {
        if (Game.UI.Menu != null) {
            Game.UI.Menu.IsSuspended = held;
        }

        if (OptionsScreen.Instance != null) {
            OptionsScreen.Instance.Navigation.IsLocked = held;
        }
    }

    public virtual void Awake() {
        layout = GetComponent<CleverMenuItemLayout>();
        selectionManager = GetComponent<CleverMenuItemSelectionManager>();
        group = GetComponent<CleverMenuItemGroup>();
        layout.MenuItems.Clear();
        selectionManager.MenuItems.Clear();
        group.Options.Clear();
        pivot = transform.FindChild("highlightFade/pivot");
        foreach (var obj in pivot) {
            Destroy(((Transform)obj).gameObject);
        }

        var componentsInChildren = GetComponentsInChildren<TransparencyAnimator>();
        for (var i = 0; i < componentsInChildren.Length; i++) {
            if (componentsInChildren[i].gameObject != gameObject) {
                componentsInChildren[i].Reset();
            }
        }

        var originalToolip = SettingsScreen.Instance.transform.Find("highlightFade/pivot/tooltip");
        var tooltip = Instantiate(originalToolip);
        tooltip.SetParent(pivot);
        tooltip.position = originalToolip.position;
        tooltipController = tooltip.GetComponent<CleverMenuItemTooltipController>();
        tooltipController.Selection = selectionManager;
        tooltipController.UpdateTooltip();
        tooltipController.enabled = true;

        InitScreen();
        // skips a leading header
        selectionManager.SetIndexToFirst();
        selectionManager.BackGuard = KeepOrDiscard;
        // any move of the selection chooses its row, a mouse hover included
        selectionManager.OptionChangeCallback += delegate { chosenIn = menuSession; };
        selectionManager.EnterRow = ReturnRow;
    }

    // Entering returns to the page's row if one was chosen since the options screen opened, else to
    // the first row the window shows. A row that left the page gives way to its nearest neighbor.
    private int ReturnRow() {
        if (chosenIn != menuSession) {
            return -1;
        }

        return selectionManager.NavigableFrom(selectionManager.Index, 0, selectionManager.MenuItems.Count - 1);
    }

    // the options screen went away, from the pause menu or the title: every page forgets its row
    public static void MenusClosed() {
        menuSession++;
    }

    // Back from a dirty page asks first; either answer clears the change, then lands the back.
    private bool KeepOrDiscard() {
        if (prompt != null) {
            return false;
        }

        if (!BindsDirty) {
            return true;
        }

        Confirm("Save keybinding changes?", new[] { "SAVE", "DISCARD" }, answer => {
            // Back on the prompt: stay on the page
            if (answer < 0) {
                return;
            }

            if (answer == 0) {
                SnapshotBinds();
            } else {
                RevertBinds();
            }

            selectionManager.OnBackPressed();
        });
        return false;
    }

    public void AddKeybind(string label, Func<KeyCode[]> getKeys, Action<KeyCode[]> setKeys) {
        var cleverMenuItem = AddItem(label);
        cleverMenuItem.gameObject.name = "Keybind (" + label + ")";
        var kc = cleverMenuItem.gameObject.AddComponent<KeybindControl>();
        kc.Init(getKeys, setKeys, this, label);
        cleverMenuItem.PressedCallback += delegate { kc.BeginEditing(); };
    }

    public abstract void InitScreen();

    // Call from InitScreen; the window re-sorts on each selection change.
    public void ScrollAfter(int rows) {
        // the rows are final by now, so the controls are cached
        keyControls = GetComponentsInChildren<KeybindControl>(true);
        padControls = GetComponentsInChildren<ControllerBindControl>(true);
        randoControls = GetComponentsInChildren<RandomizerBindControl>(true);
        layout.MaxVisible = rows;
        layout.Selection = selectionManager;
        layout.EdgeFade = EdgeFade;
        selectionManager.OptionChangeCallback += layout.Sort;
        layout.Sort();
        BuildScrollbar();
        selectionManager.OptionChangeCallback += PlaceScrollbar;
        PlaceScrollbar();
    }

    // The track spans the window; the thumb's length is the share of the list on screen.
    private void BuildScrollbar() {
        barLength = (layout.MaxVisible - 1) * layout.MenuItems[0].Space;
        scrollBar = new GameObject("scrollbar").transform;
        scrollBar.SetParent(pivot, false);
        scrollBar.localPosition = new Vector3(BarX, -0.5f * barLength, 0f);

        var order = layout.MenuItems[0].GetComponentInChildren<Renderer>();
        scrollTrack = Bar("scrollTrack", "scrollbar_track.png", order);
        scrollThumb = Bar("scrollThumb", "scrollbar_thumb.png", order);
        if (scrollTrack != null) {
            scrollTrack.localScale = new Vector3(BarWidth, barLength, 1f);
        }
    }

    private Transform Bar(string name, string resource, Renderer order) {
        var obj = RandomizerQuad.Build(name, resource, order);
        if (obj == null) {
            Randomizer.log("scrollbar: " + resource + " did not load, running without one");
            return null;
        }

        obj.transform.SetParent(scrollBar, false);
        return obj.transform;
    }

    // Rows added after this are on the page only while wanted() holds, checked on each open. They leave
    // both lists rather than hide: the scroll window and the manager re-show any row that is listed.
    public void OptionalRows(Func<bool> wanted) {
        optionalFrom = selectionManager.MenuItems.Count;
        optionalWanted = wanted;
    }

    private void FitOptional() {
        if (optionalWanted == null) {
            return;
        }

        if (optionalRows == null) {
            optionalRows = selectionManager.MenuItems.GetRange(optionalFrom, selectionManager.MenuItems.Count - optionalFrom);
        }

        var wanted = optionalWanted();
        if (wanted == optionalShown) {
            return;
        }

        optionalShown = wanted;
        // Off a leaving row without SetCurrentItem, whose callbacks would enter the page from its OnEnable;
        // the neighbor counts as chosen, so entry lands there even after the menus closed.
        if (!wanted && selectionManager.Index >= optionalFrom) {
            if (selectionManager.CurrentMenuItem != null) {
                selectionManager.CurrentMenuItem.OnUnhighlight();
            }

            selectionManager.Index = selectionManager.NavigableFrom(optionalFrom - 1, 0, optionalFrom - 1);
            chosenIn = menuSession;
            if (selectionManager.IsHighlightVisible && selectionManager.CurrentMenuItem != null) {
                selectionManager.CurrentMenuItem.OnHighlight();
            }
        }

        foreach (var row in optionalRows) {
            if (wanted) {
                row.gameObject.SetActive(true);
                selectionManager.MenuItems.Add(row);
                layout.MenuItems.Add(row);
            } else {
                selectionManager.MenuItems.Remove(row);
                layout.MenuItems.Remove(row);
                row.gameObject.SetActive(false);
            }
        }

        layout.Sort();
        PlaceScrollbar();
    }

    private void PlaceScrollbar() {
        if (scrollThumb == null) {
            return;
        }

        var count = Mathf.Max(1, layout.MenuItems.Count);
        var thumb = barLength * Mathf.Clamp01((float)layout.MaxVisible / count);
        var t = Mathf.Clamp01((float)layout.ScrollTop / Mathf.Max(1, count - layout.MaxVisible));
        scrollThumb.localScale = new Vector3(BarWidth, thumb, 1f);
        scrollThumb.localPosition = new Vector3(0f, Mathf.Lerp(0.5f * (barLength - thumb), -0.5f * (barLength - thumb), t), 0f);
    }

    public void Update() {
        // re-asserted every frame because finishing an edit resumes everything
        HoldMenu();
        if (layout == null || layout.MaxVisible <= 0) {
            return;
        }

        // every key belongs to the bind being edited, and the window it is in stays put
        if (Editing) {
            return;
        }

        RapidScroll();
        SaveHold();
        ResetTap();
        // a prompt is modal: the page behind it stays put
        if (prompt != null) {
            return;
        }

        var wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 0.01f) {
            layout.ScrollBy(wheel > 0f ? -1 : 1);
            return;
        }

        if (Input.GetMouseButton(0) && scrollTrack != null) {
            DragScrollbar();
        }
    }

    // Moves the selection, not the window: the window is clamped around the selection.
    private void RapidScroll() {
        // all four read every frame, so a plain bind's edge is spent under its shifted one
        var home = RandomizerRebinding.MenuHome.IsPressed();
        var back = RandomizerRebinding.MenuSkipBackwards.IsPressed();
        var end = RandomizerRebinding.MenuEnd.IsPressed();
        var forward = RandomizerRebinding.MenuSkipForwards.IsPressed();
        var last = layout.MenuItems.Count - 1;
        if (last < 0 || prompt != null) {
            return;
        }

        if (home || end) {
            JumpTo(home ? 0 : last);
            return;
        }

        if (back || forward) {
            // most of a window, so a row you were looking at stays in view
            var step = Mathf.Max(1, Mathf.RoundToInt(layout.MaxVisible * 0.8f));
            JumpTo(Mathf.Clamp(selectionManager.Index + (back ? -step : step), 0, last));
        }
    }

    // a header cannot hold the selection, so a jump onto one moves past it
    private void JumpTo(int index) {
        var row = selectionManager.NavigableFrom(index, 0, layout.MenuItems.Count - 1);
        if (row >= 0) {
            selectionManager.SetCurrentItem(row);
        }
    }

    // Holding Soul Link does what the prompt's SAVE does: binds are live, so saving is a new baseline.
    private void SaveHold() {
        if (prompt != null || !selectionManager.IsActive || !BindsDirty ||
                Down(PlayerInputRebinding.KeyRebindings.SoulFlame) == KeyCode.None) {
            if (saving >= 0f) {
                HideHold();
            }

            saving = -1f;
            return;
        }

        // a press of its own, not a key that was already down when the page became savable
        if (saving < 0f) {
            if (!Pressed(PlayerInputRebinding.KeyRebindings.SoulFlame)) {
                return;
            }

            saving = Time.unscaledTime;
        }

        var progress = (Time.unscaledTime - saving) / RandomizerHoldRing.Seconds;
        if (soulGlyph) {
            DrawHold(progress, "select");
        }

        if (progress < 1f) {
            return;
        }

        saving = -1f;
        HideHold();
        SnapshotBinds();
        BindLegend();
    }

    // Backspace resets the page, behind a confirm.
    private void ResetTap() {
        if (prompt != null || !selectionManager.IsActive || ResetQuestion == null ||
                !Input.GetKeyDown(EraseKey)) {
            return;
        }

        Confirm(ResetQuestion, new[] { "OK", "CANCEL" }, answer => {
            if (answer == 0) {
                ResetToDefaults();
            }
        }, -1);
    }

    // Override both to offer a reset; a null question means no reset key and no legend hint.
    public virtual string ResetQuestion {
        get { return null; }
    }

    public virtual void ResetToDefaults() {
    }

    // One copy of the file before the last reset that changed it; not .bak, where people keep their own.
    public static void Backup(string file) {
        try {
            string left;
            if (File.Exists(file) && !(resetLeft.TryGetValue(file, out left) && File.ReadAllText(file) == left)) {
                File.Copy(file, file + BackupSuffix, true);
            }
        } catch (Exception e) {
            Randomizer.log("settings: no backup of " + file + ": " + e.Message);
        }
    }

    // Call once a reset has written the file: a second reset with nothing changed keeps the first's backup.
    public static void AfterReset(string file) {
        try {
            resetLeft[file] = File.ReadAllText(file);
        } catch (Exception) {
            resetLeft.Remove(file);
        }
    }

    // Grab anywhere on the bar and the window follows.
    private void DragScrollbar() {
        var cursor = Core.Input.CursorPositionUI;
        var half = 0.5f * scrollTrack.lossyScale.y;
        if (half <= 0f || Mathf.Abs(cursor.x - scrollTrack.position.x) > ScrollGrab) {
            return;
        }

        var t = Mathf.Clamp01((scrollTrack.position.y + half - cursor.y) / (2f * half));
        layout.ScrollTo(Mathf.RoundToInt(t * (layout.MenuItems.Count - layout.MaxVisible)));
    }

    // Binds apply live; the snapshot is what DISCARD restores.
    public void SnapshotBinds() {
        foreach (var control in keyControls) {
            control.Snapshot();
        }

        foreach (var control in padControls) {
            control.Snapshot();
        }

        foreach (var control in randoControls) {
            control.Snapshot();
        }

        SnapshotUnlisted();
    }

    // Binds the page changes without a row for them (a reset reaches them); DISCARD restores them too.
    protected virtual void SnapshotUnlisted() {
    }

    protected virtual bool UnlistedChanged {
        get { return false; }
    }

    protected virtual void RestoreUnlisted() {
    }

    public bool BindsDirty {
        get {
            if (UnlistedChanged) {
                return true;
            }

            foreach (var control in keyControls) {
                if (control.Changed) {
                    return true;
                }
            }

            foreach (var control in padControls) {
                if (control.Changed) {
                    return true;
                }
            }

            foreach (var control in randoControls) {
                if (control.Changed) {
                    return true;
                }
            }

            return false;
        }
    }

    public void RevertBinds() {
        foreach (var control in keyControls) {
            control.Restore();
        }

        foreach (var control in padControls) {
            control.Restore();
        }

        foreach (var control in randoControls) {
            control.Restore();
        }

        RestoreUnlisted();
        if (randoControls.Length > 0) {
            RandomizerRebinding.WriteBindsToFile();
        }

        if (keyControls.Length > 0) {
            PlayerInputRebinding.WriteKeyRebindSettings();
        }

        if (padControls.Length > 0) {
            PlayerInputRebinding.WriteControllerRebindSettings();
        }

        var input = PlayerInput.Instance;
        if (input != null) {
            input.RefreshControlScheme();
        }
    }

    // The legend: how to work the page, and whether it holds unsaved changes.
    public virtual void BindLegend() {
        if (keyControls.Length == 0 && padControls.Length == 0 && randoControls.Length == 0) {
            return;
        }

        // a pad edit takes buttons until Cancel and has no undo; a key edit ends on Enter
        var cancel = CancelCap();
        if (Editing) {
            var erase = RandomizerKeyIcons.Caption(EraseKey) + " Remove Last";
            if (randoControls.Length > 0) {
                Legend("<icon>z</>" + Ring + "(Hold): Bind " + (ReadingActions ? "Keys" : "Game Actions"),
                    Join(erase, "<icon>D</> Finish"), cancel + Ring + "(Hold): Cancel",
                    ModeShift);
            } else if (keyControls.Length > 0) {
                Legend(string.Empty, Join(erase, "<icon>D</> Finish"),
                    cancel + Ring + "(Hold): Cancel");
            } else {
                Legend(string.Empty, cancel + " Finish", cancel + Ring + "(Hold): Cancel");
            }

            return;
        }

        var navigate = "<icon>vr</>" + Pages() + " Navigate";
        if (!BindsDirty) {
            Legend(navigate, Join("<icon>D</> Rebind", Reset()), cancel + " Back");
            return;
        }

        // the save hint gets a slot to itself: its ring wraps the slot's leftmost glyph
        soulGlyph = MessageParserUtility.ProcessString(SoulKey).Contains("<icon>");
        Legend(Join(navigate, "<icon>D</> Rebind"), SoulKey + Ring + "(Hold): Save Changes",
            Join(Reset(), cancel + " Back"), SavingLeft, SavingRight);
    }

    // the first Cancel key's cap, as the holds and taps follow that bind rather than Escape
    private static string CancelCap() {
        var keys = PlayerInputRebinding.KeyRebindings.Cancel;
        return keys != null && keys.Length > 0 ? RandomizerKeyIcons.Caption(keys[0]) : "<icon>y</>";
    }

    // the reset hint, on pages that have one
    private string Reset() {
        if (ResetQuestion == null) {
            return string.Empty;
        }

        return RandomizerKeyIcons.Caption(EraseKey) + " Reset All";
    }

    // hints sharing a slot, empty ones dropped
    private static string Join(params string[] hints) {
        return string.Join(Apart, hints.Where(hint => !string.IsNullOrEmpty(hint)).ToArray());
    }

    // between two hints sharing a slot
    private static string Apart {
        get { return RandomizerKeyIcons.Gap; }
    }

    // after a held hint's glyph, so the words start clear of the ring drawn round it
    private static string Ring {
        get { return RandomizerKeyIcons.Thin; }
    }

    // the page-skip caps for the Navigate hint, only when the list outgrows its window
    private string Pages() {
        if (layout == null || layout.MaxVisible <= 0 || layout.MenuItems.Count <= layout.MaxVisible ||
                !RandomizerRebinding.MenuSkipBackwards.HasBind() ||
                !RandomizerRebinding.MenuSkipForwards.HasBind()) {
            return string.Empty;
        }

        return RandomizerRebinding.MenuSkipBackwards.FirstBindName() +
            RandomizerRebinding.MenuSkipForwards.FirstBindName();
    }

    // <icon> switches to a font whose letters are key images: D Enter, y Esc, M Del,
    // vr up/down, st left/right.
    public void Legend(string navigate, string select, string back,
                       float left = SlotShift, float right = SlotShift) {
        var legend = transform.FindChild("highlightFade/legend/pcLegend");
        if (legend == null) {
            return;
        }

        // vanilla's slots fit vanilla's words; widen once, as this runs on every legend change
        if (!widened) {
            widened = true;
            Widen(legend, "navigate");
            Widen(legend, "select");
            Widen(legend, "back");
        }

        Spread(legend, left, right);
        Slot(legend, "navigate", navigate);
        Slot(legend, "select", select);
        Slot(legend, "back", back);
    }

    // Moves the outer two slots to this line's stand-off from the middle one; spread is where they are.
    private void Spread(Transform legend, float left, float right) {
        if (!Mathf.Approximately(spread.x, left)) {
            Nudge(legend, "navigate", spread.x - left);
            spread.x = left;
        }

        if (!Mathf.Approximately(spread.y, right)) {
            Nudge(legend, "back", right - spread.y);
            spread.y = right;
        }
    }

    // the held Cancel-bound key, if any: the legend glyph comes from that binding too
    public static KeyCode BackHeld() {
        return Down(PlayerInputRebinding.KeyRebindings.Cancel);
    }

    private static KeyCode Down(KeyCode[] keys) {
        if (keys == null) {
            return KeyCode.None;
        }

        foreach (var key in keys) {
            if (Input.GetKey(key)) {
                return key;
            }
        }

        return KeyCode.None;
    }

    private static bool Pressed(KeyCode[] keys) {
        if (keys == null) {
            return false;
        }

        foreach (var key in keys) {
            if (Input.GetKeyDown(key)) {
                return true;
            }
        }

        return false;
    }

    // Fills the hold ring over the held hint's key glyph.
    public void DrawHold(float progress, string slot = "back") {
        RandomizerHoldRing.Around(RandomizerHoldRing.Glyph(transform.FindChild("highlightFade/legend/pcLegend/" + slot)), progress);
    }

    public void HideHold() {
        RandomizerHoldRing.Hide();
    }

    private static void Nudge(Transform legend, string name, float by) {
        var child = legend.FindChild(name);
        if (child != null) {
            child.localPosition += new Vector3(by, 0f, 0f);
        }
    }

    private static void Widen(Transform legend, string name) {
        var child = legend.FindChild(name);
        var box = child == null ? null : child.GetComponentInChildren<MessageBox>(true);
        if (box != null && box.TextBox != null) {
            box.TextBox.width *= SlotWidth;
        }
    }

    private static void Slot(Transform legend, string name, string words) {
        var child = legend.FindChild(name);
        var box = child == null ? null : child.GetComponentInChildren<MessageBox>(true);
        if (box == null) {
            return;
        }

        box.MessageProvider = null;
        box.SetMessage(new MessageDescriptor(words));
    }

    // Raises the panel a row and puts a two-line tooltip over the legend; returns how many rows
    // fit above it, for ScrollAfter.
    public int Footer() {
        var line = layout.MenuItems.Count > 0 ? layout.MenuItems[0].Space : DefaultSpace;
        pivot.localPosition += new Vector3(0f, Raise, 0f);

        // a clear line over the legend and another under the last row, tooltip between
        var top = LegendY + TooltipLines * line;
        var at = tooltipController.transform.position;
        at.y = top;
        tooltipController.transform.position = at;

        var room = FirstRowY + Raise - (top + line);
        return Mathf.Max(1, Mathf.FloorToInt(room / line) + 1);
    }

    public void HideLegend() {
        Destroy(transform.FindChild("highlightFade/legend").gameObject);
    }

    public void AddButton(string caption, Action onClick, string tooltip = null) {
        var cleverMenuItem = AddItem("");
        cleverMenuItem.gameObject.name = "Button (" + caption + ")";
        cleverMenuItem.gameObject.transform.Find("text/stateText").GetComponent<MessageBox>().SetMessage(new MessageDescriptor(caption));
        cleverMenuItem.PressedCallback += onClick;
        // else the row keeps its vanilla template's tooltip
        ConfigureTooltip(cleverMenuItem.GetComponent<CleverMenuItemTooltip>(), tooltip ?? caption);
    }

    // A label row, in both lists so the window's indices line up. NeverCondition makes navigation
    // skip it; a zero Size keeps the cursor off it.
    public void AddHeader(string caption) {
        var cleverMenuItem = AddItem(caption);
        cleverMenuItem.gameObject.name = "Header (" + caption + ")";
        cleverMenuItem.Size = Vector2.zero;
        // the row's tint beats a <style> tag; one color in every state
        cleverMenuItem.Transition.NormalColor = HeaderColor;
        cleverMenuItem.Transition.HighlightedColor = HeaderColor;
        cleverMenuItem.Transition.DisabledColor = HeaderColor;
        cleverMenuItem.OnUnhighlight();
        cleverMenuItem.Activated = cleverMenuItem.gameObject.AddComponent<NeverCondition>();
        // else it keeps its template's tooltip, the vanilla Damage Text help
        Destroy(cleverMenuItem.GetComponent<CleverMenuItemTooltip>());
        var state = cleverMenuItem.transform.Find("text/stateText").GetComponent<MessageBox>();
        state.MessageProvider = null;
        state.SetMessage(new MessageDescriptor(string.Empty));
    }

    public void AddRandomizerBind(string action, string help = null, string label = null) {
        AddRandomizerBind(action, help == null ? null : (Func<string>)(() => help), label);
    }

    // help is read again each time the page opens
    public void AddRandomizerBind(string action, Func<string> help, string label = null) {
        var cleverMenuItem = AddItem(label ?? action);
        cleverMenuItem.gameObject.name = "Rando Bind (" + action + ")";
        var control = cleverMenuItem.gameObject.AddComponent<RandomizerBindControl>();
        control.Init(action, this, label ?? action, help);
        cleverMenuItem.PressedCallback += delegate { control.BeginEditing(); };
        rowRefresh.Add(control.Refresh);
    }

    public void AddControllerBind(string label, Func<PlayerInputRebinding.ControllerButton[]> getKeys, Action<PlayerInputRebinding.ControllerButton[]> setKeys) {
        var cleverMenuItem = AddItem(label);
        cleverMenuItem.gameObject.name = "Controller Bind (" + label + ")";
        var kc = cleverMenuItem.gameObject.AddComponent<ControllerBindControl>();
        kc.Init(getKeys, setKeys, this, label);
        cleverMenuItem.PressedCallback += delegate { kc.BeginEditing(); };
    }

    private void AddToLayout(CleverMenuItem item) {
        layout.AddItem(item);
        layout.Sort();
        item.SetOpacity(1f);
        item.OnUnhighlight();
    }

    public CleverMenuItem AddItem(string label) {
        var gameObject = Instantiate(SettingsScreen.Instance.transform.Find("highlightFade/pivot/damageText").gameObject);
        gameObject.transform.SetParent(pivot);
        foreach (var c in gameObject.GetComponentsInChildren<MonoBehaviour>()) {
            c.enabled = true;
        }

        var component = gameObject.GetComponent<CleverMenuItem>();
        component.Pressed = null;
        selectionManager.MenuItems.Add(component);
        AddToLayout(component);
        var componentsInChildren = component.transform.GetComponentsInChildren<TransparencyAnimator>();
        for (var i = 0; i < componentsInChildren.Length; i++) {
            componentsInChildren[i].Reset();
            componentsInChildren[i].enabled = true;
        }

        foreach (var obj in component.transform.FindChild("glowGroup")) {
            TransparencyAnimator.Register((Transform)obj);
        }

        gameObject.transform.Find("text/nameText").GetComponent<MessageBox>().SetMessage(new MessageDescriptor(label));
        return component;
    }

    public void AddToggle(RandomizerSettings.BoolSetting setting, string tooltip) {
        var cleverMenuItem = AddItem(setting.Name);
        cleverMenuItem.name = setting.Name;
        var toggleCustomSettingsAction = cleverMenuItem.gameObject.AddComponent<ToggleCustomSettingsAction>();
        toggleCustomSettingsAction.Setting = setting;
        toggleCustomSettingsAction.Init();
        cleverMenuItem.PressedCallback += toggleCustomSettingsAction.Toggle;
        rowRefresh.Add(toggleCustomSettingsAction.Init);

        ConfigureTooltip(cleverMenuItem.GetComponent<CleverMenuItemTooltip>(), tooltip);
    }

    // where a row prints its value, in the row's own space
    private float ValueColumnX(CleverMenuItem row) {
        var state = row.transform.Find("text/stateText");
        return state ? row.transform.InverseTransformPoint(state.position).x : SwatchX;
    }

    // label overrides the caption: a setting's name is its file key and can outrun a row
    public void AddColor(RandomizerSettings.ColorSetting setting, string tooltip, string label = null, bool asMessage = false) {
        var cleverMenuItem = AddItem(label ?? setting.Name);
        cleverMenuItem.name = setting.Name;

        // the white hint background tinted by _Color: a live swatch for no new art
        var swatch = RandomizerQuad.Build(setting.Name + " swatch", "hintMessageBackgroundWhite.png",
                                          cleverMenuItem.GetComponentInChildren<Renderer>());
        Transform placed = null;
        if (swatch != null) {
            placed = swatch.transform;
            placed.SetParent(cleverMenuItem.transform, false);
            // a message background previews as a wide tile, like the message it colors
            var wide = asMessage ? MessageSwatchWidth : 1f;
            var width = SwatchSize * wide;
            placed.localScale = new Vector3(width, SwatchSize * 0.55f, 1f);
            // centered quad: the message art fades in from its edge, a painted swatch does not
            var margin = asMessage ? SwatchMargin : 0f;
            placed.localPosition = new Vector3(ValueColumnX(cleverMenuItem) + (0.5f - margin) * width, 0f, 0f);
        }

        // the control owns the row's tooltip, so it can swap in the editing keys and back
        var control = cleverMenuItem.gameObject.AddComponent<ColorControl>();
        control.Init(setting, this, placed, tooltip, asMessage);
        cleverMenuItem.PressedCallback += control.BeginEditing;
        rowRefresh.Add(control.Show);
    }

    // Clones the Language row, swapping its language-filling list for a bare CleverMenuOptionsList;
    // the group opens the flyout on press. rowName overrides the caption, as label does on AddColor.
    public void AddEnum<T>(RandomizerSettings.EnumSetting<T> setting, string tooltip, string rowName = null) where T : Enum {
        var clone = (GameObject)Instantiate(SettingsScreen.Instance.transform.Find("highlightFade/pivot/language").gameObject);
        clone.name = setting.Name;
        var caption = rowName ?? setting.Name;
        foreach (var c in clone.GetComponentsInChildren<MonoBehaviour>(true)) {
            c.enabled = true;
        }

        clone.transform.SetParent(pivot);
        var cleverMenuItem = clone.GetComponent<CleverMenuItem>();
        cleverMenuItem.Pressed = null;
        selectionManager.MenuItems.Add(cleverMenuItem);
        AddToLayout(cleverMenuItem);

        var flyout = clone.transform.FindChild("languageOptions");
        group.AddItem(cleverMenuItem, flyout.GetComponent<CleverMenuItemGroup>());

        var state = clone.transform.FindChild("text/stateText").GetComponent<MessageBox>();
        var nameBox = clone.transform.FindChild("text/nameText").GetComponent<MessageBox>();
        nameBox.MessageProvider = null;
        nameBox.SetMessage(new MessageDescriptor(caption));

        var list = PlainOptionsList(flyout);
        var members = new List<string>();
        foreach (var raw in Enum.GetValues(typeof(T))) {
            var value = (T)raw;
            var label = EnumLabel(value);
            members.Add(value.ToString());
            list.AddItem(label, delegate {
                setting.Value = value;
                RandomizerSettings.SetDirty();
                state.MessageProvider = null;
                state.SetMessage(new MessageDescriptor(label));
                RestoreTooltip();
            });
        }

        Action showValue = delegate {
            state.MessageProvider = null;
            state.SetMessage(new MessageDescriptor(EnumLabel(setting.Value)));
        };
        showValue();
        rowRefresh.Add(showValue);
        ConfigureTooltip(clone.GetComponent<CleverMenuItemTooltip>(), tooltip);
        FitBackground(flyout, list.Spacing, members.IndexOf(setting.Value.ToString()));
        FlyoutTooltips(flyout, setting, members, tooltip);
    }

    // Resizes and re-centers the language panel on the rows actually there, measured off their
    // transforms. The panel is rotated 270 degrees: its local x is the screen's vertical.
    private void FitBackground(Transform flyout, float spacing, int selected) {
        var bg = flyout.FindChild("abilityMessageBackground");
        var mesh = bg == null ? null : bg.GetComponent<MeshFilter>();
        var rows = flyout.GetComponent<CleverMenuItemSelectionManager>().MenuItems;
        if (mesh == null || rows.Count == 0) {
            return;
        }

        var top = flyout.InverseTransformPoint(rows[0].transform.position).y;
        var bottom = flyout.InverseTransformPoint(rows[rows.Count - 1].transform.position).y;

        // opens with the option set at build time over the row's own value
        var anchor = -top + Mathf.Max(0, selected) * spacing;
        flyout.localPosition = new Vector3(flyout.localPosition.x, anchor, flyout.localPosition.z);

        // the on-screen gap, which Spacing need not be
        var gap = rows.Count > 1 ? Mathf.Abs(top - bottom) / (rows.Count - 1) : spacing;
        var wanted = Mathf.Abs(top - bottom) + BackgroundPad * gap;
        // the art has a transparent margin, so the quad outgrows the rows
        bg.localScale = new Vector3(wanted / mesh.sharedMesh.bounds.size.x,
                                    bg.localScale.y + 2.2f * gap, bg.localScale.z);
        bg.localPosition = new Vector3(bg.localPosition.x, 0.5f * (top + bottom), bg.localPosition.z);
    }

    // Points the page's one tooltip at the flyout while it is open; a pick or Back hands it back.
    private void FlyoutTooltips(Transform flyout, RandomizerSettings.SettingBase setting, List<string> members, string fallback) {
        var sel = flyout.GetComponent<CleverMenuItemSelectionManager>();
        for (var i = 0; i < sel.MenuItems.Count && i < members.Count; i++) {
            var help = ValueHelp(setting, members[i]);
            ConfigureTooltip(sel.MenuItems[i].gameObject.AddComponent<CleverMenuItemTooltip>(),
                             help ?? fallback);
        }

        sel.OptionChangeCallback += delegate {
            tooltipController.Selection = sel;
            tooltipController.UpdateTooltip();
        };
        var flyoutGroup = flyout.GetComponent<CleverMenuItemGroup>();
        flyoutGroup.OnBackPressed = (Action)Delegate.Combine(flyoutGroup.OnBackPressed, new Action(RestoreTooltip));
    }

    private void RestoreTooltip() {
        tooltipController.Selection = selectionManager;
        tooltipController.UpdateTooltip();
    }

    // per-value help, read from the setting comment's "<Value>: what it does" lines
    private static string ValueHelp(RandomizerSettings.SettingBase setting, string member) {
        foreach (var line in setting.Comment.Split('\n')) {
            var text = line.Trim();
            if (!text.StartsWith(member)) {
                continue;
            }

            var colon = text.IndexOf(':');
            if (colon > 0 && colon <= member.Length + " (default)".Length) {
                return text.Substring(colon + 1).Trim();
            }
        }

        return null;
    }

    // Clones the pause menu's "Return to Main Menu?" prompt. Answers index its manager's list; Back is -1.
    // With no prompt to clone, the caller's unasked answer is taken: make it the harmless one.
    public void Confirm(string question, string[] answers, Action<int> chosen, int unasked = 0) {
        if (prompt != null) {
            return;
        }

        if (questionPrefab == null) {
            foreach (var spawn in Resources.FindObjectsOfTypeAll<InstantiateAction>()) {
                if (spawn.Prefab != null && spawn.Prefab.name == QuestionPrefabName) {
                    questionPrefab = spawn.Prefab;
                    break;
                }
            }
        }

        if (questionPrefab == null) {
            Randomizer.log("settings: no confirm prefab, answering " + (unasked >= 0 ? answers[unasked] : "Back"));
            chosen(unasked);
            return;
        }

        prompt = (GameObject)Instantiate(questionPrefab);
        prompt.name = "confirm";
        // world position kept: the prefab already sits where a prompt belongs on screen
        prompt.transform.SetParent(transform, true);
        prompt.SetActive(true);

        // its opening sequence pauses the game, which is not ours to do from a menu
        var opening = prompt.transform.FindChild("*actionSequence");
        if (opening != null) {
            opening.gameObject.SetActive(false);
        }

        // the prefab wraps at its own question's width; widen before the text so it renders once
        var title = prompt.transform.FindChild("title");
        var titleBox = title == null ? null : title.GetComponentInChildren<MessageBox>(true);
        if (titleBox != null && titleBox.TextBox != null) {
            titleBox.TextBox.width *= QuestionWidth;
        }

        Ask(title, question);

        // the plate widens, and grows upward only, so the answers stay put
        var back = prompt.transform.FindChild("messageBackgroundA");
        var mesh = back == null ? null : back.GetComponent<MeshFilter>();
        if (mesh != null && mesh.sharedMesh != null && mesh.sharedMesh.bounds.size.y > 0f) {
            var grow = TopPad / mesh.sharedMesh.bounds.size.y;
            back.localScale = new Vector3(back.localScale.x * PlateWidth,
                                          back.localScale.y + grow, back.localScale.z);
            back.localPosition += new Vector3(0f, 0.5f * TopPad, 0f);
        }
        var manager = prompt.GetComponent<CleverMenuItemSelectionManager>();
        for (var i = 0; i < manager.MenuItems.Count; i++) {
            var item = manager.MenuItems[i];
            var answer = i;
            Ask(item.transform, i < answers.Length ? answers[i] : string.Empty);
            // vanilla rows quit to the menu, gated on being safe to quit; ours only report
            item.Pressed = null;
            item.Activated = null;
            item.Visible = null;
            item.PressedCallback += delegate { CloseConfirm(chosen, answer); };
        }

        manager.BackGuard = delegate {
            CloseConfirm(chosen, -1);
            return false;
        };

        // Freeze the page behind: IsActive stops keys, IsLocked (checked first) stops clicks,
        // and its lit row is dimmed.
        selectionManager.IsActive = false;
        selectionManager.IsLocked = true;
        if (selectionManager.CurrentMenuItem != null) {
            selectionManager.CurrentMenuItem.OnUnhighlight();
        }

        manager.IsActive = true;
        manager.SetCurrentItem(0);
    }

    // Every text node in the prompt is a MessageBox under a same-named child.
    private static void Ask(Transform where, string words) {
        if (where == null) {
            return;
        }

        var box = where.GetComponentInChildren<MessageBox>(true);
        if (box == null) {
            return;
        }

        box.MessageProvider = null;
        box.SetMessage(new MessageDescriptor(words));
    }

    private void CloseConfirm(Action<int> chosen, int answer) {
        if (prompt == null) {
            return;
        }

        var going = prompt;
        prompt = null;
        Destroy(going);
        selectionManager.IsActive = true;
        selectionManager.IsLocked = false;
        if (selectionManager.CurrentMenuItem != null) {
            selectionManager.CurrentMenuItem.OnHighlight();
        }

        // the controller hides the tooltip while the page is inactive and never re-shows it
        tooltipController.UpdateTooltip();
        chosen(answer);
        // after the answer, which decides whether anything is unsaved
        BindLegend();
    }

    private CleverMenuOptionsList PlainOptionsList(Transform flyout) {
        var old = flyout.GetComponent<CleverMenuOptionsList>();
        var prefab = old.Item;
        var origin = old.Origin;
        var spacing = old.Spacing;
        var scrollPivot = old.ScrollPivot;
        var scrollable = old.Scrollable;
        var onScreenLimit = old.OnScreenLimit;
        var scrollingSpeed = old.ScrollingSpeed;
        DestroyImmediate(old);

        foreach (var kid in origin.Cast<Transform>().ToList()) {
            if (kid.name.StartsWith("optionRow") && kid.gameObject != prefab) {
                DestroyImmediate(kid.gameObject);
            }
        }

        // the manager still lists the destroyed language rows, and SetIndexToFirst would land on one
        flyout.GetComponent<CleverMenuItemSelectionManager>().MenuItems.Clear();

        var list = flyout.gameObject.AddComponent<CleverMenuOptionsList>();
        list.Item = prefab;
        list.Origin = origin;
        list.Spacing = spacing;
        list.ScrollPivot = scrollPivot;
        list.Scrollable = scrollable;
        list.OnScreenLimit = onScreenLimit;
        list.ScrollingSpeed = scrollingSpeed;
        return list;
    }

    // A [Description] wins where one is set; otherwise the member name is split.
    private static string EnumLabel(object value) {
        var field = value.GetType().GetField(value.ToString());
        var described = field == null ? null : field.GetCustomAttributes(typeof(DescriptionAttribute), false);
        if (described != null && described.Length > 0) {
            return ((DescriptionAttribute)described[0]).Description;
        }

        return Spaced(value.ToString());
    }

    // NewPlayer reads as New Player. A run of capitals stays together until the last one,
    // which belongs to the word starting there, so QOLThing splits as QOL Thing.
    private static string Spaced(string name) {
        var text = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++) {
            var starts = char.IsUpper(name[i]) && (!char.IsUpper(name[i - 1 < 0 ? 0 : i - 1])
                                                   || (i + 1 < name.Length && char.IsLower(name[i + 1])));
            if (i > 0 && starts) {
                text.Append(' ');
            }

            text.Append(name[i]);
        }

        return text.ToString();
    }

    public void AddSlider(RandomizerSettings.FloatSetting setting, float min, float max, float step, string tooltip) {
        // Template is music volume slider
        var clone = Instantiate(SettingsScreen.Instance.transform.Find("highlightFade/pivot/musicVolume").gameObject);
        clone.gameObject.name = setting.Name;
        foreach (var c in clone.GetComponentsInChildren<MonoBehaviour>()) {
            c.enabled = true;
        }

        // Add to navigation manager (required for all option types)
        clone.transform.SetParent(pivot);
        var cleverMenuItem = clone.GetComponent<CleverMenuItem>();
        selectionManager.MenuItems.Add(cleverMenuItem);
        AddToLayout(cleverMenuItem);

        // Add to group (required for sliders and dropdown items, but not toggles)
        var slider = clone.transform.FindChild("slider").GetComponent<CleverValueSlider>();
        slider.NavigateMessageBoxes = new[] {
            transform.FindChild("highlightFade/legend/pcLegend/navigate").GetComponent<MessageBox>(),
            transform.FindChild("highlightFade/legend/xBoxLegend/navigate").GetComponent<MessageBox>()
        };
        group.AddItem(cleverMenuItem, slider);

        slider.MinValue = min;
        slider.MaxValue = max;
        slider.Step = step;
        (slider as MusicVolumeSlider).Setting = setting;

        var nameTextBox = clone.transform.Find("nameText").GetComponent<MessageBox>();
        nameTextBox.MessageProvider = null;
        nameTextBox.SetMessage(new MessageDescriptor(setting.Name));

        ConfigureTooltip(clone.GetComponent<CleverMenuItemTooltip>(), tooltip);
    }

    private void ConfigureTooltip(CleverMenuItemTooltip tooltipComponent, string tooltip) {
        var tooltipMessageProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        tooltipMessageProvider.SetMessage(tooltip);
        tooltipComponent.Tooltip = tooltipMessageProvider;
    }

    public CleverMenuItemLayout layout;

    public CleverMenuItemSelectionManager selectionManager;

    public Transform pivot;

    public CleverMenuItemGroup group;

    public CleverMenuItem fakeTooltip;

    public CleverMenuItemTooltipController tooltipController;

    // a bind control is taking every key; the screen's own binds stand down
    public bool Editing;

    // which of a rando bind's two readings the edit in hand is on, for the legend
    public bool ReadingActions;

    // by eye against a legend slot: enough for two hints in one of them
    private const float SlotWidth = 2.2f;

    // outer-slot stand-offs per legend line, measured for even gaps between hints
    private const float SlotShift = 0.35f;

    private const float ModeShift = 0.5f;

    private const float SavingLeft = 0.1f;

    private const float SavingRight = 0.25f;

    private Vector2 spread;

    private bool widened;

    // warmer and flatter than a row's own white, so a category reads as a label
    private static readonly Color HeaderColor = new Color(0.85f, 0.72f, 0.42f, 0.55f);

    private GameObject prompt;

    // long enough to cover the manager's next FixedUpdate whichever order the two run in
    private const int SettleFrames = 3;

    private int settle;

    // when the save hold started, or -1 for no hold in hand
    private float saving = -1f;

    // a MessageBox resolves this to the Soul Link keycap, or to the key's name where there is no cap
    private const string SoulKey = "[SoulFlame]";

    private bool soulGlyph;

    // Erase, at both scales: the last bind of the row being edited, or every bind on the page.
    private const KeyCode EraseKey = KeyCode.Backspace;

    private const string BackupSuffix = ".before-reset";

    // each reset file as this session's last reset left it
    private static readonly Dictionary<string, string> resetLeft = new Dictionary<string, string>();

    // empty until the rows are built: the legend asks whether they are dirty on the way up
    private KeybindControl[] keyControls = new KeybindControl[0];

    private ControllerBindControl[] padControls = new ControllerBindControl[0];

    private RandomizerBindControl[] randoControls = new RandomizerBindControl[0];

    // re-reads each value row's setting into the row
    private readonly List<Action> rowRefresh = new List<Action>();

    // the tail of both lists from optionalFrom, kept while it is off them
    private int optionalFrom;

    private Func<bool> optionalWanted;

    private List<CleverMenuItem> optionalRows;

    private bool optionalShown = true;

    // counts closings of the options screen; a page's row is remembered within one
    private static int menuSession;

    private int chosenIn = -1;

    // Measured: the vanilla legend sits here, panel rows start here and step by this. The
    // camera has a fixed vertical FOV, so these are the same at every resolution and aspect.
    private const float LegendY = -3.42f;

    private const float FirstRowY = 2.944f;

    private const float DefaultSpace = 0.45f;

    // a tooltip wraps, so the footer reserves two lines rather than one
    private const int TooltipLines = 2;

    // there is about two rows of margin over the first row; one of them is worth taking
    private const float Raise = 0.45f;

    // a text line, matching the gap between the prompt's own two answers
    private const float TopPad = 0.45f;

    // multipliers on the prefab's wrap and plate widths, by eye against the longest question
    private const float QuestionWidth = 1.6f;

    private const float PlateWidth = 1.35f;

    private const string QuestionPrefabName = "returnToMainMenuQuestion";

    private static GameObject questionPrefab;

    public string DefaultTooltip = "Click on an action to add or remove binds";

    // placed by eye against a running screen; the bar hangs right of the rows
    public float BarX = 6.5f;

    public float EdgeFade = 0.35f;

    // how far either side of the bar still counts as grabbing it
    public float ScrollGrab = 0.5f;

    // the stroke is a fifth of its texture's width, so the quad is wider than the bar
    public float BarWidth = 0.26f;

    // fallback only: a row missing its state text still needs somewhere to put the swatch
    public float SwatchX = 3.7f;

    // the message art fades in over this fraction of its quad's width
    public float SwatchMargin = 0.162f;

    // tall enough to read, short enough that two color rows do not touch
    public float SwatchSize = 0.62f;

    public float MessageSwatchWidth = 4.4f;

    // flyout panel length past its rows, in row gaps: both ends plus the art's margin
    public float BackgroundPad = 7.2f;

    private float barLength;

    private Transform scrollBar;

    private Transform scrollTrack;

    private Transform scrollThumb;
}

// A key held to cancel or swap, tapped to bind; a key pressed mid-hold joins the chord along with it.
public class BindHold {
    public BindHold(Action<float> draw, Action hide, Action<KeyCode> tap, Action<KeyCode> join, Action held) {
        this.draw = draw;
        this.hide = hide;
        this.tap = tap;
        this.join = join;
        this.held = held;
    }

    // the held key is a chord member now, and neither taps nor holds until it is let go
    public bool Joined { get; private set; }

    // a key is down under this hold, joined or not
    public bool Holding {
        get { return key != KeyCode.None; }
    }

    // down is the key held now, or None; true while the hold has the frame, the one it lands on included
    public bool Update(KeyCode down, float now, bool canStart = true) {
        if (down == KeyCode.None) {
            var tapped = key != KeyCode.None && !Joined && now - since < RandomizerHoldRing.Tap ? key : KeyCode.None;
            hide();
            Reset();
            if (tapped != KeyCode.None) {
                tap(tapped);
            }

            return false;
        }

        if (key == KeyCode.None) {
            if (!canStart) {
                return false;
            }

            key = down;
            since = now;
        }

        if (Joined) {
            return false;
        }

        var progress = (now - since) / RandomizerHoldRing.Seconds;
        draw(progress);
        if (progress < 1f) {
            return true;
        }

        hide();
        Reset();
        held();
        return true;
    }

    // Another key went down. Called before Update, so a hold that starts this frame is not joined.
    public void Join() {
        if (key == KeyCode.None || Joined) {
            return;
        }

        Joined = true;
        hide();
        join(key);
    }

    public void Reset() {
        key = KeyCode.None;
        Joined = false;
    }

    private readonly Action<float> draw;

    private readonly Action hide;

    private readonly Action<KeyCode> tap;

    private readonly Action<KeyCode> join;

    private readonly Action held;

    private KeyCode key = KeyCode.None;

    private float since;
}

// A whole settings object as it was taken, arrays copied: fields no row shows are restored too.
public class SettingsSnapshot<T> where T : class, new() {
    public void Take(T live) {
        taken = new T();
        Copy(live, taken);
    }

    public bool Differs(T live) {
        if (taken == null) {
            return false;
        }

        foreach (var field in Fields) {
            if (!Same(field.GetValue(taken), field.GetValue(live))) {
                return true;
            }
        }

        return false;
    }

    public void Restore(T live) {
        if (taken != null) {
            Copy(taken, live);
        }
    }

    private static void Copy(T from, T to) {
        foreach (var field in Fields) {
            var value = field.GetValue(from);
            var array = value as Array;
            field.SetValue(to, array != null ? array.Clone() : value);
        }
    }

    private static bool Same(object a, object b) {
        var x = a as Array;
        var y = b as Array;
        if (x == null || y == null) {
            return Equals(a, b);
        }

        if (x.Length != y.Length) {
            return false;
        }

        for (var i = 0; i < x.Length; i++) {
            if (!Equals(x.GetValue(i), y.GetValue(i))) {
                return false;
            }
        }

        return true;
    }

    private static readonly FieldInfo[] Fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

    private T taken;
}

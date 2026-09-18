using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public abstract class CustomSettingsScreen : MonoBehaviour {
    public void OnEnable() {
        // each visit is its own baseline, so keeping changes and coming back starts clean
        SnapshotBinds();
    }

    public void OnDisable() {
        Hold(false);
        saving = -1f;
        HideHold();
        // Will only write if there have been changes
        RandomizerSettings.WriteSettings();
    }

    // Escape is bound to Pause as well as Cancel: the menu manager reads it first and closes
    // the whole screen, and the tab list takes clicks even while it is inactive. Both are
    // shut while the page has a question to ask or an edit in hand, and for a few frames
    // after either ends -- the manager reads that same press a frame behind the edit.
    public void HoldMenu() {
        if (Editing || prompt != null) {
            settle = SettleFrames;
        } else if (settle > 0) {
            settle--;
        }

        Hold(prompt != null || Editing || settle > 0 || (selectionManager.IsActive && BindsDirty));
        // an edit owns the page: the row under the cursor must not take the selection, and the
        // tooltip the edit put up must not be replaced by the one belonging to that row
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
        // Layout and selection manager
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

        // Tooltip
        var originalToolip = SettingsScreen.Instance.transform.Find("highlightFade/pivot/tooltip");
        var tooltip = Instantiate(originalToolip);
        tooltip.SetParent(pivot);
        tooltip.position = originalToolip.position;
        tooltipController = tooltip.GetComponent<CleverMenuItemTooltipController>();
        tooltipController.Selection = selectionManager;
        tooltipController.UpdateTooltip();
        tooltipController.enabled = true;

        InitScreen();
        // the first row can be a header, which is not a thing to be sitting on
        selectionManager.SetIndexToFirst();
        selectionManager.BackGuard = KeepOrDiscard;
    }

    // Leaving a page whose binds have changed asks first. Both answers land the back they
    // interrupted, because clearing the change is what lets the guard through.
    private bool KeepOrDiscard() {
        if (prompt != null) {
            return false;
        }

        if (!BindsDirty) {
            return true;
        }

        Confirm("Save keybinding changes?", new[] { "SAVE", "DISCARD" }, answer => {
            // Back declines to answer, which is a reason to stay rather than either of them
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

    // Call from InitScreen. The window follows the selection, so the re-sort has to hang
    // off the selection change rather than off Update.
    public void ScrollAfter(int rows) {
        // the rows are built by now and never change, so the controls are worth keeping
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

    // Two bars: the track is the whole window, the thumb is the share of the list that
    // fits in it. Both are chalk strokes drawn from PNGs, because the game ships no
    // scrollbar art and its own sprites cannot be re-textured.
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

        var wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 0.01f) {
            layout.ScrollBy(wheel > 0f ? -1 : 1);
            return;
        }

        if (Input.GetMouseButton(0) && scrollTrack != null) {
            DragScrollbar();
        }
    }

    // The same binds that skip through save slots. They move the selection, not the window:
    // the window is clamped around the selection, so moving it alone would snap back.
    private void RapidScroll() {
        // all four read every frame, so a plain bind's edge is spent under its shifted one
        var home = RandomizerRebinding.MenuHome.IsPressed();
        var back = RandomizerRebinding.MenuSkipBackwards.IsPressed();
        var end = RandomizerRebinding.MenuEnd.IsPressed();
        var forward = RandomizerRebinding.MenuSkipForwards.IsPressed();
        var last = layout.MenuItems.Count - 1;
        if (last < 0) {
            return;
        }

        if (home || end) {
            selectionManager.SetCurrentItem(home ? 0 : last);
            return;
        }

        if (back || forward) {
            // most of a screenful, so a row you were just looking at stays in view to orient by
            var step = Mathf.Max(1, Mathf.RoundToInt(layout.MaxVisible * 0.8f));
            selectionManager.SetCurrentItem(Mathf.Clamp(selectionManager.Index + (back ? -step : step), 0, last));
        }
    }

    // A held Soul Link is how the game itself saves, so it is how a page keeps its changes
    // without being asked on the way out. It answers the same way the question's SAVE does:
    // the binds are already live, and keeping them means making them the baseline.
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

    // Putting a page back rides a key rather than a row: a row is one more thing that scrolls
    // out of sight and reads like the binds around it. The question is the guard.
    private void ResetTap() {
        if (prompt != null || !selectionManager.IsActive || ResetQuestion == null ||
                !Input.GetKeyDown(EraseKey)) {
            return;
        }

        Confirm(ResetQuestion, new[] { "OK", "CANCEL" }, answer => {
            if (answer == 0) {
                ResetToDefaults();
            }
        });
    }

    // A page that can put its binds back overrides both. Without a question there is no
    // reset: the legend does not offer the key and the key does nothing.
    public virtual string ResetQuestion {
        get { return null; }
    }

    public virtual void ResetToDefaults() {
    }

    // A reset is the one thing these pages do that cannot be undone from inside them, so the
    // file it is about to overwrite is kept beside it. One copy, holding the state before the
    // last reset -- a history is not what someone who just lost their binds is after. Its own
    // suffix rather than .bak, which is where people put copies they made themselves.
    public static void Backup(string file) {
        try {
            if (File.Exists(file)) {
                File.Copy(file, file + BackupSuffix, true);
            }
        } catch (Exception e) {
            Randomizer.log("settings: no backup of " + file + ": " + e.Message);
        }
    }

    // Grab anywhere on the bar and the window follows, which is what a scrollbar is for.
    private void DragScrollbar() {
        var cursor = Core.Input.CursorPositionUI;
        var half = 0.5f * scrollTrack.lossyScale.y;
        if (half <= 0f || Mathf.Abs(cursor.x - scrollTrack.position.x) > ScrollGrab) {
            return;
        }

        var t = Mathf.Clamp01((scrollTrack.position.y + half - cursor.y) / (2f * half));
        layout.ScrollTo(Mathf.RoundToInt(t * (layout.MenuItems.Count - layout.MaxVisible)));
    }

    // Binds apply the moment they are made, so what the screen offers on the way out is
    // "keep these?", and declining restores what it was entered with.
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
    }

    public bool BindsDirty {
        get {
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

    // The bottom line: how to work the page, and whether it is holding anything unsaved. The
    // interaction text used to live in the tooltip, which left no room for anything about the
    // bind under the cursor.
    public virtual void BindLegend() {
        if (keyControls.Length == 0 && padControls.Length == 0 && randoControls.Length == 0) {
            return;
        }

        // a pad edit takes buttons until Escape and has no undo; a key edit ends on Enter
        if (Editing) {
            var erase = RandomizerKeyIcons.Caption(EraseKey) + " Remove Last";
            if (randoControls.Length > 0) {
                Legend("<icon>z</>" + Ring + "(Hold): Bind " + (ReadingActions ? "Keys" : "Game Actions"),
                    Join(erase, "<icon>D</> Finish"), "<icon>y</>" + Ring + "(Hold): Cancel",
                    ModeShift);
            } else if (keyControls.Length > 0) {
                Legend(string.Empty, Join(erase, "<icon>D</> Finish"),
                    "<icon>y</>" + Ring + "(Hold): Cancel");
            } else {
                Legend(string.Empty, "<icon>y</> Finish", "<icon>y</>" + Ring + "(Hold): Cancel");
            }

            return;
        }

        var navigate = "<icon>vr</>" + Pages() + " Navigate";
        if (!BindsDirty) {
            Legend(navigate, Join("<icon>D</> Rebind", Reset()), "<icon>y</> Back");
            return;
        }

        // Five hints over three slots: rebind joins the navigation it sits beside anyway, so the
        // save hint has a slot of its own and the ring can wrap its glyph as the leftmost one.
        // Reset follows save, keeping the two keys that rewrite the file next to each other.
        soulGlyph = MessageParserUtility.ProcessString(SoulKey).Contains("<icon>");
        Legend(Join(navigate, "<icon>D</> Rebind"), SoulKey + Ring + "(Hold): Save Changes",
            Join(Reset(), "<icon>y</> Back"), SavingLeft, SavingRight);
    }

    // The key that puts the whole page back, on the pages that have one.
    private string Reset() {
        if (ResetQuestion == null) {
            return string.Empty;
        }

        return RandomizerKeyIcons.Caption(EraseKey) + " Reset All";
    }

    // Hints that share a slot, with the ones a page has no use for left out.
    private static string Join(params string[] hints) {
        return string.Join(Apart, hints.Where(hint => !string.IsNullOrEmpty(hint)).ToArray());
    }

    // Between two hints sharing a slot, and after the glyph of a hint that is held -- the ring
    // is drawn wider than the cap it wraps, so the words after it have to start clear of it.
    private static string Apart {
        get { return RandomizerKeyIcons.Gap; }
    }

    private static string Ring {
        get { return RandomizerKeyIcons.Thin; }
    }

    // The page-at-a-time binds, drawn from the bindings themselves and shown in the same breath
    // as the arrows: four keys, one word. Only when there is more list than window.
    private string Pages() {
        if (layout == null || layout.MaxVisible <= 0 || layout.MenuItems.Count <= layout.MaxVisible ||
                !RandomizerRebinding.MenuSkipBackwards.HasBind() ||
                !RandomizerRebinding.MenuSkipForwards.HasBind()) {
            return string.Empty;
        }

        return RandomizerRebinding.MenuSkipBackwards.FirstBindName() +
            RandomizerRebinding.MenuSkipForwards.FirstBindName();
    }

    // The legend's three slots. Key icons come out of the text itself -- <icon> switches to a
    // font whose letters are key images: D is Enter, y Esc, M Del, vr the up and down arrows,
    // st left and right. More than one hint fits in a slot.
    public void Legend(string navigate, string select, string back,
                       float left = SlotShift, float right = SlotShift) {
        var legend = transform.FindChild("highlightFade/legend/pcLegend");
        if (legend == null) {
            return;
        }

        // The slots are cut to the words vanilla puts in them, and ours are longer; once only,
        // because this runs on every legend change.
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

    // The three slots sit where vanilla's short hints sat, anchored so the outer two grow away
    // from the middle one: how far apart they belong depends on how much each is carrying. Each
    // line says, and the numbers are measured off it -- one gap between hints wherever they sit.
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

    // Which key is standing in for Back right now. The gesture rides the binding rather than
    // Escape, because the legend glyph it fills is drawn from the binding too.
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

    // The ring a hold fills, drawn over the glyph of the key being held. One between all the
    // pages, because only one hold on one of them can be running at a time.
    public void DrawHold(float progress, string slot = "back") {
        var glyph = HoldGlyph(slot);
        if (glyph == null || ringless) {
            return;
        }

        // the holder is a plain object and never goes null with the scene its clone was in
        if (ring == null || ring.Object == null) {
            ring = new RandomizerHoldRing();
            if (!ring.Adopt(LoadingBar(), null, "randomizerHoldRing")) {
                Randomizer.log("hold ring: no loading bar to borrow; holds will have no ring");
                ring = null;
                ringless = true;
                return;
            }

            ring.Match(glyph, glyph.gameObject.layer);
            ring.Fade(1f);
        }

        ring.Show(true);
        var at = glyph.transform.position;
        ring.Place(new Vector3(at.x, at.y, at.z - RingLift));
        ring.Widen(glyph.bounds.size.y * RingSpan);
        ring.Progress(progress);
    }

    public void HideHold() {
        if (ring != null) {
            ring.Show(false);
        }
    }

    // The loading bar carries the provider that reads the prewarmer, which is what names it
    // among the handful of things alive from boot -- Sein's UI, and its ring, are not.
    private static GameObject LoadingBar() {
        foreach (var progress in Resources.FindObjectsOfTypeAll<UberShaderPrewarmerProgress>()) {
            if (progress != null && progress.GetComponent<TimelineSequence>() != null) {
                return progress.gameObject;
            }
        }

        return null;
    }

    // The leftmost key glyph in a legend slot, which is the key that slot is offering to hold:
    // a hold's hint is written first in it. Leftmost rather than first, because the icons are
    // cloned in the order they were needed and keep it when the text changes under them.
    public Renderer HoldGlyph(string name) {
        var slot = transform.FindChild("highlightFade/legend/pcLegend/" + name);
        var icons = slot == null ? null : slot.GetComponentInChildren<CatlikeCoding.TextBox.MoonIconRenderer>(true);
        if (icons == null) {
            return null;
        }

        Renderer leftmost = null;
        foreach (var renderer in icons.GetComponentsInChildren<Renderer>(true)) {
            if (leftmost == null || renderer.transform.position.x < leftmost.transform.position.x) {
                leftmost = renderer;
            }
        }

        return leftmost;
    }

    // Vanilla's three hints are short and sit with a clear band between them; ours are long
    // enough to close those gaps up. The outer two move apart into the empty screen either
    // side of the legend, which is where the room is.
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

    // Two lines of tooltip over the legend, which is the pair the vanilla screens show. The
    // panel is lifted into the margin above its first row to buy back a row, and the answer
    // is how many rows fit above the footer. Call instead of ScrollAfter's magic number.
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
        // without this the row keeps the tooltip of the vanilla one it was cloned from
        ConfigureTooltip(cleverMenuItem.GetComponent<CleverMenuItemTooltip>(), tooltip ?? caption);
    }

    // A row that is only a label. It sits in both lists so the scroll window's arithmetic still
    // lines up, but navigation steps over it (an Activated that never validates) and the cursor
    // cannot land on it (no bounds of its own).
    public void AddHeader(string caption) {
        var cleverMenuItem = AddItem(caption);
        cleverMenuItem.gameObject.name = "Header (" + caption + ")";
        cleverMenuItem.Size = Vector2.zero;
        // the row tints its own text, so the colour has to come from there rather than a
        // <style> tag; all three states, because a header is never in any of them
        cleverMenuItem.Transition.NormalColor = HeaderColor;
        cleverMenuItem.Transition.HighlightedColor = HeaderColor;
        cleverMenuItem.Transition.DisabledColor = HeaderColor;
        cleverMenuItem.OnUnhighlight();
        cleverMenuItem.Activated = cleverMenuItem.gameObject.AddComponent<NeverCondition>();
        var state = cleverMenuItem.transform.Find("text/stateText").GetComponent<MessageBox>();
        state.MessageProvider = null;
        state.SetMessage(new MessageDescriptor(string.Empty));
    }

    public void AddRandomizerBind(string action, string label = null) {
        var cleverMenuItem = AddItem(label ?? action);
        cleverMenuItem.gameObject.name = "Rando Bind (" + action + ")";
        var control = cleverMenuItem.gameObject.AddComponent<RandomizerBindControl>();
        control.Init(action, this, label ?? action);
        cleverMenuItem.PressedCallback += delegate { control.BeginEditing(); };
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

        ConfigureTooltip(cleverMenuItem.GetComponent<CleverMenuItemTooltip>(), tooltip);
    }

    // where a row prints its value, in the row's own space
    private float ValueColumnX(CleverMenuItem row) {
        var state = row.transform.Find("text/stateText");
        return state ? row.transform.InverseTransformPoint(state.position).x : SwatchX;
    }

    // label overrides the row's caption: a setting's name is a file key first, and some of
    // them are longer than a row.
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
            // a message background gets a wider tile: it is previewing a message, and a
            // square reads as a color chip rather than as the thing it will look like
            var wide = asMessage ? MessageSwatchWidth : 1f;
            var width = SwatchSize * wide;
            placed.localScale = new Vector3(width, SwatchSize * 0.55f, 1f);
            // centred quad: the message art fades in from its edge, a painted swatch does not
            var margin = asMessage ? SwatchMargin : 0f;
            placed.localPosition = new Vector3(ValueColumnX(cleverMenuItem) + (0.5f - margin) * width, 0f, 0f);
        }

        // the control owns the row's tooltip, so it can swap in the editing keys and back
        var control = cleverMenuItem.gameObject.AddComponent<ColorControl>();
        control.Init(setting, this, placed, tooltip, asMessage);
        cleverMenuItem.PressedCallback += control.BeginEditing;
    }

    // Template is the Language picker: a row whose child flyout is a CleverMenuOptionsList.
    // Its subclass only exists to refill itself with languages, so it is swapped out for
    // the bare base class -- pressing the row is handled by the group, not by an action.
    // label overrides the row's caption, as on AddColor: the setting's name is a file key
    // first, and the words that read best there are not always the ones for a menu.
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

        state.MessageProvider = null;
        state.SetMessage(new MessageDescriptor(EnumLabel(setting.Value)));
        ConfigureTooltip(clone.GetComponent<CleverMenuItemTooltip>(), tooltip);
        FitBackground(flyout, list.Spacing, members.IndexOf(setting.Value.ToString()));
        FlyoutTooltips(flyout, setting, members, tooltip);
    }

    // The panel is drawn for the language list and is padded for its eight rows, so it
    // is not enough to scale it -- it has to be re-centred on the rows that are actually
    // there. Measured off their transforms, which sidesteps how Origin is nested and the
    // panel's own 270-degree rotation (its local x is the screen's vertical).
    private void FitBackground(Transform flyout, float spacing, int selected) {
        var bg = flyout.FindChild("abilityMessageBackground");
        var mesh = bg == null ? null : bg.GetComponent<MeshFilter>();
        var rows = flyout.GetComponent<CleverMenuItemSelectionManager>().MenuItems;
        if (mesh == null || rows.Count == 0) {
            return;
        }

        var top = flyout.InverseTransformPoint(rows[0].transform.position).y;
        var bottom = flyout.InverseTransformPoint(rows[rows.Count - 1].transform.position).y;

        // The flyout carries a positive offset from the vanilla prefab, which is what put it
        // above the row that spawns it. Anchor on the option that is *currently set*, so the
        // value stays where it was reading a moment ago instead of jumping.
        var anchor = -top + Mathf.Max(0, selected) * spacing;
        flyout.localPosition = new Vector3(flyout.localPosition.x, anchor, flyout.localPosition.z);

        // measured off the rows rather than trusting Spacing, which is the list's own idea
        // of a gap and not necessarily the one on screen
        var gap = rows.Count > 1 ? Mathf.Abs(top - bottom) / (rows.Count - 1) : spacing;
        var wanted = Mathf.Abs(top - bottom) + BackgroundPad * gap;
        // the panel art carries transparent margin, so the quad has to be bigger than the
        // rows it is meant to sit behind
        bg.localScale = new Vector3(wanted / mesh.sharedMesh.bounds.size.x,
                                    bg.localScale.y + 2.2f * gap, bg.localScale.z);
        bg.localPosition = new Vector3(bg.localPosition.x, 0.5f * (top + bottom), bg.localPosition.z);
    }

    // The flyout has its own selection, so the screen's one tooltip is pointed at it while
    // it is open and handed back on the way out.
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

    // The file comment already documents each value as "<Value>: what it does", so the
    // per-value help is read back out of it rather than written twice.
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

    // The pause menu's "Return to Main Menu?" prompt, cloned off the InstantiateAction that
    // normally spawns it. Its own selection manager lists the answers; Back answers -1.
    public void Confirm(string question, string[] answers, Action<int> chosen) {
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
            Randomizer.log("settings: no confirm prefab, answering " + answers[0]);
            chosen(0);
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

        // The prefab wraps at the width of "Return to Main Menu?", and a longer question
        // wrapped onto the answers. Widen before the text is set, so it renders once.
        var title = prompt.transform.FindChild("title");
        var titleBox = title == null ? null : title.GetComponentInChildren<MessageBox>(true);
        if (titleBox != null && titleBox.TextBox != null) {
            titleBox.TextBox.width *= QuestionWidth;
        }

        Ask(title, question);

        // The plate widens with the question, and grows upward for a line of headroom over
        // it rather than moving the text down, so the answers stay where the prefab puts them.
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
            // vanilla rows quit to the menu through Pressed, and gate that on being safe to
            // quit; ours are always answerable and only report the answer
            item.Pressed = null;
            item.Activated = null;
            item.Visible = null;
            item.PressedCallback += delegate { CloseConfirm(chosen, answer); };
        }

        manager.BackGuard = delegate {
            CloseConfirm(chosen, -1);
            return false;
        };

        // The page behind stays readable but must stop responding. IsActive gates its keys
        // only -- a click reaches a row ahead of that check -- and the row it left lit would
        // otherwise go on glowing under the question.
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
        // after the answer, because that is what decides whether anything is still unsaved
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

        // the clone came with the language rows registered; destroying the objects leaves
        // the selection manager pointing at the corpses, and SetIndexToFirst lands on one
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

        // Set up slider properties
        slider.MinValue = min;
        slider.MaxValue = max;
        slider.Step = step;
        (slider as MusicVolumeSlider).Setting = setting;

        // Update label
        var nameTextBox = clone.transform.Find("nameText").GetComponent<MessageBox>();
        nameTextBox.MessageProvider = null;
        nameTextBox.SetMessage(new MessageDescriptor(setting.Name));

        // Update tooltip
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

    // How far the outer two slots stand off the middle one, per line, measured so that every
    // gap between hints comes out near sixty pixels of screen. A slot carrying more reaches
    // further on its own and needs less; the editing line's mode hint and the dirty line's
    // three hints on the right are where that shows.
    private const float SlotShift = 0.35f;

    private const float ModeShift = 0.5f;

    private const float SavingLeft = 0.1f;

    private const float SavingRight = 0.25f;

    private Vector2 spread;

    private bool widened;

    // warmer and flatter than a row's own white, so a category reads as a label
    private static readonly Color HeaderColor = new Color(0.85f, 0.72f, 0.42f, 0.55f);

    private GameObject prompt;

    // by eye against a key glyph: the ring reads as around it rather than behind it
    private const float RingSpan = 1.875f;

    private const float RingLift = 0.1f;

    private static RandomizerHoldRing ring;

    private static bool ringless;

    // long enough to cover the manager's next FixedUpdate whichever order the two run in
    private const int SettleFrames = 3;

    private int settle;

    // when the save hold started, or -1 for no hold in hand
    private float saving = -1f;

    // The game's own placeholder for the Soul Link key, which a MessageBox resolves to the
    // keycap when there is one for that key and to the key's name when there is not.
    private const string SoulKey = "[SoulFlame]";

    private bool soulGlyph;

    // Erase, at both scales: the last bind of the row being edited, or every bind on the page.
    private const KeyCode EraseKey = KeyCode.Backspace;

    private const string BackupSuffix = ".before-reset";

    // empty until the rows are built: the legend asks whether they are dirty on the way up
    private KeybindControl[] keyControls = new KeybindControl[0];

    private ControllerBindControl[] padControls = new ControllerBindControl[0];

    private RandomizerBindControl[] randoControls = new RandomizerBindControl[0];

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

    // Of the prefab's own. Both by eye against the longest question the screens ask; the
    // plate starts out much wider than the wrap, so it needs less.
    private const float QuestionWidth = 1.6f;

    private const float PlateWidth = 1.35f;

    private const string QuestionPrefabName = "returnToMainMenuQuestion";

    private static GameObject questionPrefab;

    public string DefaultTooltip = "Click on an action to add or remove binds";

    // placed by eye against a running screen; the bar hangs right of the rows
    public float BarX = 6.5f;

    public float EdgeFade = 0.35f;

    // the stroke is a fifth of its texture's width, so the quad is wider than the bar
    // how far either side of the bar still counts as grabbing it
    public float ScrollGrab = 0.5f;

    public float BarWidth = 0.26f;

    // fallback only: a row missing its state text still needs somewhere to put the swatch
    public float SwatchX = 3.7f;

    // the message art fades in over this fraction of its quad's width
    public float SwatchMargin = 0.162f;

    // tall enough to read, short enough that two color rows do not touch
    public float SwatchSize = 0.62f;

    public float MessageSwatchWidth = 4.4f;

    // Panel padding past the first and last row, in row heights. 2.1 is what the vanilla
    // language panel works out to, so a short list is padded like a long one.
    public float BackgroundPad = 7.2f;

    private float barLength;

    private Transform scrollBar;

    private Transform scrollTrack;

    private Transform scrollThumb;
}

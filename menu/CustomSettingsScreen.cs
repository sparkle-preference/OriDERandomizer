using System;
using System.ComponentModel;
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
        // Will only write if there have been changes
        RandomizerSettings.WriteSettings();
    }

    // The ways off this page that skip its own Back, shut while it has a question to ask:
    // Escape is bound to Pause as well as Cancel and the menu manager reads it first and
    // closes the whole screen, and the tab list takes clicks even while it is inactive.
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
        selectionManager.SetCurrentItem(0);
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
        Hold(prompt != null || (selectionManager.IsActive && BindsDirty));
        if (layout == null || layout.MaxVisible <= 0) {
            return;
        }

        // every key belongs to the bind being edited, including these
        if (!Editing) {
            RapidScroll();
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
        if (keyControls.Length == 0 && padControls.Length == 0) {
            return;
        }

        if (Editing) {
            Legend(string.Empty, "<icon>D</> Finish", "Backspace  Remove last");
            return;
        }

        Legend("<icon>vr</> Navigate", "<icon>D</> Rebind",
            BindsDirty ? "<icon>y</> Back  (unsaved)" : "<icon>y</> Back");
    }

    // The legend's three slots. Key icons come out of the text itself -- <icon> switches to a
    // font whose letters are key images: D is Enter, y Esc, M Del, vr the up and down arrows,
    // st left and right. More than one hint fits in a slot.
    public void Legend(string navigate, string select, string back) {
        var legend = transform.FindChild("highlightFade/legend/pcLegend");
        if (legend == null) {
            return;
        }

        Slot(legend, "navigate", navigate);
        Slot(legend, "select", select);
        Slot(legend, "back", back);
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

        Ask(prompt.transform.FindChild("title"), question);

        // A line of headroom over the question. The box grows upward rather than the text
        // moving down, so the answers stay where the prefab puts them.
        var back = prompt.transform.FindChild("messageBackgroundA");
        var mesh = back == null ? null : back.GetComponent<MeshFilter>();
        if (mesh != null && mesh.sharedMesh != null && mesh.sharedMesh.bounds.size.y > 0f) {
            var grow = TopPad / mesh.sharedMesh.bounds.size.y;
            back.localScale = new Vector3(back.localScale.x, back.localScale.y + grow, back.localScale.z);
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

    private GameObject prompt;

    // empty until the rows are built: the legend asks whether they are dirty on the way up
    private KeybindControl[] keyControls = new KeybindControl[0];

    private ControllerBindControl[] padControls = new ControllerBindControl[0];

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

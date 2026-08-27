using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public abstract class CustomSettingsScreen : MonoBehaviour {
    public void OnDisable() {
        // Will only write if there have been changes
        RandomizerSettings.WriteSettings();
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
    }

    public void AddKeybind(string label, Func<KeyCode[]> getKeys, Action<KeyCode[]> setKeys) {
        var cleverMenuItem = AddItem(label);
        cleverMenuItem.gameObject.name = "Keybind (" + label + ")";
        var kc = cleverMenuItem.gameObject.AddComponent<KeybindControl>();
        kc.Init(getKeys, setKeys, this);
        cleverMenuItem.PressedCallback += delegate { kc.BeginEditing(); };
    }

    public abstract void InitScreen();

    // Call from InitScreen. The window follows the selection, so the re-sort has to hang
    // off the selection change rather than off Update.
    public void ScrollAfter(int rows) {
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
        if (layout == null || layout.MaxVisible <= 0) {
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
        kc.Init(getKeys, setKeys, this);
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
            // square reads as a colour chip rather than as the thing it will look like
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

    // LanguageOptions repopulates itself in OnEnable, so it has to go rather than be
    // emptied. DestroyImmediate: a deferred Destroy would still refill on the way in.
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

    // tall enough to read, short enough that two colour rows do not touch
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

using System;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class CustomSettingsScreen : MonoBehaviour {
    public void OnDisable() {
        // Will only write if there have been changes
        RandomizerSettings.WriteSettings();
    }

    public virtual void Awake() {
        // Layout and selection manager
        Layout = GetComponent<CleverMenuItemLayout>();
        SelectionManager = GetComponent<CleverMenuItemSelectionManager>();
        Group = GetComponent<CleverMenuItemGroup>();
        Layout.MenuItems.Clear();
        SelectionManager.MenuItems.Clear();
        Group.Options.Clear();
        Pivot = transform.FindChild("highlightFade/pivot");
        foreach (var obj in Pivot) {
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
        tooltip.SetParent(Pivot);
        tooltip.position = originalToolip.position;
        TooltipController = tooltip.GetComponent<CleverMenuItemTooltipController>();
        TooltipController.Selection = SelectionManager;
        TooltipController.UpdateTooltip();
        TooltipController.enabled = true;

        InitScreen();
        SelectionManager.SetCurrentItem(0);
    }

    public void AddKeybind(string label, Func<KeyCode[]> getKeys, Action<KeyCode[]> setKeys) {
        var cleverMenuItem = AddItem(label);
        cleverMenuItem.gameObject.name = "Keybind (" + label + ")";
        var kc = cleverMenuItem.gameObject.AddComponent<KeybindControl>();
        kc.Init(getKeys, setKeys, this);
        cleverMenuItem.PressedCallback += delegate { kc.BeginEditing(); };
    }

    public abstract void InitScreen();

    public void HideLegend() {
        Destroy(transform.FindChild("highlightFade/legend").gameObject);
    }

    public void AddButton(string caption, Action onClick) {
        var cleverMenuItem = AddItem("");
        cleverMenuItem.gameObject.name = "Button (" + caption + ")";
        cleverMenuItem.gameObject.transform.Find("text/stateText").GetComponent<MessageBox>().SetMessage(new MessageDescriptor(caption));
        cleverMenuItem.PressedCallback += onClick;
    }

    public void AddControllerBind(string label, Func<PlayerInputRebinding.ControllerButton[]> getKeys, Action<PlayerInputRebinding.ControllerButton[]> setKeys) {
        var cleverMenuItem = AddItem(label);
        cleverMenuItem.gameObject.name = "Controller Bind (" + label + ")";
        var kc = cleverMenuItem.gameObject.AddComponent<ControllerBindControl>();
        kc.Init(getKeys, setKeys, this);
        cleverMenuItem.PressedCallback += delegate { kc.BeginEditing(); };
    }

    private void AddToLayout(CleverMenuItem item) {
        Layout.AddItem(item);
        Layout.Sort();
        item.SetOpacity(1f);
        item.OnUnhighlight();
    }

    public CleverMenuItem AddItem(string label) {
        var gameObject = Instantiate(SettingsScreen.Instance.transform.Find("highlightFade/pivot/damageText").gameObject);
        gameObject.transform.SetParent(Pivot);
        foreach (var c in gameObject.GetComponentsInChildren<MonoBehaviour>()) {
            c.enabled = true;
        }

        var component = gameObject.GetComponent<CleverMenuItem>();
        component.Pressed = null;
        SelectionManager.MenuItems.Add(component);
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

    public void AddSlider(RandomizerSettings.FloatSetting setting, float min, float max, float step, string tooltip) {
        // Template is music volume slider
        var clone = Instantiate(SettingsScreen.Instance.transform.Find("highlightFade/pivot/musicVolume").gameObject);
        clone.gameObject.name = setting.Name;
        foreach (var c in clone.GetComponentsInChildren<MonoBehaviour>()) {
            c.enabled = true;
        }

        // Add to navigation manager (required for all option types)
        clone.transform.SetParent(Pivot);
        var cleverMenuItem = clone.GetComponent<CleverMenuItem>();
        SelectionManager.MenuItems.Add(cleverMenuItem);
        AddToLayout(cleverMenuItem);

        // Add to group (required for sliders and dropdown items, but not toggles)
        var slider = clone.transform.FindChild("slider").GetComponent<CleverValueSlider>();
        slider.NavigateMessageBoxes = new[] {
            transform.FindChild("highlightFade/legend/pcLegend/navigate").GetComponent<MessageBox>(),
            transform.FindChild("highlightFade/legend/xBoxLegend/navigate").GetComponent<MessageBox>(),
        };
        Group.AddItem(cleverMenuItem, slider);

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

    [FormerlySerializedAs("layout")] public CleverMenuItemLayout Layout;

    [FormerlySerializedAs("selectionManager")] public CleverMenuItemSelectionManager SelectionManager;

    [FormerlySerializedAs("pivot")] public Transform Pivot;

    [FormerlySerializedAs("group")] public CleverMenuItemGroup Group;

    [FormerlySerializedAs("fakeTooltip")] public CleverMenuItem FakeTooltip;

    [FormerlySerializedAs("tooltipController")] public CleverMenuItemTooltipController TooltipController;

    public string DefaultTooltip = "Click on an action to add or remove binds";
}

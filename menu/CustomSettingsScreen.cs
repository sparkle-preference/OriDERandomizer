using System;
using UnityEngine;

public abstract class CustomSettingsScreen : MonoBehaviour
{
    public void OnDisable()
    {
        // Will only write if there have been changes
        RandomizerSettings.WriteSettings();
    }

    public virtual void Awake()
    {
        // Layout and selection manager
        layout = GetComponent<CleverMenuItemLayout>();
        selectionManager = GetComponent<CleverMenuItemSelectionManager>();
        group = GetComponent<CleverMenuItemGroup>();
        layout.MenuItems.Clear();
        selectionManager.MenuItems.Clear();
        group.Options.Clear();
        pivot = transform.FindChild("highlightFade/pivot");
        foreach (object obj in pivot)
        {
            Destroy(((Transform)obj).gameObject);
        }

        TransparencyAnimator[] componentsInChildren = GetComponentsInChildren<TransparencyAnimator>();
        for (int i = 0; i < componentsInChildren.Length; i++)
        {
            if (componentsInChildren[i].gameObject != gameObject)
            {
                componentsInChildren[i].Reset();
            }
        }

        // Tooltip
        Transform originalToolip = SettingsScreen.Instance.transform.Find("highlightFade/pivot/tooltip");
        Transform tooltip = Instantiate(originalToolip);
        tooltip.SetParent(pivot);
        tooltip.position = originalToolip.position;
        tooltipController = tooltip.GetComponent<CleverMenuItemTooltipController>();
        tooltipController.Selection = selectionManager;
        tooltipController.UpdateTooltip();
        tooltipController.enabled = true;

        InitScreen();
        selectionManager.SetCurrentItem(0);
    }

    public void AddKeybind(string label, Func<KeyCode[]> getKeys, Action<KeyCode[]> setKeys)
    {
        CleverMenuItem cleverMenuItem = AddItem(label);
        cleverMenuItem.gameObject.name = "Keybind (" + label + ")";
        KeybindControl kc = cleverMenuItem.gameObject.AddComponent<KeybindControl>();
        kc.Init(getKeys, setKeys, this);
        cleverMenuItem.PressedCallback += delegate
        {
            kc.BeginEditing();
        };
    }

    public abstract void InitScreen();

    public void HideLegend()
    {
        Destroy(transform.FindChild("highlightFade/legend").gameObject);
    }

    public void AddButton(string caption, Action onClick)
    {
        CleverMenuItem cleverMenuItem = AddItem("");
        cleverMenuItem.gameObject.name = "Button (" + caption + ")";
        cleverMenuItem.gameObject.transform.Find("text/stateText").GetComponent<MessageBox>().SetMessage(new MessageDescriptor(caption));
        cleverMenuItem.PressedCallback += onClick;
    }

    public void AddControllerBind(string label, Func<PlayerInputRebinding.ControllerButton[]> getKeys, Action<PlayerInputRebinding.ControllerButton[]> setKeys)
    {
        CleverMenuItem cleverMenuItem = AddItem(label);
        cleverMenuItem.gameObject.name = "Controller Bind (" + label + ")";
        ControllerBindControl kc = cleverMenuItem.gameObject.AddComponent<ControllerBindControl>();
        kc.Init(getKeys, setKeys, this);
        cleverMenuItem.PressedCallback += delegate
        {
            kc.BeginEditing();
        };
    }

    private void AddToLayout(CleverMenuItem item)
    {
        layout.AddItem(item);
        layout.Sort();
        item.SetOpacity(1f);
        item.OnUnhighlight();
    }

    public CleverMenuItem AddItem(string label)
    {
        GameObject gameObject = Instantiate(SettingsScreen.Instance.transform.Find("highlightFade/pivot/damageText").gameObject);
        gameObject.transform.SetParent(pivot);
        foreach (var c in gameObject.GetComponentsInChildren<MonoBehaviour>())
            c.enabled = true;
        CleverMenuItem component = gameObject.GetComponent<CleverMenuItem>();
        component.Pressed = null;
        selectionManager.MenuItems.Add(component);
        AddToLayout(component);
        TransparencyAnimator[] componentsInChildren = component.transform.GetComponentsInChildren<TransparencyAnimator>();
        for (int i = 0; i < componentsInChildren.Length; i++)
        {
            componentsInChildren[i].Reset();
            componentsInChildren[i].enabled = true;
        }
        foreach (object obj in component.transform.FindChild("glowGroup"))
        {
            TransparencyAnimator.Register((Transform)obj);
        }
        gameObject.transform.Find("text/nameText").GetComponent<MessageBox>().SetMessage(new MessageDescriptor(label));
        return component;
    }

    public void AddToggle(RandomizerSettings.BoolSetting setting, string tooltip)
    {
        CleverMenuItem cleverMenuItem = AddItem(setting.Name);
        cleverMenuItem.name = setting.Name;
        ToggleCustomSettingsAction toggleCustomSettingsAction = cleverMenuItem.gameObject.AddComponent<ToggleCustomSettingsAction>();
        toggleCustomSettingsAction.Setting = setting;
        toggleCustomSettingsAction.Init();
        cleverMenuItem.PressedCallback += toggleCustomSettingsAction.Toggle;

        ConfigureTooltip(cleverMenuItem.GetComponent<CleverMenuItemTooltip>(), tooltip);
    }

    public void AddSlider(RandomizerSettings.FloatSetting setting, float min, float max, float step, string tooltip)
    {
        // Template is music volume slider
        GameObject clone = Instantiate(SettingsScreen.Instance.transform.Find("highlightFade/pivot/musicVolume").gameObject);
        clone.gameObject.name = setting.Name;
        foreach (var c in clone.GetComponentsInChildren<MonoBehaviour>())
            c.enabled = true;

        // Add to navigation manager (required for all option types)
        clone.transform.SetParent(pivot);
        CleverMenuItem cleverMenuItem = clone.GetComponent<CleverMenuItem>();
        selectionManager.MenuItems.Add(cleverMenuItem);
        AddToLayout(cleverMenuItem);

        // Add to group (required for sliders and dropdown items, but not toggles)
        CleverValueSlider slider = clone.transform.FindChild("slider").GetComponent<CleverValueSlider>();
        slider.NavigateMessageBoxes = new[]
		{
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
        MessageBox nameTextBox = clone.transform.Find("nameText").GetComponent<MessageBox>();
        nameTextBox.MessageProvider = null;
        nameTextBox.SetMessage(new MessageDescriptor(setting.Name));

        // Update tooltip
        ConfigureTooltip(clone.GetComponent<CleverMenuItemTooltip>(), tooltip);
    }

    private void ConfigureTooltip(CleverMenuItemTooltip tooltipComponent, string tooltip)
    {
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
}

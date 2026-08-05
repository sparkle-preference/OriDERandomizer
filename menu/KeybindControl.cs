using System;
using System.Collections.Generic;
using UnityEngine;

public class KeybindControl : MonoBehaviour {
    private void Awake() {
        messageBox = transform.Find("text/stateText").GetComponent<MessageBox>();
    }

    public void BeginEditing() {
        currentKeys.Clear();
        currentKeys.AddRange(getKeys());
        SuspensionManager.SuspendAll();
        editing = true;
        exit = 0;
        tooltipProvider.SetMessage("Backspace: remove bind\nEnter: finish editing");
        owner.TooltipController.UpdateTooltip();
    }

    public void Update() {
        if (!editing) {
            return;
        }

        if (exit < 2) {
            exit++;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) && currentKeys.Count > 0) {
            editing = false;
            SuspensionManager.ResumeAll();
            setKeys(currentKeys.ToArray());
            PlayerInputRebinding.WriteKeyRebindSettings();
            PlayerInput.Instance.RefreshControlScheme();
            tooltipProvider.SetMessage(owner.DefaultTooltip);
            owner.TooltipController.UpdateTooltip();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Backspace)) {
            if (currentKeys.Count > 0) {
                currentKeys.RemoveAt(currentKeys.Count - 1);
                UpdateMessageBox();
            }
        } else if (Input.anyKeyDown) {
            foreach (var obj in Enum.GetValues(typeof(KeyCode))) {
                var keyCode = (KeyCode)obj;
                if (Input.GetKeyDown(keyCode) && !currentKeys.Contains(keyCode)) {
                    currentKeys.Add(keyCode);
                    UpdateMessageBox();
                }
            }
        }
    }

    private void UpdateMessageBox() {
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(currentKeys.ToArray())));
    }

    public static string KeyBindingToString(KeyCode[] codes) {
        var text = string.Empty;
        var flag = true;
        foreach (var keyCode in codes) {
            text += !flag ? ", " : string.Empty;
            text += keyCode;
            flag = false;
        }

        return text;
    }

    public void Reset() {
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(getKeys())));
        editing = false;
    }

    public void Init(Func<KeyCode[]> getKeys, Action<KeyCode[]> setKeys, CustomSettingsScreen owner) {
        this.owner = owner;
        this.getKeys = getKeys;
        this.setKeys = setKeys;
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(getKeys())));
        var component = GetComponent<CleverMenuItemTooltip>();
        tooltipProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        tooltipProvider.SetMessage(owner.DefaultTooltip);
        component.Tooltip = tooltipProvider;
        owner.TooltipController.UpdateTooltip();
    }

    private Func<KeyCode[]> getKeys;

    private Action<KeyCode[]> setKeys;

    private bool editing;

    private MessageBox messageBox;

    private List<KeyCode> currentKeys = new List<KeyCode>();

    private int exit;

    private CustomSettingsScreen owner;

    private RandomizerMessageProvider tooltipProvider;
}

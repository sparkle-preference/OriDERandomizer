using System;
using System.Collections.Generic;
using UnityEngine;

public class KeybindControl : MonoBehaviour {
    private void Awake() {
        messageBox = transform.Find("text/stateText").GetComponent<MessageBox>();
    }

    public void BeginEditing() {
        currentKeys.Clear();
        currentKeys.AddRange(GetKeys());
        SuspensionManager.SuspendAll();
        editing = true;
        owner.Editing = true;
        exit = 0;
        // the legend says how to edit; the tooltip names what is being edited
        owner.BindLegend();
        Tooltip("Editing binds for " + label + "...");
    }

    private void Tooltip(string words) {
        tooltipProvider.SetMessage(words);
        owner.tooltipController.UpdateTooltip();
    }

    public void Update() {
        if (!editing) {
            return;
        }

        if (exit < 2) {
            exit++;
            return;
        }

        if (Cancelling()) {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) && currentKeys.Count > 0) {
            editing = false;
            owner.Editing = false;
            SuspensionManager.ResumeAll();
            SetKeys(currentKeys.ToArray());
            PlayerInputRebinding.WriteKeyRebindSettings();
            var input = PlayerInput.Instance;
            if (input != null) {
                input.RefreshControlScheme();
            }

            owner.BindLegend();
            Tooltip(owner.DefaultTooltip);
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

    // A Back hold cancels the edit; a tap is a key like any other and binds on release.
    private bool Cancelling() {
        var down = CustomSettingsScreen.BackHeld();
        if (down == KeyCode.None) {
            if (tapped != KeyCode.None && Time.unscaledTime - held < RandomizerHoldRing.Tap) {
                Bind(tapped);
            }

            held = -1f;
            tapped = KeyCode.None;
            owner.HideHold();
            return false;
        }

        if (held < 0f) {
            held = Time.unscaledTime;
            tapped = down;
        }

        var progress = (Time.unscaledTime - held) / RandomizerHoldRing.Seconds;
        owner.DrawHold(progress);

        if (progress < 1f) {
            return true;
        }

        // the hold spent the key, so letting go of it must not also bind it
        tapped = KeyCode.None;
        owner.HideHold();
        Cancel();
        return true;
    }

    private void Bind(KeyCode key) {
        if (!currentKeys.Contains(key)) {
            currentKeys.Add(key);
            UpdateMessageBox();
        }
    }

    // nothing is applied until Enter, so cancelling only redraws the row and stands down
    private void Cancel() {
        held = -1f;
        editing = false;
        owner.Editing = false;
        SuspensionManager.ResumeAll();
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(GetKeys())));
        owner.BindLegend();
        Tooltip(owner.DefaultTooltip);
        owner.HoldMenu();
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
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(GetKeys())));
        editing = false;
        owner.Editing = false;
    }

    // the keys the screen was entered with; Restore puts them back
    public void Snapshot() {
        snapshot = (KeyCode[])GetKeys().Clone();
    }

    public bool Changed {
        get {
            if (snapshot == null) {
                return false;
            }

            var now = GetKeys();
            if (now.Length != snapshot.Length) {
                return true;
            }

            for (var i = 0; i < now.Length; i++) {
                if (now[i] != snapshot[i]) {
                    return true;
                }
            }

            return false;
        }
    }

    public void Restore() {
        if (snapshot == null) {
            return;
        }

        SetKeys((KeyCode[])snapshot.Clone());
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(GetKeys())));
    }

    public void Init(Func<KeyCode[]> getKeys, Action<KeyCode[]> setKeys, CustomSettingsScreen owner, string label) {
        this.owner = owner;
        this.label = label;
        GetKeys = getKeys;
        SetKeys = setKeys;
        messageBox.SetMessage(new MessageDescriptor(KeyBindingToString(getKeys())));
        var component = GetComponent<CleverMenuItemTooltip>();
        tooltipProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        tooltipProvider.SetMessage(owner.DefaultTooltip);
        component.Tooltip = tooltipProvider;
        owner.tooltipController.UpdateTooltip();
    }

    private Func<KeyCode[]> GetKeys;

    private Action<KeyCode[]> SetKeys;


    private MessageBox messageBox;

    // this control is taking keys; owner.Editing only says that *some* control is
    private bool editing;

    private KeyCode[] snapshot;

    private List<KeyCode> currentKeys = new List<KeyCode>();

    private int exit;

    private CustomSettingsScreen owner;

    private string label;

    private float held = -1f;

    private KeyCode tapped = KeyCode.None;

    private RandomizerMessageProvider tooltipProvider;
}

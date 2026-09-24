using System;
using System.Collections.Generic;
using UnityEngine;

// Edits one rando bind: comma-separated chords, each landing when all its keys are let go.
// Reads keys, or the game actions they are bound to; a Tab hold swaps between the two.
public class RandomizerBindControl : MonoBehaviour {
    public enum Mode {
        Keys,
        Actions
    }

    private void Awake() {
        messageBox = transform.Find("text/stateText").GetComponent<MessageBox>();
    }

    public void Init(string action, CustomSettingsScreen owner, string label, string help) {
        this.owner = owner;
        this.action = action;
        this.label = label;
        this.help = help ?? owner.DefaultTooltip;
        Show();
        tooltipProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        tooltipProvider.SetMessage(this.help);
        GetComponent<CleverMenuItemTooltip>().Tooltip = tooltipProvider;
        owner.tooltipController.UpdateTooltip();
        back = new BindHold(p => owner.DrawHold(p), owner.HideHold, Press, Join, Cancel);
        swap = new BindHold(p => owner.DrawHold(p, "navigate"), owner.HideHold, Press, Join, Swap);
    }

    public void BeginEditing() {
        chords = Current();
        SuspensionManager.SuspendAll();
        editing = true;
        owner.Editing = true;
        exit = 0;
        mode = Mode.Keys;
        owner.ReadingActions = false;
        armed = false;
        peak.Clear();
        back.Reset();
        swap.Reset();
        denyAt = -1f;
        owner.BindLegend();
        Tooltip();
    }

    public void Update() {
        if (!editing) {
            return;
        }

        if (exit < 2) {
            exit++;
            return;
        }

        // a key pressed during a Back or Tab hold joins the chord
        if (armed && Input.anyKeyDown) {
            back.Join();
            swap.Join();
        }

        // A Back hold cancels the edit; a tap is a key like any other and binds on release.
        if (back.Update(CustomSettingsScreen.BackHeld(), Time.unscaledTime)) {
            return;
        }

        // whatever was already down belongs to the press that opened the edit
        if (!armed) {
            armed = Quiet();
            return;
        }

        if (Bare(KeyCode.Return) || Bare(KeyCode.KeypadEnter)) {
            Finish();
            return;
        }

        if (Bare(KeyCode.Backspace)) {
            RemoveLast();
            return;
        }

        // A Tab hold swaps keys/actions; a tap, or Tab with a chord in hand, is just a key.
        if (swap.Update(Input.GetKey(KeyCode.Tab) ? KeyCode.Tab : KeyCode.None, Time.unscaledTime, Empty)) {
            return;
        }

        Collect();
    }

    // pressed with no chord in hand
    private bool Bare(KeyCode key) {
        return Input.GetKeyDown(key) && Empty;
    }

    // no chord in hand; a held Back or Tab that joined one counts as one
    private bool Empty {
        get { return peak.Count == 0 && !back.Joined && !swap.Joined; }
    }

    // triggers, sticks and the D-pad are axes, which anyKey does not see
    private bool Quiet() {
        if (Input.anyKey) {
            return false;
        }

        if (mode == Mode.Actions) {
            foreach (var pair in RandomizerRebinding.CoreInputMap) {
                if (pair.Value.Pressed) {
                    return false;
                }
            }
        }

        return true;
    }

    // in Actions mode a held key counts through its action, as any held key does
    private void Join(KeyCode key) {
        if (mode == Mode.Keys) {
            Press(key);
        }
    }

    private void Swap() {
        mode = mode == Mode.Keys ? Mode.Actions : Mode.Keys;
        owner.ReadingActions = mode == Mode.Actions;
        peak.Clear();
        denyAt = -1f;
        // wait for Tab to come up, or it swaps straight back
        armed = false;
        owner.BindLegend();
        Tooltip();
        UpdateMessageBox();
    }

    // The chord grows while anything is down and lands when the board is clear again.
    private void Collect() {
        if (mode == Mode.Keys) {
            if (Input.anyKeyDown) {
                foreach (var raw in Enum.GetValues(typeof(KeyCode))) {
                    var key = (KeyCode)raw;
                    if (Input.GetKeyDown(key)) {
                        Press(key);
                    }
                }
            }
        } else {
            var grew = false;
            foreach (var pair in RandomizerRebinding.CoreInputMap) {
                if (pair.Value.Pressed) {
                    grew = Add(pair.Key) || grew;
                }
            }

            // a key on no action gets one refusal per chord, once a fixed step (when actions move) grew nothing
            if (grew || peak.Count > 0) {
                denyAt = -1f;
            } else if (Input.anyKeyDown && denyAt < 0f) {
                denyAt = Time.fixedTime;
            } else if (denyAt >= 0f && Time.fixedTime > denyAt) {
                denyAt = -1f;
                Deny();
            }
        }

        if (peak.Count > 0 && Quiet()) {
            Commit();
        }
    }

    private void Press(KeyCode key) {
        var side = RandomizerRebinding.SingleInput.SideOf(key);
        Add(side ?? key.ToString());
    }

    private bool Add(string name) {
        if (peak.Contains(name)) {
            return false;
        }

        peak.Add(name);
        UpdateMessageBox();
        return true;
    }

    private void Commit() {
        var chord = Chord();
        peak.Clear();
        if (chord == null) {
            Deny();
        } else if (!chords.Contains(chord)) {
            chords.Add(chord);
        }

        UpdateMessageBox();
    }

    // Modifiers first, then press order; null for modifiers alone, which are refused.
    private string Chord() {
        var modifiers = new List<string>();
        var rest = new List<string>();
        foreach (var name in peak) {
            if (RandomizerRebinding.SingleInput.Unside(name) != null) {
                modifiers.Add(name);
            } else {
                rest.Add(name);
            }
        }

        if (rest.Count == 0) {
            return null;
        }

        modifiers.AddRange(rest);
        return String.Join("+", modifiers.ToArray());
    }

    // a Required action keeps its last chord
    private void RemoveLast() {
        if (chords.Count == 0 || (chords.Count == 1 && RandomizerRebinding.Required.Contains(action))) {
            Deny();
            return;
        }

        chords.RemoveAt(chords.Count - 1);
        UpdateMessageBox();
    }

    private void Finish() {
        Apply(Text());
        Stop();
    }

    // Nothing is written until the edit ends, so abandoning it is just standing down.
    private void Cancel() {
        Show();
        Stop();
    }

    private void Stop() {
        editing = false;
        owner.Editing = false;
        peak.Clear();
        SuspensionManager.ResumeAll();
        owner.BindLegend();
        Tooltip();
        owner.HoldMenu();
    }

    private void Apply(string text) {
        RandomizerRebinding.SetBinds(action, text);
        RandomizerRebinding.WriteBindsToFile();
        Show();
    }

    private string Text() {
        return chords.Count == 0 ? RandomizerRebinding.Unbound : String.Join(", ", chords.ToArray());
    }

    // What the file would say, which is also what the row says.
    private string Live() {
        var set = RandomizerRebinding.BindNamed(action);
        return set != null && set.HasBind() ? set.ToString() : RandomizerRebinding.Unbound;
    }

    private List<string> Current() {
        var text = Live();
        var found = new List<string>();
        if (text == RandomizerRebinding.Unbound) {
            return found;
        }

        foreach (var chord in text.Split(',')) {
            if (chord.Trim() != "") {
                found.Add(chord.Trim());
            }
        }

        return found;
    }

    private void Show() {
        messageBox.SetMessage(new MessageDescriptor(Live()));
    }

    // the chord still being held trails a +
    private void UpdateMessageBox() {
        var text = String.Join(", ", chords.ToArray());
        if (peak.Count > 0) {
            text += (text == "" ? "" : ", ") + String.Join("+", peak.ToArray()) + "+";
        }

        messageBox.SetMessage(new MessageDescriptor(text == "" ? RandomizerRebinding.Unbound : text));
    }

    private void Tooltip() {
        tooltipProvider.SetMessage(editing
            ? "Editing binds for " + label + " (" + (mode == Mode.Keys ? "keys" : "game actions") + ")..."
            : help);
        owner.tooltipController.UpdateTooltip();
    }

    // Ori's not-enough-energy sound, cached once found; silent while there is no Sein to find it on.
    private void Deny() {
        if (denial == null) {
            foreach (var ability in Resources.FindObjectsOfTypeAll<SeinChargeFlameAbility>()) {
                if (ability != null && ability.NotEnoughEnergySound != null) {
                    denial = ability.NotEnoughEnergySound;
                    break;
                }
            }
        }

        if (denial != null) {
            Core.Sound.Play(denial.GetSound(null), transform.position, null);
        }
    }

    public void Reset() {
        Show();
        editing = false;
        owner.Editing = false;
    }

    // Binds apply live; Restore puts this back and the screen writes the file.
    public void Snapshot() {
        snapshot = Live();
    }

    public bool Changed {
        get { return snapshot != null && snapshot != Live(); }
    }

    public void Restore() {
        if (snapshot != null) {
            RandomizerRebinding.SetBinds(action, snapshot);
            Show();
        }
    }

    public string Action {
        get { return action; }
    }

    private MessageBox messageBox;

    private CustomSettingsScreen owner;

    private string action;

    private string label;

    // what this bind is for, shown whenever the row is not being edited
    private string help;

    // this control is taking keys; owner.Editing only says that *some* control is
    private bool editing;

    private int exit;

    // nothing is read until everything has been let go of once
    private bool armed;

    private Mode mode;

    private List<string> chords = new List<string>();

    // everything held since the chord started, which is what lands when it is let go
    private readonly List<string> peak = new List<string>();

    private string snapshot;

    private BindHold back;

    private BindHold swap;

    // Time.fixedTime at a press that grew nothing yet, or -1
    private float denyAt = -1f;

    private RandomizerMessageProvider tooltipProvider;

    private static SoundProvider denial;
}

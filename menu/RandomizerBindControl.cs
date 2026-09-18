using System;
using System.Collections.Generic;
using UnityEngine;

// A rando bind: alternatives separated by commas, each one a chord joined by +. A chord is
// everything held at once and lands when the last of it comes up, so Alt+R is one press rather
// than two binds. The edit reads keys, or the game actions they are bound to, and Tab held
// swaps between the two.
public class RandomizerBindControl : MonoBehaviour {
    public enum Mode {
        Keys,
        Actions
    }

    private void Awake() {
        messageBox = transform.Find("text/stateText").GetComponent<MessageBox>();
    }

    public void Init(string action, CustomSettingsScreen owner, string label) {
        this.owner = owner;
        this.action = action;
        this.label = label;
        Show();
        tooltipProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        tooltipProvider.SetMessage(owner.DefaultTooltip);
        GetComponent<CleverMenuItemTooltip>().Tooltip = tooltipProvider;
        owner.tooltipController.UpdateTooltip();
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

        if (Cancelling()) {
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

        if (Swapping()) {
            return;
        }

        Collect();
    }

    // Alone, because a chord is collected as it is held: nothing gathered means nothing down.
    private bool Bare(KeyCode key) {
        return Input.GetKeyDown(key) && peak.Count == 0;
    }

    private static bool Quiet() {
        return !Input.anyKey;
    }

    // Back held long enough abandons the edit. A tap of it is still a key, so it binds on the
    // release -- at the press there is no telling the two apart yet.
    private bool Cancelling() {
        var down = CustomSettingsScreen.BackHeld();
        if (down == KeyCode.None) {
            if (tapped != KeyCode.None && Time.unscaledTime - held < RandomizerHoldRing.Tap) {
                Press(tapped);
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

    // Tab held swaps what the edit reads; tapped, it is a key like any other, and held with a
    // chord already in hand it is part of that chord.
    private bool Swapping() {
        if (!Input.GetKey(KeyCode.Tab)) {
            if (swap >= 0f) {
                if (Time.unscaledTime - swap < RandomizerHoldRing.Tap) {
                    Press(KeyCode.Tab);
                }

                owner.HideHold();
            }

            swap = -1f;
            return false;
        }

        if (swap < 0f) {
            if (peak.Count > 0) {
                return false;
            }

            swap = Time.unscaledTime;
        }

        var progress = (Time.unscaledTime - swap) / RandomizerHoldRing.Seconds;
        owner.DrawHold(progress, "navigate");

        if (progress < 1f) {
            return true;
        }

        swap = -1f;
        owner.HideHold();
        mode = mode == Mode.Keys ? Mode.Actions : Mode.Keys;
        owner.ReadingActions = mode == Mode.Actions;
        peak.Clear();
        // Tab is still down, and standing down again is what stops it swapping straight back
        armed = false;
        owner.BindLegend();
        Tooltip();
        UpdateMessageBox();
        return true;
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

            // a key on none of the actions is worth saying no to, once per chord
            if (Input.anyKeyDown && !grew && peak.Count == 0) {
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

    // Modifiers first, then the order they were pressed in. A chord of nothing but modifiers is
    // a press thought better of rather than a bind, and would fire on its own besides.
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

    // The last bind of something the rando cannot be played without stays where it is.
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
        held = -1f;
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

    // The chord in hand trails a + of its own: it is still open until the keys come up.
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
            : owner.DefaultTooltip);
        owner.tooltipController.UpdateTooltip();
    }

    // Ori's own no: the sound an ability makes when there is not enough energy behind it. It
    // lives on Sein, who is not always around, so a menu that cannot find it stays quiet.
    private void Deny() {
        // looked up again each time rather than cached away: a menu opened before the save was
        // loaded has no Sein to ask, and one opened after does
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

    // Binds apply as they are made, so leaving without keeping them is a restore, not a commit.
    // The screen writes the file once the whole page has been put back.
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

    private float held = -1f;

    private float swap = -1f;

    private KeyCode tapped = KeyCode.None;

    private RandomizerMessageProvider tooltipProvider;

    private static SoundProvider denial;
}

using CatlikeCoding.TextBox;
using Game;
using UnityEngine;

// Two boxes of our own on the UI camera, cloned from the game's hint box the way the
// pickup notifications are: the clock in the top right, and the tally over the finish
// screen. Neither goes through UI.Hints, which any menu opening hides.
public static class PracticeHud {
    private static GameObject clockObj;

    private static MessageBox clock;

    private static GameObject tallyObj;

    private static MessageBox tally;

    private static string shown;

    // the line under the clock: the time to beat, fixed for the attempt
    private static string reference = "";

    // UI camera units, the anchored edge of each box: the clock's top right corner in
    // the screen's, the tally's top centre under the finish screen's rows
    private static readonly Vector3 ClockAt = new Vector3(6.9f, 3.8f, 0f);

    private static readonly Vector3 TallyAt = new Vector3(0f, -1.35f, 0f);

    public static void Tick() {
        var wanted = PracticeController.Active && RandomizerSettings.Practice.Timer.Value
            && PracticeController.Current != PracticeController.Phase.Editing
            && GameController.Instance != null && !GameController.Instance.GameInTitleScreen;
        if (!wanted) {
            HideClock();
            return;
        }

        if (clock == null) {
            clockObj = Make("practiceClock", AlignmentMode.Right, HorizontalAnchorMode.Right, ClockAt, out clock);
            shown = null;
        }

        if (clock == null) {
            return;
        }

        var text = PracticeController.Clock(PracticeController.Elapsed);
        if (reference != "") {
            text += "   " + reference;
        }

        if (text != shown) {
            shown = text;
            clock.SetMessage(new MessageDescriptor(text));
        }
    }

    // when the attempt starts and when a run lands
    public static void Refresh() {
        reference = "";
        var file = PracticeController.File;
        if (file == null) {
            return;
        }

        var average = file.Segment["timing"]["mode"].Str == "average";
        var ms = average ? file.AverageMs() : file.BestMs();
        reference = ms < 0 ? "first run" : (average ? "avg " : "best ") + PracticeController.Clock(ms);
    }

    public static void ShowTally(string text) {
        HideTally();
        tallyObj = Make("practiceTally", AlignmentMode.Center, HorizontalAnchorMode.Center, TallyAt, out tally);
        if (tally != null) {
            tally.SetMessage(new MessageDescriptor(text));
        }
    }

    public static void HideTally() {
        Drop(ref tallyObj);
        tally = null;
    }

    public static void HideClock() {
        Drop(ref clockObj);
        clock = null;
        shown = null;
    }

    public static void Hide() {
        HideClock();
        HideTally();
    }

    private static void Drop(ref GameObject obj) {
        if (obj != null) {
            Object.Destroy(obj);
        }

        obj = null;
    }

    private static GameObject Make(string name, AlignmentMode align, HorizontalAnchorMode anchor, Vector3 at, out MessageBox box) {
        box = null;
        var prefab = UI.MessageController == null ? null : UI.MessageController.HintMessage;
        if (prefab == null) {
            return null;
        }

        // the prefab sleeps through the clone so nothing runs before the clone is set up
        var wasActive = prefab.activeSelf;
        prefab.SetActive(false);
        var obj = (GameObject)InstantiateUtility.Instantiate(prefab);
        prefab.SetActive(wasActive);
        obj.name = name;
        if (RandomizerUI.Instance != null) {
            obj.transform.parent = RandomizerUI.Instance.transform;
        }

        // a retry restores a checkpoint, which would take the box with it
        Object.Destroy(obj.GetComponent<DestroyOnRestoreCheckpoint>());
        Object.Destroy(obj.GetComponent<SoundSource>());
        box = obj.GetComponentInChildren<MessageBox>();
        if (box == null) {
            Object.Destroy(obj);
            return null;
        }

        // no typewriter: the text is whole the moment it is set
        if (box.WriteOutTextBox != null) {
            Object.DestroyImmediate(box.WriteOutTextBox);
            box.WriteOutTextBox = null;
        }

        // in front of the menu screens
        box.TextBox.transform.localPosition = new Vector3(0f, 0f, -1f);
        box.TextBox.alignment = align;
        box.TextBox.horizontalAnchor = anchor;
        box.TextBox.verticalAnchor = VerticalAnchorMode.Top;
        box.SetWaitDuration(float.PositiveInfinity);
        // shown whole at once: a menu's suspension would hold a fade at nothing
        box.Visibility.TransitionInDuration = 0.001f;
        box.transform.position = at;
        obj.SetActive(true);
        box.Visibility.Start();
        return obj;
    }
}

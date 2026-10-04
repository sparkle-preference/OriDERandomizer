using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game;
using UnityEngine;

// The in-game box editor: boxes drawn with the mouse over the frozen world, kept as BX lines in a practice
// container's placements.bfr or, outside practice, the loaded seed. Create snapshots a save into a new container.
public static class PracticeEditor {
    public static bool Active;

    // editing the loaded seed's boxes rather than a practice segment's
    public static bool Seed;

    // the rectangle being dragged, in world units
    public static RandomizerBox? Draft;

    private static Vector2 dragFrom;

    private static bool doMotionBlur;

    private static bool dragging;

    private static string tool = "item";

    // with a variant running its own list takes the boxes, unless V says the shared one
    private static bool toVariant = true;

    // M: the mouse moves and resizes boxes instead of drawing them
    private static bool adjusting;

    // an adjust drag's box: its list, index and rect when grabbed, and the edges that move
    private static string grabTarget;

    private static int grabIndex = -1;

    private static RandomizerBox grabBox;

    private static Rect grabFrom;

    private static int grabEdges;

    // what the cursor was last over while moving, read again only once it moves or the boxes change
    private static Vector2 hoverAt;

    // the lists hover reads, and what they were read from: a reparse makes a new segment, a seed edit a new revision
    private static Dictionary<string, List<RandomizerBox>> hoverLists;

    private static object hoverSource;

    private static RandomizerBox hoverBox;

    private static int hoverEdges;

    // what the highlight shows, which outlives the hover while it fades out, and how strongly
    private static RandomizerBox lit;

    private static int litEdges;

    private static float glow;

    // when the fade in finished and the pulse began; negative while fading
    private static float fullAt = -1f;

    // the fade in and out, and the pulse between full and half after it
    private const float FadeSeconds = 0.5f;

    private const float PulseSeconds = 3f;

    // the hover's tint, and the resize preview's fill when its box is invisible
    private static readonly Color Tint = new Color(1f, 1f, 1f, 0.35f);

    private static readonly Color DraftFill = new Color32(255, 255, 153, 64);

    private const int Left = 1, Right = 2, Bottom = 4, Top = 8;

    // what Z takes back: a box's list and index, and the box it replaced (null: it was added)
    private static string undoTarget;

    private static int undoIndex = -1;

    private static RandomizerBox undoOld;

    // whether right click has moved Ori since the seed editor opened
    private static bool seedMoved;

    // the bind pressed with unsaved edits asks first; pressed again it drops them
    private static bool leaveArmed;

    private const float PanSpeed = 24f;

    // the thinnest side a drawn box or a resize leaves
    private const float MinSide = 0.5f;

    // the attempt freezes where it is and boxes are drawn over it; Enter saves and retries
    public static void Begin() {
        if (!PracticeController.Active || PracticeController.File == null) {
            return;
        }

        var given = !PracticeController.File.IsFolder;
        if (!PracticeController.MakeEditable()) {
            return;
        }

        PracticeController.Current = PracticeController.Phase.Editing;
        PracticeController.Freeze();
        Seed = false;
        Open();
        // the server first, so the help can say where the page is
        PracticeServer.OpenOnce();
        Help();
        if (given) {
            Say("Extracted to the folder " + PracticeFolder.NameOf(PracticeController.File.Path) + " for editing", 300);
        }
    }

    // The loaded seed's boxes, from the Edit Seed Boxes bind or the debug menu; the bind again leaves.
    public static void ToggleSeed() {
        if (Active && Seed) {
            if (SeedBoxes.Current != null && SeedBoxes.Current.Dirty && !leaveArmed) {
                leaveArmed = true;
                Say("Unsaved box edits: Enter saves them, " + RandomizerRebinding.EditSeedBoxes + " again throws them away", 360);
                return;
            }

            LeaveSeed();
            return;
        }

        BeginSeed();
    }

    public static void BeginSeed() {
        if (Active) {
            return;
        }

        if (PracticeController.Active) {
            Randomizer.printInfo("Seed boxes can't be edited during a practice session", 300);
            return;
        }

        if (Characters.Sein == null || GameController.Instance == null || GameController.Instance.GameInTitleScreen
                || GameController.Instance.IsLoadingGame) {
            return;
        }

        if (SeedBoxes.Synced) {
            Randomizer.printInfo("This seed is synced with the server, so its boxes can't be edited here.\n"
                + "Remove the Sync flag from the seed's first line if you want to do this.", 480);
            return;
        }

        try {
            SeedBoxes.Current = SeedBoxes.Load(Randomizer.SeedFilePath);
        } catch (Exception e) {
            Randomizer.LogError("seed boxes: could not read " + Randomizer.SeedFilePath + ": " + e.Message);
            Randomizer.printInfo("Could not read the seed file; see randomizer.log", 300);
            return;
        }

        Seed = true;
        seedMoved = false;
        tool = "item";
        PracticeController.Freeze();
        Open();
        SeedBoxes.Current.Apply();
        Help();
    }

    private static void Open() {
        Active = true;
        Draft = null;
        dragging = false;
        adjusting = false;
        grabIndex = -1;
        undoIndex = -1;
        leaveArmed = false;
        hoverLists = null;

        if (UberPostProcess.Instance != null) {
            doMotionBlur = UberPostProcess.Instance.DoMotionBlur;
            UberPostProcess.Instance.DoMotionBlur = false;
        }
    }

    // what is drawn over the world goes with the editor, whichever way it is left
    public static void Stop() {
        DropGrab();
        Draft?.DeInit();
        Draft = null;
        Highlight.Hide();
        lit = null;
        Active = false;
        dragging = false;

        if (UberPostProcess.Instance != null) {
            UberPostProcess.Instance.DoMotionBlur = doMotionBlur;
        }
    }

    // the retry bind in the seed editor: back to the game, unsaved edits dropped
    public static void LeaveUnsaved() {
        if (!Active || !Seed) {
            return;
        }

        var dropped = SeedBoxes.Current != null && SeedBoxes.Current.Dirty;
        LeaveSeed();
        if (dropped) {
            Randomizer.printInfo("Unsaved box edits thrown away", 180);
        }
    }

    // back to the game, from wherever right click left Ori, the file's own boxes in force
    private static void LeaveSeed() {
        DropSeedEdits();

        Stop();
        Seed = false;
        PracticeController.Resume();
        Randomizer.clearMessage();
        // settles the camera and the scenes on Ori's new spot
        if (seedMoved && Characters.Sein != null) {
            Randomizer.WarpTo(Characters.Sein.Position, 0);
        }
    }

    // a seed file gone or locked must not keep the game frozen in the editor
    private static void DropSeedEdits() {
        if (SeedBoxes.Current == null || !SeedBoxes.Current.Dirty) {
            return;
        }

        try {
            SeedBoxes.Current.Reload();
            SeedBoxes.Current.Apply();
        } catch (Exception e) {
            Randomizer.LogError("seed boxes: could not reload: " + e.Message);
        }
    }

    // quitting to the title mid-edit: nothing is saved and the freeze must not ride along
    public static void OnReturnToTitle() {
        if (!Seed) {
            return;
        }

        DropSeedEdits();

        Stop();
        Seed = false;
        PracticeController.Resume();
    }

    // save what was drawn and run it; with nothing to end the run yet, stay
    public static void SaveAndRetry() {
        if (!Active) {
            return;
        }

        if (!Write()) {
            Say("The segment could not be saved; see randomizer.log", 300);
            return;
        }

        if (Seed) {
            LeaveSeed();
            return;
        }

        if (PracticeController.Segment != null && !PracticeController.Segment.HasEnd) {
            Say("Saved. Nothing ends this segment yet: draw a goal box with 4, or set the end condition on the page", 600);
            return;
        }

        Stop();
        PracticeController.Retry();
    }

    // save what was drawn and keep editing
    public static void Save() {
        if (Active && Write()) {
            leaveArmed = false;
            Say(Seed ? "Boxes saved to " + Path.GetFileName(SeedBoxes.Current.Path) : "Segment saved", 180);
        }
    }

    private static bool Write() {
        try {
            if (Seed) {
                // with nothing unsaved the file is left alone, so reload and Enter leave it as found
                if (SeedBoxes.Current.Dirty) {
                    SeedBoxes.Current.Save();
                }

                SeedBoxes.Current.Apply();
            } else {
                PracticeController.File.Save();
            }

            return true;
        } catch (Exception e) {
            Randomizer.LogError("practice: could not save the " + (Seed ? "seed's boxes" : "segment") + ": " + e.Message);
            return false;
        }
    }

    // what is on disk replaces everything drawn since the last save
    public static bool Reload() {
        if (!Active) {
            return false;
        }

        try {
            if (Seed) {
                SeedBoxes.Current.Reload();
                SeedBoxes.Current.Apply();
            } else {
                var fresh = BfrpFile.Load(PracticeController.File.Path);
                fresh.Variant = PracticeController.File.Variant;
                fresh.Edited = PracticeController.File.Edited;
                PracticeController.File = fresh;
                PracticeController.Reparse();
            }

            undoIndex = -1;
            leaveArmed = false;
            Say(Seed ? "Boxes reloaded from the seed file" : "Segment reloaded from disk", 180);
            return true;
        } catch (Exception e) {
            Randomizer.LogError("practice: could not reload the " + (Seed ? "seed's boxes" : "segment") + ": " + e.Message);
            return false;
        }
    }

    public static void Tick() {
        Draft?.DeInit();
        Draft = null;
        Highlight.Hide();

        if (!Active || Characters.Sein == null || GameController.Instance == null || GameController.Instance.GameInTitleScreen) {
            return;
        }

        // Ctrl and a letter is a command, not a pan
        var ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
        if (ctrl) {
            if (UnityEngine.Input.GetKeyDown(KeyCode.S)) {
                Save();
            } else if (UnityEngine.Input.GetKeyDown(KeyCode.R)) {
                Reload();
            } else if (UnityEngine.Input.GetKeyDown(KeyCode.C)) {
                CopyLines();
            }
        } else {
            Pan();
        }

        // number keys: the letters are taken by the pan
        if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1)) {
            Tool("item");
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2)) {
            Tool("solid");
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3)) {
            Tool("kill");
        } else if ((UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4)) && !Seed) {
            Tool("goal");
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad5)) {
            PracticeServer.Open();
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.M)) {
            adjusting = !adjusting;
            DropGrab();
            dragging = false;
            Randomizer.clearMessage();
            Help();
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.V) && !Seed) {
            toVariant = !toVariant;
            Randomizer.clearMessage();
            Help();
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Z)) {
            Undo();
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.X)) {
            DeleteUnderCursor();
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)) {
            SaveAndRetry();
            return;
        }

        // the legend is this mode's HUD, not a one-off: it goes back up as it runs out
        if (Time.unscaledTime - helped > HelpAgain && Randomizer.MessageQueue.Count == 0
                && Randomizer.MessageQueueTime <= 0f) {
            Help();
        }

        var at = World(Core.Input.CursorPosition);
        // right click stands Ori where you are looking, which is how the scenes there load
        if (Core.Input.RightClick.OnPressed && !Game.UI.MainMenuVisible) {
            Characters.Sein.Position = at;
            seedMoved = Seed;
        }

        if (Core.Input.LeftClick.OnPressed && !Game.UI.MainMenuVisible) {
            if (adjusting) {
                Grab(at);
            } else {
                dragFrom = at;
                dragging = true;
            }
        }

        if (grabIndex >= 0) {
            Adjust(at);
            return;
        }

        if (adjusting && !Game.UI.MainMenuVisible) {
            Hover(at);
        } else {
            lit = null;
        }

        if (!dragging) {
            return;
        }

        // a press that barely moved is a click, not a box
        var moved = Mathf.Max(Mathf.Abs(at.x - dragFrom.x), Mathf.Abs(at.y - dragFrom.y)) >= Least();
        var drawn = Drawn(dragFrom, at);
        if (moved) {
            Draft = CreateBox(drawn, "renderDepth=-99", "ffff99", "");
            Draft!.Init(-1);
        }

        if (Core.Input.LeftClick.OnReleased) {
            dragging = false;
            Draft?.DeInit();
            Draft = null;
            if (moved) {
                Commit(drawn);
            }
        }
    }

    private static void Tool(string name) {
        tool = name;
        adjusting = false;
        // the legend is what says which box you are drawing, so it changes as you change it
        Randomizer.clearMessage();
        Help();
    }

    private static bool HasVariant {
        get { return !Seed && PracticeController.File != null && !string.IsNullOrEmpty(PracticeController.File.Variant); }
    }

    private static bool TargetVariant {
        get { return toVariant && HasVariant; }
    }

    // the list a new box joins
    private static string Target {
        get { return TargetVariant ? PracticeController.File.Variant : ""; }
    }

    // the lists the cursor finds boxes in, the variant's first
    private static string[] Targets {
        get { return HasVariant ? new[] { PracticeController.File.Variant, "" } : new[] { "" }; }
    }

    // a segment's shared list or a variant's, or the seed's own
    private static List<RandomizerBox> BoxesOf(string target) {
        return Seed ? new List<RandomizerBox>(SeedBoxes.Current.Boxes) : PracticeController.File.Boxes(target);
    }

    private static void Put(string target, List<RandomizerBox> boxes) {
        leaveArmed = false;
        if (Seed) {
            SeedBoxes.Current.SetBoxes(boxes);
            SeedBoxes.Current.Apply();
        } else {
            PracticeController.File.SetBoxes(target, boxes);
            PracticeController.Reparse();
        }
    }

    // the legend's own duration, less a moment, so it never blinks out between showings
    private const float HelpAgain = 25f;

    private static float helped = -100f;

    // goes up ahead of the legend, which then waits its turn
    private static void Say(string text, int frames) {
        Randomizer.clearMessage();
        Randomizer.printInfo(text, frames);
        helped = Time.unscaledTime - HelpAgain + frames / 60f;
    }

    private static void Help() {
        helped = Time.unscaledTime;
        var doing = adjusting ? "MOVING BOXES   M: draw instead"
            : "drag to draw " + (tool == "item" ? "an " : "a ") + "*" + tool + "* box   M: move and resize";
        // moving has no tools, so their row says how to move instead
        var keys = (adjusting ? "click&drag to move/resize boxes"
            : Seed ? "1: item   2: solid   3: kill" : "1: item   2: solid   3: kill   4: goal")
            + "   Z: undo   X: delete under cursor   Ctrl+C: copy box lines\n";
        if (Seed) {
            Randomizer.printQuiet("EDITING SEED BOXES - " + doing + "\n" + keys
                + "WASD: pan   right click: stand Ori here   Ctrl+S: save   Ctrl+R: reload from the file   Enter: save and play\n"
                + "[[Retry Practice Segment]]: leave without saving   5: fill in the boxes in a browser"
                + (PracticeServer.Running ? "   " + PracticeServer.Url : ""), 1800);
            return;
        }

        var where = !HasVariant ? "" : "\n("
            + (TargetVariant ? "adding boxes to variant " + VariantLabel() : "adding boxes to the shared list") + "   V: switch)";
        Randomizer.printQuiet("EDITING - " + doing + "\n" + keys
            + "WASD: pan   right click: stand Ori here   Ctrl+S: save   Ctrl+R: reload from disk   Enter: save and retry\n"
            + "5: open the segment editor in a browser" + (PracticeServer.Running ? "   " + PracticeServer.Url : "") + where, 1800);
    }

    // the running variant's name, or its id, cut to fit the legend
    private static string VariantLabel() {
        var id = PracticeController.File.Variant;
        var name = PracticeController.File.VariantSegment(id)["name"];
        var label = name.IsString && name.Str.Trim().Length > 0 ? name.Str.Trim() : id;
        return label.Length > 16 ? label.Substring(0, 16) : label;
    }

    // WASD walks the camera's own root, which the frozen chase leaves alone
    private static void Pan() {
        var cameras = Game.UI.Cameras.Current;
        if (cameras == null || cameras.Transform == null) {
            return;
        }

        var move = Vector3.zero;
        if (UnityEngine.Input.GetKey(KeyCode.W)) { move.y += 1f; }
        if (UnityEngine.Input.GetKey(KeyCode.S)) { move.y -= 1f; }
        if (UnityEngine.Input.GetKey(KeyCode.A)) { move.x -= 1f; }
        if (UnityEngine.Input.GetKey(KeyCode.D)) { move.x += 1f; }
        if (move == Vector3.zero) {
            return;
        }

        var from = cameras.Transform.position;
        var step = move.normalized * PanSpeed * Time.unscaledDeltaTime;
        var to = from + step;
        // past the loaded scenes there is nothing to draw on, so the pan slides along their edge
        if (!Loaded(to)) {
            to = new Vector3(from.x + step.x, from.y, from.z);
            if (!Loaded(to)) {
                to = new Vector3(from.x, from.y + step.y, from.z);
                if (!Loaded(to)) {
                    return;
                }
            }
        }

        cameras.Transform.position = to;
        if (cameras.Controller != null) {
            cameras.Controller.UpdateCamera();
        }
    }

    private static bool Loaded(Vector3 at) {
        var manager = Core.Scenes.Manager;
        if (manager == null) {
            return true;
        }

        var point = new Vector2(at.x, at.y);
        foreach (var scene in manager.ActiveScenes) {
            if (scene != null && scene.MetaData != null && scene.IsLoadingComplete && scene.MetaData.SceneBounds.Contains(point)) {
                return true;
            }
        }

        return false;
    }

    private static Vector2 World(Vector2 cursor) {
        var camera = Game.UI.Cameras.Current == null ? null : Game.UI.Cameras.Current.Camera;
        if (camera == null) {
            return Vector2.zero;
        }

        var point = camera.ViewportToWorldPoint(new Vector3(cursor.x, cursor.y, -camera.transform.position.z));
        return new Vector2(point.x, point.y);
    }

    // the dragged rect, a side too thin grown to MinSide the way the drag went
    private static Rect Drawn(Vector2 from, Vector2 to) {
        return Between(from, new Vector2(Stretch(from.x, to.x), Stretch(from.y, to.y)));
    }

    private static float Stretch(float from, float to) {
        return Mathf.Abs(to - from) >= MinSide ? to : from + (to < from ? -MinSide : MinSide);
    }

    private static Rect Between(Vector2 a, Vector2 b) {
        var min = Vector2.Min(a, b);
        var max = Vector2.Max(a, b);
        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    // The goal is shared and single; other boxes join the target list.
    private static void Commit(Rect area) {
        var target = tool != "goal" ? Target : "";
        // each tool is named for its box's flag
        var box = CreateBox(area, tool, "", "");

        var boxes = BoxesOf(target);
        if (box.Goal) {
            for (var i = 0; i < boxes.Count; i++) {
                if (boxes[i].Goal && !boxes[i].Deleted) {
                    boxes[i] = Buried();
                }
            }
        }

        boxes.Add(box);
        Put(target, boxes);
        Remember(target, boxes.Count - 1, null);
    }

    // empty trailing fields are left off, as the page writes them
    private static RandomizerBox CreateBox(Rect area, string flags, string color, string item) {
        var line = $"BX|{flags}|{Corners(area)}";
        if (color != "" || item != "") {
            line += "|" + color;
        }

        if (item != "") {
            line += "|" + item;
        }

        return RandomizerBox.Parse(line);
    }

    private static string Corners(Rect area) {
        return string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##},{2:0.##},{3:0.##}", area.min.x, area.min.y, area.max.x, area.max.y);
    }

    // the same line at new corners
    private static RandomizerBox Moved(RandomizerBox box, Rect area) {
        var fields = box.Line.Split('|');
        fields[2] = Corners(area);
        return RandomizerBox.Parse(string.Join("|", fields));
    }

    // a grip's reach either side of an edge: about a dozen pixels, down to four on a thin box
    private static float Reach() {
        return Mathf.Max(0.05f, Pixels(12f));
    }

    private static float Least() {
        return Mathf.Min(Reach(), Pixels(4f));
    }

    // n screen pixels in world units at the current zoom
    private static float Pixels(float n) {
        var a = World(new Vector2(0f, 0f));
        var b = World(new Vector2(n / Screen.width, 0f));
        return Mathf.Abs(b.x - a.x);
    }

    // the topmost box under the cursor, by an edge or corner when the cursor is near one (edges), else whole
    private static bool Hit(Vector2 at, Func<string, List<RandomizerBox>> read, out string target, out int index, out RandomizerBox box, out int edges) {
        float reach = Reach(), least = Least();
        foreach (var each in Targets) {
            var boxes = read(each);
            for (var i = boxes.Count - 1; i >= 0; i--) {
                var r = boxes[i].Rect;
                var x = Grip(at.x, r.xMin, r.xMax, reach, least, Left, Right);
                var y = Grip(at.y, r.yMin, r.yMax, reach, least, Bottom, Top);
                if (boxes[i].Deleted || x < 0 || y < 0) {
                    continue;
                }

                target = each;
                index = i;
                box = boxes[i];
                edges = x | y;
                return true;
            }
        }

        target = "";
        index = -1;
        box = null;
        edges = 0;
        return false;
    }

    // One axis of a grab, or -1: grips straddle each edge, shrinking with the box to `least`;
    // a box too thin for three of those gets three centered on it.
    private static int Grip(float at, float min, float max, float reach, float least, int low, int high) {
        var size = max - min;
        var grip = Mathf.Clamp(size / 3f, least, reach);
        if (size >= 3f * grip) {
            if (at < min - grip || at > max + grip) {
                return -1;
            }

            return Mathf.Abs(at - min) < grip ? low : Mathf.Abs(at - max) < grip ? high : 0;
        }

        var from = (min + max) * 0.5f - 1.5f * least;
        if (at < from || at > from + 3f * least) {
            return -1;
        }

        return at < from + least ? low : at > from + 2f * least ? high : 0;
    }

    private static void Grab(Vector2 at) {
        if (Hit(at, BoxesOf, out grabTarget, out grabIndex, out grabBox, out grabEdges)) {
            grabFrom = grabBox.Rect;
            dragFrom = at;
        }
    }

    // what a click would take: the whole box tinted, or the edges it would drag lit
    private static void Hover(Vector2 at) {
        var source = Seed ? (object)SeedBoxes.Current.Revision : PracticeController.Segment;
        var fresh = hoverLists == null || !Equals(source, hoverSource);
        if (fresh) {
            hoverSource = source;
            hoverLists = new Dictionary<string, List<RandomizerBox>>();
            foreach (var target in Targets) {
                hoverLists[target] = BoxesOf(target);
            }
        }

        // fresh lists re-hit where the cursor is; no NaN sentinel for that, as Unity's != is false against NaN
        if (fresh || at != hoverAt) {
            hoverAt = at;
            Hit(at, target => hoverLists[target], out _, out _, out hoverBox, out hoverEdges);
        }

        Glow(hoverBox, hoverEdges);
    }

    // Fades the highlight in on what the cursor is on, pulses it there, and fades it out once the cursor leaves.
    private static void Glow(RandomizerBox box, int edges) {
        var step = Time.unscaledDeltaTime / FadeSeconds;
        if (box != null && (lit == null || box.Line != lit.Line || edges != litEdges)) {
            lit = box;
            litEdges = edges;
            glow = 0f;
            fullAt = -1f;
        }

        if (box == null) {
            fullAt = -1f;
            glow = Mathf.Max(0f, glow - step);
        } else if (fullAt < 0f) {
            glow = Mathf.Min(1f, glow + step);
            if (glow >= 1f) {
                fullAt = Time.unscaledTime;
            }
        } else {
            glow = 0.75f + 0.25f * Mathf.Cos((Time.unscaledTime - fullAt) * 2f * Mathf.PI / PulseSeconds);
        }

        if (lit == null || glow <= 0f) {
            lit = null;
            return;
        }

        // a box too thin to show its middle lights its whole outline instead
        var sides = litEdges != 0 ? litEdges : Slight(lit.Rect) ? Left | Right | Bottom | Top : 0;
        Highlight.Show(lit.Rect, sides == 0 ? Tint : Color.clear, sides, lit.ParallaxDepth, glow);
    }

    // the middle inside a box's drawn edges is narrower than an edge
    private static bool Slight(Rect area) {
        return Mathf.Min(area.width, area.height) < 3f * RandomizerBoxPrefab.EdgeWidth;
    }

    // an adjust drag dropped before its release: the box it hid comes back
    private static void DropGrab() {
        if (grabIndex >= 0) {
            Conceal(null);
        }

        grabIndex = -1;
    }

    // the live boxes drawn from this line hide while a resize draws it; null brings every box back
    private static void Conceal(string line) {
        foreach (var box in RandomizerBoxes.ActiveBoxes) {
            if (box.UnityObject == null) {
                continue;
            }

            if (line != null && box.Line == line) {
                box.UnityObject.gameObject.SetActive(false);
            } else {
                box.UnityObject.UpdateActive();
            }
        }
    }

    private static void Adjust(Vector2 at) {
        var d = at - dragFrom;
        float x1 = grabFrom.xMin, y1 = grabFrom.yMin, x2 = grabFrom.xMax, y2 = grabFrom.yMax;
        if (grabEdges == 0) {
            x1 += d.x;
            x2 += d.x;
            y1 += d.y;
            y2 += d.y;
        } else {
            // a dragged side stops where the box would get thinner than MinSide, or than it already was
            var w = Mathf.Min(MinSide, grabFrom.width);
            var h = Mathf.Min(MinSide, grabFrom.height);
            if ((grabEdges & Left) != 0) { x1 = Mathf.Min(x1 + d.x, x2 - w); }
            if ((grabEdges & Right) != 0) { x2 = Mathf.Max(x2 + d.x, x1 + w); }
            if ((grabEdges & Bottom) != 0) { y1 = Mathf.Min(y1 + d.y, y2 - h); }
            if ((grabEdges & Top) != 0) { y2 = Mathf.Max(y2 + d.y, y1 + h); }
        }

        var area = Between(new Vector2(x1, y1), new Vector2(x2, y2));
        if (grabEdges == 0) {
            Draft = CreateBox(area, "renderDepth=-99", "ffff99", "");
            Draft!.Init(-1);
        } else {
            // a resize shows the box itself with only the edges it drags, in white
            Conceal(grabBox.Line);
            Highlight.Show(area, grabBox.Invisible ? DraftFill : (Color)grabBox.Color, grabEdges, grabBox.ParallaxDepth);
        }

        if (!Core.Input.LeftClick.OnReleased) {
            return;
        }

        Draft?.DeInit();
        Draft = null;
        Highlight.Hide();
        if (grabEdges != 0) {
            Conceal(null);
        }

        var target = grabTarget;
        var index = grabIndex;
        grabIndex = -1;
        var boxes = BoxesOf(target);
        if (index >= boxes.Count || area == grabFrom) {
            return;
        }

        var old = boxes[index];
        boxes[index] = Moved(old, area);
        Put(target, boxes);
        Remember(target, index, old);
    }

    // Drawn over the boxes on a mesh of its own: a fill, and white strips along the chosen edges.
    private static class Highlight {
        private static GameObject shown;

        private static Mesh mesh;

        private static Rect rect;

        private static Color fill;

        private static int edges = -1;

        private static float depth;

        // each vertex's full color, and the strength last applied to them
        private static readonly List<Color> paints = new List<Color>();

        private static float applied = -1f;

        // over the new-box draft's depth
        private const float Depth = -99f;

        public static void Show(Rect area, Color tint, int sides, float z, float strength = 1f) {
            if (shown == null) {
                // the scene took the object; the mesh is an asset and outlives it
                if (mesh != null) {
                    UnityEngine.Object.Destroy(mesh);
                }

                shown = new GameObject("practiceEditorHighlight") { layer = RandomizerLayers.Solids };
                mesh = new Mesh();
                shown.AddComponent<MeshFilter>().sharedMesh = mesh;
                shown.AddComponent<MeshRenderer>().sharedMaterial = RandomizerBoxPrefab.GetMaterial(Depth);
                UberShaderRenderQueue.SetRenderQueueExplicit(shown, Depth);
                edges = -1;
            }

            shown.SetActive(true);
            if (!(area == rect && tint == fill && sides == edges && z == depth)) {
                Build(area, tint, sides, z);
            }

            if (strength != applied) {
                applied = strength;
                var colors = new Color[paints.Count];
                for (var i = 0; i < colors.Length; i++) {
                    colors[i] = new Color(paints[i].r, paints[i].g, paints[i].b, paints[i].a * strength);
                }

                mesh.colors = colors;
            }
        }

        private static void Build(Rect area, Color tint, int sides, float z) {
            rect = area;
            fill = tint;
            edges = sides;
            depth = z;
            shown.transform.position = area.center;
            var verts = new List<Vector3>();
            paints.Clear();
            var tris = new List<int>();
            float x = area.width * 0.5f, y = area.height * 0.5f;
            // strips are the box's own edge width, inside it, or just outside a box too thin to hold two
            var w = RandomizerBoxPrefab.EdgeWidth;
            float left = x > w ? -x : -x - w, right = x > w ? x - w : x, bottom = y > w ? -y : -y - w, top = y > w ? y - w : y;
            if (tint.a > 0f) {
                Quad(verts, paints, tris, -x, -y, x, y, z, tint);
            }

            if ((sides & Left) != 0) {
                Quad(verts, paints, tris, left, -y, left + w, y, z, Color.white);
            }

            if ((sides & Right) != 0) {
                Quad(verts, paints, tris, right, -y, right + w, y, z, Color.white);
            }

            if ((sides & Bottom) != 0) {
                Quad(verts, paints, tris, -x, bottom, x, bottom + w, z, Color.white);
            }

            if ((sides & Top) != 0) {
                Quad(verts, paints, tris, -x, top, x, top + w, z, Color.white);
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            applied = -1f;
        }

        public static void Hide() {
            if (shown != null) {
                shown.SetActive(false);
            }
        }

        private static void Quad(List<Vector3> verts, List<Color> colors, List<int> tris, float x0, float y0, float x1, float y1, float z, Color color) {
            var at = verts.Count;
            verts.Add(new Vector3(x0, y0, z));
            verts.Add(new Vector3(x0, y1, z));
            verts.Add(new Vector3(x1, y1, z));
            verts.Add(new Vector3(x1, y0, z));
            for (var i = 0; i < 4; i++) {
                colors.Add(color);
            }

            tris.AddRange(new[] { at, at + 2, at + 1, at, at + 3, at + 2 });
        }
    }

    private static void Remember(string target, int index, RandomizerBox old) {
        undoTarget = target;
        undoIndex = index;
        undoOld = old;
    }

    // the page rewrote the boxes, so the index an undo kept may now be another box
    public static void ForgetUndo() {
        undoIndex = -1;
    }

    // an added box goes; a moved or deleted one comes back as it was
    private static void Undo() {
        if (undoIndex < 0) {
            return;
        }

        var boxes = BoxesOf(undoTarget);
        if (undoOld == null) {
            if (undoIndex != boxes.Count - 1) {
                undoIndex = -1;
                return;
            }

            boxes.RemoveAt(undoIndex);
        } else if (undoIndex < boxes.Count) {
            boxes[undoIndex] = undoOld;
        }

        Put(undoTarget, boxes);
        undoIndex = -1;
    }

    // the variant's boxes first, then the shared ones, the goal among them
    // the box a click would take, thin ones included
    private static void DeleteUnderCursor() {
        string target;
        int index;
        RandomizerBox old;
        if (!Hit(World(Core.Input.CursorPosition), BoxesOf, out target, out index, out old, out _)) {
            return;
        }

        var boxes = BoxesOf(target);
        boxes[index] = Buried();
        Put(target, boxes);
        Remember(target, index, old);
    }

    // a deleted box keeps a line, so the boxes after it keep their numbers
    private static RandomizerBox Buried() {
        return RandomizerBox.Parse(RandomizerBox.Tombstone);
    }

    // for pasting into the plando builder: the list being drawn into, tombstones and all
    private static void CopyLines() {
        var boxes = BoxesOf(Target);
        GUIUtility.systemCopyBuffer = string.Join("\n", boxes.Select(box => box.Line).ToArray());
        Say("Copied " + boxes.Count + " box line" + (boxes.Count == 1 ? "" : "s"), 180);
    }

    // from a normal game: the current state becomes a new, empty segment's save; the seed carries on
    public static void Create() {
        Create(false);
    }

    // withSeed: the seed's pickups and boxes become the new segment's own placements
    public static void Create(bool withSeed) {
        if (Characters.Sein == null) {
            return;
        }

        if (PracticeController.Active) {
            Randomizer.printInfo("Practice: end the current session before creating a segment", 300);
            return;
        }

        try {
            var path = CreateFrom(CheckpointHere(), null);
            if (withSeed) {
                var file = BfrpFile.Load(path);
                file.SetPlacementLines("", SeedPlacements());
                file.Save();
            }

            Randomizer.printInfo("Practice segment saved: " + path + (withSeed ? ", with the seed's placements" : "")
                + "\nStart it from PRACTICE MODE: it opens in the editor", 600);
        } catch (Exception e) {
            Randomizer.LogError("practice: could not create a segment: " + e.Message);
        }
    }

    // the seed file's lines an attempt can use, as the page's paste takes them: no flags, doors or other players' items
    private static List<string> SeedPlacements() {
        var skipped = new List<string>();
        var kept = PracticeSegment.ReadPlacements(File.ReadAllText(Randomizer.SeedFilePath), false, skipped);
        if (skipped.Count > 0) {
            Randomizer.log("practice: the new segment leaves out " + skipped.Count + " seed line(s), first " + skipped[0]);
        }

        return kept;
    }

    // The save a checkpoint here would write, box contact cleared; the game's checkpoint and save file stay as they were.
    private static byte[] CheckpointHere() {
        var checkpoint = Game.Checkpoint.SaveGameData;
        // SaveToWriter disposes its writer, and the stream with it; ToArray still works after that
        var stream = new MemoryStream();
        checkpoint.SaveToWriter(new BinaryWriter(stream));
        var kept = stream.ToArray();
        // scenes left since the last checkpoint: SaveToWriter skips them and LoadFromReader clears them
        var pending = new Dictionary<MoonGuid, SaveScene>(checkpoint.PendingScenes);

        var touching = new Dictionary<int, int>();
        for (var id = RandomizerBoxes.FirstActiveId; id <= RandomizerBoxes.LastActiveId; id++) {
            var bits = Randomizer.Inventory.GetRandomizerItem(id);
            if (bits != 0) {
                touching[id] = bits;
                Randomizer.Inventory.SetRandomizerItem(id, 0);
            }
        }

        try {
            // CreateCheckpoint without its OnPostCreate, which would move the scenes kept loaded for the real one
            SaveSceneManager.Master.SaveWithoutClearing(checkpoint.Master);
            checkpoint.ApplyPendingScenes();
            foreach (var scene in Core.Scenes.Manager.ActiveScenes) {
                if (scene.IsVisible && scene.HasStartBeenCalled && scene.SceneRoot.SaveSceneManager) {
                    scene.SceneRoot.SaveSceneManager.Save(checkpoint.InsertScene(scene.MetaData.SceneMoonGuid));
                }
            }

            SaveSlotsManager.CurrentSaveSlot.FillData();
            return GameController.Instance.SaveGameController.SaveToBytes();
        } finally {
            foreach (var pair in touching) {
                Randomizer.Inventory.SetRandomizerItem(pair.Key, pair.Value);
            }

            checkpoint.LoadFromReader(new BinaryReader(new MemoryStream(kept)));
            foreach (var pair in pending) {
                checkpoint.PendingScenes[pair.Key] = pair.Value;
            }
        }
    }

    // a new segment folder around a save, with no end yet, named "New Segment N" unless given a name
    public static string CreateFrom(byte[] save, string name) {
        if (string.IsNullOrEmpty(name)) {
            name = PracticeSelect.NextName();
        }

        var path = PracticeSelect.PathFor(name);
        var segment = JsonValue.NewObject();
        segment.Set("version", JsonValue.Of(1));
        segment.Set("name", JsonValue.Of(name));
        segment.Set("end", JsonValue.NewObject());
        BfrpFile.Create(path, segment, save).Save();
        Randomizer.log("practice: created " + path + " (" + save.Length + " byte save)");
        return path;
    }
}

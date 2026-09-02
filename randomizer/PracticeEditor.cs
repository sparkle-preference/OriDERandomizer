using System;
using System.IO;
using Game;
using UnityEngine;

// The in-engine half of the segment editor: boxes drawn with the mouse over the frozen
// world, the camera panned with WASD, written back into the container's segment.json.
// Names, hint text and colours are the companion page's job. Creating a segment from a
// normal game snapshots the current save into a new container in the practice folder.
public static class PracticeEditor {
    public static bool Active;

    // the rectangle being dragged, in world units
    public static Rect? Draft;

    private static Vector2 dragFrom;

    private static bool dragging;

    private static string tool = "kill";

    // with a variant running its own list takes the boxes, unless V says the shared one
    private static bool toVariant = true;

    // what Z takes back: the box just placed, and whose list it went to
    private static RandomizerBox lastBox;

    private static string lastTarget;

    private const float PanSpeed = 24f;

    // a box smaller than this is a click
    private const float MinSide = 0.5f;

    // From the practice pause menu: the attempt stops where it is and the boxes are
    // edited over it; saving restarts the attempt with the new boxes.
    public static void Begin() {
        if (!PracticeController.Active || PracticeController.File == null) {
            return;
        }

        PracticeController.Current = PracticeController.Phase.Editing;
        PracticeController.Freeze();
        Active = true;
        Draft = null;
        dragging = false;
        lastBox = null;
        // the server first, so the help can say where the page is
        PracticeServer.OpenOnce();
        Help();
    }

    public static void Stop() {
        Active = false;
        Draft = null;
        dragging = false;
    }

    // save what was drawn and run it
    public static void SaveAndRetry() {
        if (!Active) {
            return;
        }

        try {
            PracticeController.File.Save();
        } catch (Exception e) {
            Randomizer.LogError("practice: could not save the segment: " + e.Message);
        }

        Stop();
        PracticeController.Retry();
    }

    public static void Tick() {
        if (!Active) {
            return;
        }

        Pan();
        // number keys: the letters are taken by the pan
        if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1)) {
            Tool("goal");
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2)) {
            Tool("kill");
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3)) {
            Tool("hint");
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4)) {
            Tool("solid");
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad5)) {
            PracticeServer.Open();
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.V)) {
            toVariant = !toVariant;
            Help();
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Z)) {
            Undo();
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.X)) {
            DeleteUnderCursor();
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)) {
            SaveAndRetry();
            return;
        }

        var at = World(Core.Input.CursorPosition);
        if (Core.Input.LeftClick.OnPressed && !Game.UI.MainMenuVisible) {
            dragFrom = at;
            dragging = true;
        }

        if (!dragging) {
            return;
        }

        Draft = Between(dragFrom, at);
        if (Core.Input.LeftClick.OnReleased) {
            dragging = false;
            var drawn = Draft.Value;
            Draft = null;
            if (drawn.width >= MinSide && drawn.height >= MinSide) {
                Commit(drawn);
            }
        }
    }

    private static void Tool(string name) {
        tool = name;
        Help();
    }

    private static bool HasVariant {
        get { return PracticeController.File != null && !string.IsNullOrEmpty(PracticeController.File.Variant); }
    }

    private static bool TargetVariant {
        get { return toVariant && HasVariant; }
    }

    private static void Help() {
        var where = !HasVariant ? "" : "\n"
            + (TargetVariant ? "boxes go to this variant" : "boxes go to the shared list") + "   V switches";
        Randomizer.printInfo("EDITING - drag to draw a " + tool + " box" + where + "\n"
            + "1 goal   2 kill   3 hint   4 solid   Z undo   X delete under cursor\n"
            + "WASD pan   Enter save and retry\n"
            + "5 open the segment editor in a browser" + (PracticeServer.Running ? "   " + PracticeServer.Url : ""), 1800);
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

    private static Rect Between(Vector2 a, Vector2 b) {
        var min = Vector2.Min(a, b);
        var max = Vector2.Max(a, b);
        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    // The goal is always shared and there is one; everything else joins the variant's
    // list or the shared one. A hint is an item box that prints, until the page says
    // what it gives.
    private static void Commit(Rect area) {
        var file = PracticeController.File;
        var box = new RandomizerBox();
        box.Area = area;
        var target = "";
        if (tool == "goal") {
            box.Type = RandomizerBox.Kind.Goal;
        } else {
            target = TargetVariant ? file.Variant : "";
            if (tool == "kill") {
                box.Type = RandomizerBox.Kind.Kill;
                box.Give = new RandomizerAction("RB", "3");
            } else if (tool == "solid") {
                box.Type = RandomizerBox.Kind.Solid;
            } else {
                box.Type = RandomizerBox.Kind.Item;
                box.Give = new RandomizerAction("SH", "hint");
            }
        }

        box.SetColour("");
        var boxes = file.Boxes(target);
        if (box.Type == RandomizerBox.Kind.Goal) {
            boxes.RemoveAll(b => b.Type == RandomizerBox.Kind.Goal);
        }

        boxes.Add(box);
        file.SetBoxes(target, boxes);
        lastBox = box;
        lastTarget = target;
        PracticeController.Reparse();
    }

    private static void Undo() {
        if (lastBox == null) {
            return;
        }

        var file = PracticeController.File;
        var boxes = file.Boxes(lastTarget);
        var line = lastBox.ToLine();
        for (var i = boxes.Count - 1; i >= 0; i--) {
            if (boxes[i].ToLine() == line) {
                boxes.RemoveAt(i);
                break;
            }
        }

        file.SetBoxes(lastTarget, boxes);
        lastBox = null;
        PracticeController.Reparse();
    }

    // the variant's boxes first, then the shared ones, the goal among them
    private static void DeleteUnderCursor() {
        var file = PracticeController.File;
        var at = World(Core.Input.CursorPosition);
        var targets = HasVariant ? new[] { file.Variant, "" } : new[] { "" };
        foreach (var target in targets) {
            var boxes = file.Boxes(target);
            for (var i = boxes.Count - 1; i >= 0; i--) {
                if (boxes[i].Area.Contains(at)) {
                    boxes.RemoveAt(i);
                    file.SetBoxes(target, boxes);
                    PracticeController.Reparse();
                    return;
                }
            }
        }
    }

    // From a normal game: the current state becomes a new segment's save, and the
    // segment starts empty. Nothing about the seed changes.
    public static void Create() {
        if (Characters.Sein == null) {
            return;
        }

        if (PracticeController.Active) {
            Randomizer.printInfo("Practice: end the current session before creating a segment", 300);
            return;
        }

        try {
            var game = GameController.Instance;
            game.CreateCheckpoint();
            game.SaveGameController.PerformSave();
            var bytes = File.ReadAllBytes(game.SaveGameController.GetSaveFilePath(SaveSlotsManager.CurrentSlotIndex));

            var area = "segment";
            using (var reader = new BinaryReader(new MemoryStream(bytes))) {
                var info = new SaveSlotInfo();
                if (info.LoadFromReader(reader) && !string.IsNullOrEmpty(info.AreaName)) {
                    area = info.AreaName;
                }
            }

            var name = area + " " + DateTime.Now.ToString("yyyy-MM-dd HHmm");
            var safe = name;
            foreach (var bad in Path.GetInvalidFileNameChars()) {
                safe = safe.Replace(bad, '-');
            }

            var folder = PracticeSelect.Folder;
            if (!Directory.Exists(folder)) {
                Directory.CreateDirectory(folder);
            }

            var path = Path.Combine(folder, safe + ".bfrp");
            var segment = JsonValue.NewObject();
            segment.Set("version", JsonValue.Of(1));
            segment.Set("name", JsonValue.Of(name));
            segment.Set("end", JsonValue.NewObject());
            segment.Set("boxes", JsonValue.NewArray());
            BfrpFile.Create(path, segment, bytes).Save();
            Randomizer.printInfo("Practice segment saved: " + path + "\nStart it from PRACTICE and press EDIT BOXES", 600);
            Randomizer.log("practice: created " + path + " (" + bytes.Length + " byte save)");
        } catch (Exception e) {
            Randomizer.LogError("practice: could not create a segment: " + e.Message);
        }
    }
}

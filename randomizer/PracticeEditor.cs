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

    private static string tool = "death";

    // with a variant running its own list takes the boxes, unless V says the shared one
    private static bool toVariant = true;

    private enum Placed {
        Nothing,
        Goal,
        Shared,
        Variant
    }

    private static Placed last;

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
        last = Placed.Nothing;
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
            Tool("death");
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3)) {
            Tool("hint");
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4)) {
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
            + "1 goal   2 death   3 hint   Z undo   X delete under cursor\n"
            + "WASD pan   Enter save and retry\n"
            + "4 open the segment editor in a browser" + (PracticeServer.Running ? "   " + PracticeServer.Url : ""), 1800);
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

        cameras.Transform.position += move.normalized * PanSpeed * Time.unscaledDeltaTime;
        if (cameras.Controller != null) {
            cameras.Controller.UpdateCamera();
        }
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

    private static JsonValue Corners(Rect area) {
        var corners = JsonValue.NewArray();
        corners.Add(JsonValue.Of(Math.Round(area.xMin, 1)));
        corners.Add(JsonValue.Of(Math.Round(area.yMin, 1)));
        corners.Add(JsonValue.Of(Math.Round(area.xMax, 1)));
        corners.Add(JsonValue.Of(Math.Round(area.yMax, 1)));
        return corners;
    }

    private static Rect Area(JsonValue corners) {
        return Between(
            new Vector2((float)corners[0].Num, (float)corners[1].Num),
            new Vector2((float)corners[2].Num, (float)corners[3].Num));
    }

    // The goal is the end condition's box, always shared; everything else joins the
    // variant's list or the shared one.
    private static void Commit(Rect area) {
        var file = PracticeController.File;
        if (tool == "goal") {
            var end = file.Segment["end"];
            if (!end.IsObject) {
                end = JsonValue.NewObject();
                file.Segment.Set("end", end);
            }

            end.Set("box", Corners(area));
            last = Placed.Goal;
        } else {
            var box = JsonValue.NewObject();
            box.Set("box", Corners(area));
            box.Set("type", JsonValue.Of(tool));
            if (tool == "hint") {
                box.Set("text", JsonValue.Of("hint"));
            }

            if (TargetVariant) {
                var json = Variant();
                Boxes(json).Add(box);
                file.SetVariantSegment(file.Variant, json);
                last = Placed.Variant;
            } else {
                Boxes(file.Segment).Add(box);
                last = Placed.Shared;
            }
        }

        Reparse();
    }

    // the variant's json is parsed fresh each time, so a change has to be written back
    private static JsonValue Variant() {
        var json = PracticeController.File.VariantSegment(PracticeController.File.Variant);
        return json.IsObject ? json : JsonValue.NewObject();
    }

    private static JsonValue Boxes(JsonValue json) {
        var boxes = json["boxes"];
        if (!boxes.IsArray) {
            boxes = JsonValue.NewArray();
            json.Set("boxes", boxes);
        }

        return boxes;
    }

    private static void Undo() {
        var file = PracticeController.File;
        if (last == Placed.Goal) {
            if (file.Segment["end"].IsObject) {
                file.Segment["end"].Set("box", JsonValue.Null());
            }
        } else if (last == Placed.Variant && HasVariant) {
            var json = Variant();
            var boxes = json["boxes"];
            if (boxes.IsArray && boxes.Count > 0) {
                json.Set("boxes", Without(boxes, boxes.Count - 1));
                file.SetVariantSegment(file.Variant, json);
            }
        } else if (last == Placed.Shared) {
            var boxes = file.Segment["boxes"];
            if (boxes.IsArray && boxes.Count > 0) {
                file.Segment.Set("boxes", Without(boxes, boxes.Count - 1));
            }
        }

        last = Placed.Nothing;
        Reparse();
    }

    // the variant's boxes first, then the shared ones, then the goal
    private static void DeleteUnderCursor() {
        var file = PracticeController.File;
        var at = World(Core.Input.CursorPosition);
        if (HasVariant) {
            var json = Variant();
            var index = Under(json["boxes"], at);
            if (index >= 0) {
                json.Set("boxes", Without(json["boxes"], index));
                file.SetVariantSegment(file.Variant, json);
                Reparse();
                return;
            }
        }

        var shared = Under(file.Segment["boxes"], at);
        if (shared >= 0) {
            file.Segment.Set("boxes", Without(file.Segment["boxes"], shared));
            Reparse();
            return;
        }

        var goal = file.Segment["end"]["box"];
        if (goal.IsArray && goal.Count == 4 && Area(goal).Contains(at)) {
            file.Segment["end"].Set("box", JsonValue.Null());
            Reparse();
        }
    }

    private static int Under(JsonValue boxes, Vector2 at) {
        for (var i = boxes.Count - 1; i >= 0; i--) {
            var corners = boxes[i]["box"];
            if (corners.IsArray && corners.Count == 4 && Area(corners).Contains(at)) {
                return i;
            }
        }

        return -1;
    }

    private static JsonValue Without(JsonValue boxes, int index) {
        var kept = JsonValue.NewArray();
        for (var i = 0; i < boxes.Count; i++) {
            if (i != index) {
                kept.Add(boxes[i]);
            }
        }

        return kept;
    }

    // the drawn boxes are the live ones, so the file's json is what the session runs
    private static void Reparse() {
        var file = PracticeController.File;
        PracticeController.Segment = PracticeSegment.Parse(file.Segment, file.VariantSegment(file.Variant));
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

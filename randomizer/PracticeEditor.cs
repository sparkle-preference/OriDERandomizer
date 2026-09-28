using System;
using System.Globalization;
using System.IO;
using Game;
using UnityEngine;

// The in-game half of the segment editor: boxes drawn with the mouse over the frozen world,
// kept as BX lines in the container's placements.bfr. Create snapshots a save into a new container.
public static class PracticeEditor {
    public static bool Active;

    // the rectangle being dragged, in world units
    public static RandomizerBox? Draft;

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

    // the attempt freezes where it is and boxes are drawn over it; Enter saves and retries
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

    // save what was drawn and run it; with nothing to end the run yet, stay
    public static void SaveAndRetry() {
        if (!Active) {
            return;
        }

        if (!Write()) {
            Say("The segment could not be saved; see randomizer.log", 300);
            return;
        }

        if (PracticeController.Segment != null && !PracticeController.Segment.HasEnd) {
            Say("Saved. Nothing ends this segment yet: draw a goal box with 1, or set the end condition on the page", 600);
            return;
        }

        Stop();
        PracticeController.Retry();
    }

    // save what was drawn and keep editing
    public static void Save() {
        if (Active && Write()) {
            Say("Segment saved", 180);
        }
    }

    private static bool Write() {
        try {
            PracticeController.File.Save();
            return true;
        } catch (Exception e) {
            Randomizer.LogError("practice: could not save the segment: " + e.Message);
            return false;
        }
    }

    // what is on disk replaces everything drawn since the last save
    public static bool Reload() {
        if (!Active) {
            return false;
        }

        try {
            var fresh = BfrpFile.Load(PracticeController.File.Path);
            fresh.Variant = PracticeController.File.Variant;
            PracticeController.File = fresh;
            lastBox = null;
            PracticeController.Reparse();
            Say("Segment reloaded from disk", 180);
            return true;
        } catch (Exception e) {
            Randomizer.LogError("practice: could not reload the segment: " + e.Message);
            return false;
        }
    }

    public static void Tick() {
        Draft?.DeInit();
        Draft = null;

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
            }
        } else {
            Pan();
        }

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
        }

        if (Core.Input.LeftClick.OnPressed && !Game.UI.MainMenuVisible) {
            dragFrom = at;
            dragging = true;
        }

        if (!dragging) {
            return;
        }

        Draft = CreateBox(Between(dragFrom, at), "renderDepth=-99", "ffff99", "");
        Draft!.Init(-1);
        if (Core.Input.LeftClick.OnReleased) {
            dragging = false;
            var drawn = Draft!.Rect;
            Draft.DeInit();
            Draft = null;
            if (drawn.width >= MinSide && drawn.height >= MinSide) {
                Commit(drawn);
            }
        }
    }

    private static void Tool(string name) {
        tool = name;
        // the legend is what says which box you are drawing, so it changes as you change it
        Randomizer.clearMessage();
        Help();
    }

    private static bool HasVariant {
        get { return PracticeController.File != null && !string.IsNullOrEmpty(PracticeController.File.Variant); }
    }

    private static bool TargetVariant {
        get { return toVariant && HasVariant; }
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
        var where = !HasVariant ? "" : "\n"
            + (TargetVariant ? "boxes go to this variant" : "boxes go to the shared list") + "   V: switch";
        Randomizer.printQuiet("EDITING - drag to draw a " + tool + " box" + where + "\n"
            + "1: goal   2: kill   3: hint   4: solid   Z: undo   X: delete under cursor\n"
            + "WASD: pan   right click: stand Ori here   Ctrl+S: save   Ctrl+R: reload from disk   Enter: save and retry\n"
            + "5: open the segment editor in a browser" + (PracticeServer.Running ? "   " + PracticeServer.Url : ""), 1800);
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

    // The goal is shared and single; other boxes join the target list.
    private static void Commit(Rect area) {
        var file = PracticeController.File;
        var target = tool != "goal" && TargetVariant ? file.Variant : "";

        var (boxFlags, boxItem) = tool switch {
            "goal" => ("goal", ""),
            "kill" => ("kill", ""),
            "solid" => ("solid", ""),
            _ => ("item", "SH|hint"),
        };

        var box = CreateBox(area, boxFlags, "", boxItem);

        var boxes = file.Boxes(target);
        if (box.Goal) {
            for (var i = 0; i < boxes.Count; i++) {
                if (boxes[i].Goal && !boxes[i].Deleted) {
                    boxes[i] = Buried();
                }
            }
        }

        boxes.Add(box);
        file.SetBoxes(target, boxes);
        lastBox = box;
        lastTarget = target;
        PracticeController.Reparse();
    }

    private static RandomizerBox CreateBox(Rect area, string flags, string color, string item) {
        var areaString = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##},{2:0.##},{3:0.##}", area.min.x, area.min.y, area.max.x, area.max.y);

        return RandomizerBox.Parse($"BX|{flags}|{areaString}|{color}|{item}");
    }

    private static void Undo() {
        if (lastBox == null) {
            return;
        }

        var file = PracticeController.File;
        var boxes = file.Boxes(lastTarget);
        var line = lastBox.Line;
        for (var i = boxes.Count - 1; i >= 0; i--) {
            if (boxes[i].Line == line) {
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
                if (!boxes[i].Deleted && boxes[i].Rect.Contains(at)) {
                    boxes[i] = Buried();
                    file.SetBoxes(target, boxes);
                    PracticeController.Reparse();
                    return;
                }
            }
        }
    }

    // a deleted box keeps a line, so the boxes after it keep their numbers
    private static RandomizerBox Buried() {
        return RandomizerBox.Parse(RandomizerBox.Tombstone);
    }

    // from a normal game: the current state becomes a new, empty segment's save; the seed carries on
    public static void Create() {
        if (Characters.Sein == null) {
            return;
        }

        if (PracticeController.Active) {
            Randomizer.printInfo("Practice: end the current session before creating a segment", 300);
            return;
        }

        try {
            var path = CreateFrom(CheckpointHere(), null);
            Randomizer.printInfo("Practice segment saved: " + path + "\nStart it from PRACTICE MODE: it opens in the editor", 600);
        } catch (Exception e) {
            Randomizer.LogError("practice: could not create a segment: " + e.Message);
        }
    }

    // The save a checkpoint here would write, box contact cleared; the game's checkpoint and save file stay as they were.
    private static byte[] CheckpointHere() {
        var checkpoint = Game.Checkpoint.SaveGameData;
        // SaveToWriter disposes its writer, and the stream with it; ToArray still works after that
        var stream = new MemoryStream();
        checkpoint.SaveToWriter(new BinaryWriter(stream));
        var kept = stream.ToArray();
        // scenes left since the last checkpoint: SaveToWriter skips them and LoadFromReader clears them
        var pending = new System.Collections.Generic.Dictionary<MoonGuid, SaveScene>(checkpoint.PendingScenes);

        var touching = new System.Collections.Generic.Dictionary<int, int>();
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

    // a new container around a save, with no end yet, named "New Segment N" unless given a name
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

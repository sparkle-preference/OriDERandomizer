using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// The loaded seed file's boxes, edited in place: a box's number is its place among the file's box
// lines, so an edited box keeps its line, a new one is appended and a deleted one leaves a tombstone.
public class SeedBoxes {
    // the seed the box editor or its page last opened; a seed reload drops it
    public static SeedBoxes Current;

    public readonly string Path;

    public List<RandomizerBox> Boxes = new List<RandomizerBox>();

    public bool Dirty;

    // bumps on every change, so the page can tell its copy is stale
    public int Revision;

    private List<string> lines = new List<string>();

    // the file as it was read, so a save can tell it changed underneath
    private string found = "";

    // the file line each box came from, in box order
    private List<int> at = new List<int>();

    private string eol = "\n";

    private bool endsWithEol;

    private bool backedUp;

    private SeedBoxes(string path) {
        Path = path;
    }

    public static SeedBoxes Load(string path) {
        var seed = new SeedBoxes(path);
        seed.Read();
        return seed;
    }

    // a seed with a sync flag is a netcode game's: its server would never hear about boxes drawn here
    public static bool Synced {
        get { return !string.IsNullOrEmpty(Randomizer.SyncId); }
    }

    private void Read() {
        var text = File.ReadAllText(Path);
        found = text;
        eol = text.Contains("\r\n") ? "\r\n" : "\n";
        endsWithEol = text.EndsWith("\n");
        lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
        if (endsWithEol) {
            lines.RemoveAt(lines.Count - 1);
        }

        Boxes = new List<RandomizerBox>();
        at = new List<int>();
        // line 0 is the flags; a box line the game can't read has no number, as in Randomizer.initialize
        for (var i = 1; i < lines.Count; i++) {
            if (!RandomizerBox.IsLine(lines[i])) {
                continue;
            }

            try {
                Boxes.Add(RandomizerBox.Parse(lines[i]));
                at.Add(i);
            } catch (Exception) {
            }
        }

        Dirty = false;
    }

    public void SetBoxes(List<RandomizerBox> boxes) {
        Boxes = new List<RandomizerBox>(boxes);
        Dirty = true;
        Revision++;
    }

    // other lines stay byte for byte and a deleted box is written as the tombstone; the first save keeps a backup
    public void Save() {
        if (File.ReadAllText(Path) != found) {
            throw new IOException("the seed file changed on disk since it was read; Ctrl+R reads it again");
        }

        var output = new List<string>(lines);
        for (var i = 0; i < at.Count && i < Boxes.Count; i++) {
            output[at[i]] = RandomizerBox.Modern(Boxes[i].Line);
        }

        // an undone box that was already saved gives its line back
        for (var i = at.Count - 1; i >= Boxes.Count; i--) {
            output.RemoveAt(at[i]);
        }

        for (var i = at.Count; i < Boxes.Count; i++) {
            output.Add(RandomizerBox.Modern(Boxes[i].Line));
        }

        if (!backedUp) {
            var backup = Path + ".before-boxes";
            // one backup per seed: a later edit of it keeps the original, another seed replaces it
            if (!File.Exists(backup) || Unboxed(File.ReadAllText(backup)) != Unboxed(found)) {
                File.Copy(Path, backup, true);
            }

            backedUp = true;
        }

        var temp = Path + ".tmp";
        File.WriteAllText(temp, string.Join(eol, output.ToArray()) + (endsWithEol ? eol : ""));
        File.Replace(temp, Path, null);
        Read();
        Revision++;
    }

    // every line but the boxes: what tells one seed from another
    private static string Unboxed(string text) {
        return string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Where(line => !RandomizerBox.IsLine(line)).ToArray());
    }

    // what is on disk replaces every unsaved edit
    public void Reload() {
        Read();
        Revision++;
    }

    // the edited list in force, unless a practice session has the floor
    public void Apply() {
        RandomizerBoxes.Seed.Clear();
        RandomizerBoxes.Seed.AddRange(Boxes);
        RandomizerBoxes.SeedLoaded();
    }

    // every box line in order, tombstones too, so pasting them keeps each box's number
    public string Text() {
        return string.Join("\n", Boxes.Select(box => box.Line).ToArray());
    }
}

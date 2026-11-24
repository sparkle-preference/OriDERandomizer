// Offline stand-ins for the engine-bound statics the pickup code calls; each shadows the dll's copy.
using System.Collections.Generic;

static class Randomizer {
    public static RandomizerInventory Inventory;
    public static List<string> Errors = new List<string>();
    public static List<string> Logs = new List<string>();

    public static void LogError(string text) { Errors.Add(text); }

    public static void log(string text) { Logs.Add(text); }
}

static class RandomizerSettings {
    public class BoolSetting { public bool Value; }

    public static class Accessibility { public static BoolSetting TextCues = new BoolSetting(); }
}

// must match RandomizerMW.cs and RandomizerBox.cs; pickups_props checks them against the source
static class RandomizerMW {
    public const int GrantedSlotsBase = 940;
    public const int GrantedSlotsLast = 947;
}

static class RandomizerBoxes {
    public const int FirstBitId = 1700;
    public const int LastBitId = 1999;
    public static List<int> Masks = new List<int>();

    public static void MaskUpdated(int code) { Masks.Add(code); }

    public static void Serialize(Archive ar) { }

    // a fake seed's boxes: middles in hundredths, and which are deleted or off
    public static long[] MiddleX = new long[0], MiddleY = new long[0];
    public static bool[] Gone = new bool[0], Off = new bool[0];
    public static List<int> Moves = new List<int>();

    public static bool Exists(int box) { return box >= 0 && box < MiddleX.Length; }

    public static bool Deleted(int box) { return Gone[box]; }

    public static bool IsOn(int box) { return !Gone[box] && !Off[box]; }

    public static bool Middle(int box, out long x, out long y) {
        x = MiddleX[box];
        y = MiddleY[box];
        return System.Math.Abs(x) < 1000000000 && System.Math.Abs(y) < 1000000000;
    }

    public static void Moved(int box) { Moves.Add(box); }

    public static Dictionary<int, int[]> Offsets = new Dictionary<int, int[]>();

    public static void Offset(int box, out int x, out int y) {
        int[] at;
        x = Offsets.TryGetValue(box, out at) ? at[0] : 0;
        y = at != null ? at[1] : 0;
    }

    public static void Place(int box, int x, int y, int turn) {
        if (x == 0 && y == 0) {
            Offsets.Remove(box);
        } else {
            Offsets[box] = new[] { x, y };
        }

        if (turn == 0) {
            Rotations.Remove(box);
        } else {
            Rotations[box] = turn;
        }

        Moved(box);
    }

    public static Dictionary<int, int> Rotations = new Dictionary<int, int>();

    // the goal and unsafe boxes of a fake seed
    public static bool[] Fixed = new bool[0];

    public static int Rotation(int box) {
        int turn;
        return Rotations.TryGetValue(box, out turn) ? turn : 0;
    }

    public static bool CanTurn(int box) { return box >= Fixed.Length || !Fixed[box]; }
}

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

    // a fake seed's boxes: top-left corners in hundredths, and which are deleted or off
    public static long[] CornerX = new long[0], CornerY = new long[0];
    public static bool[] Gone = new bool[0], Off = new bool[0];
    public static List<int> Moves = new List<int>();

    public static bool Exists(int box) { return box >= 0 && box < CornerX.Length; }

    public static bool Deleted(int box) { return Gone[box]; }

    public static bool IsOn(int box) { return !Gone[box] && !Off[box]; }

    public static bool Corner(int box, out long x, out long y) {
        x = CornerX[box];
        y = CornerY[box];
        return System.Math.Abs(x) < 1000000000 && System.Math.Abs(y) < 1000000000;
    }

    public static void Moved(int box) { Moves.Add(box); }
}

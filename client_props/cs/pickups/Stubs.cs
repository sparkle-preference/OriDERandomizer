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
}

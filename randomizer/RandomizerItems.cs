using System.Collections.Generic;

// Naming an item, and reading one off a cross-world seed line. Deliberately
// free of any game dependency so it can be exercised on its own.
public static class RandomizerItems {
    public static readonly Dictionary<string, string> SkillNames = new Dictionary<string, string> {
        { "0", "Bash" }, { "2", "Charge Flame" }, { "3", "Wall Jump" }, { "4", "Stomp" }, { "5", "Double Jump" },
        { "8", "Charge Jump" }, { "12", "Climb" }, { "14", "Glide" }, { "50", "Dash" }, { "51", "Grenade" }, { "15", "Spirit Flame" }
    };

    public static readonly Dictionary<string, string> EventNames = new Dictionary<string, string> {
        { "0", "Water Vein" }, { "1", "Clean Water" }, { "2", "Gumon Seal" }, { "3", "Wind Restored" }, { "4", "Sunstone" }, { "5", "Warmth Returned" }
    };

    private static readonly Dictionary<string, string> ShardNames = new Dictionary<string, string> {
        { "17", "Water Vein Shard" }, { "19", "Gumon Seal Shard" }, { "21", "Sunstone Shard" }, { "28", "Warmth Fragment" }
    };

    /// <summary>
    /// The item a cross-world line carries, out of its comma field. An owner
    /// above the player count is an Archipelago shadow, and its line names a
    /// recipient and a promised slot before the item. A player count of 0 means
    /// the seed never said, so every line reads as an ordinary cross-world one.
    /// </summary>
    public static bool Inner(string value, int players, out string code, out string id) {
        code = null;
        id = null;
        if (string.IsNullOrEmpty(value)) {
            return false;
        }

        var owned = value.Split(',');
        if (owned.Length < 4 || !int.TryParse(owned[0], out var owner)) {
            return false;
        }

        var width = players > 0 && owner > players ? 6 : 4;
        var parts = value.Split(new[] { ',' }, width);
        if (parts.Length != width) {
            return false;
        }

        code = parts[width - 2];
        id = parts[width - 1];
        return true;
    }

    /// <summary>The one place a code and an id become something a player reads.</summary>
    public static string Name(string code, string id) {
        switch (code) {
            case "SK":
                return SkillNames.ContainsKey(id) ? SkillNames[id] : "Unknown Skill " + id;
            case "EV":
                return EventNames.ContainsKey(id) ? EventNames[id] : "Unknown Event " + id;
            case "TP":
                return id + " Teleporter";
            case "TW":
                return id.Split(',')[0];   // "<name>,<x>,<y>,<node>"
            case "AP":
                return id;                 // a foreign game's item: the id is the room's own name
            case "HC":
                return "Health Cell";
            case "EC":
                return "Energy Cell";
            case "AC":
                return "Ability Cell";
            case "KS":
                return "Keystone";
            case "MS":
                return "Mapstone";
            case "EX":
                return id + " Experience";
            case "RB":
                return ShardNames.ContainsKey(id) ? ShardNames[id] : "Bonus Item";
            default:
                return code + "|" + id;
        }
    }
}

using System;
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

    // 112 is rewritten as the Pokeball's contents change, so this is live state
    public static Dictionary<int, string> BonusSkillNames = new Dictionary<int, string> {
        { 101, "Polarity Shift" },
        { 102, "Gravity Swap" },
        { 103, "Extreme Speed" },
        { 104, "Teleport to Last AltR" },
        { 105, "Teleport to Soul Link" },
        { 106, "Respec" },
        { 107, "Level Explosion" },
        { 108, "Toggle Movement Bonuses" },
        { 109, "Timewarp" },
        { 110, "Invincibility" },
        { 111, "Wither" },
        { 112, "Pokeball" },
        { 113, "Toggle Bash/Stomp Damage" },
        { 114, "Summon Mom" },
        { 115, "Toggle Enhanced Effects" },
        { 116, "Mark" },
        { 1587, "Warp to Credits" },
    };

    private static readonly HashSet<string> blueStuff = new HashSet<string> { "Water Vein", "Ginso Teleporter", "Clean Water" };
    private static readonly HashSet<string> orangeStuff = new HashSet<string> { "Gumon Seal", "Forlorn Teleporter", "Wind Restored" };
    private static readonly HashSet<string> redStuff = new HashSet<string> { "Sunstone", "Horu Teleporter", "Warmth Returned" };

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
                if (ShardNames.ContainsKey(id)) {
                    return ShardNames[id];
                }

                int bonus;
                return int.TryParse(id, out bonus) && BonusSkillNames.ContainsKey(bonus) ? BonusSkillNames[bonus] : "Bonus Item";
            default:
                return code + "|" + id;
        }
    }

    /// <summary>
    /// What a player sees on picking the item up: the name in its colors, or
    /// the phrasing that item announces itself with. Variants that depend on
    /// game state -- a teleporter still short its shards, a clue-locked one --
    /// belong to the caller, which is what keeps this class game-free.
    /// </summary>
    public static string Message(string code, string id) {
        if (code == "TP") {
            var color = ColorOf(Name(code, id));
            return color + id + " teleporter activated" + color;
        }

        return ColorWrap(Name(code, id));
    }

    public static string ColorWrap(string input) {
        var color = ColorOf(input);
        return color + input + color;
    }

    private static string ColorOf(string name) {
        if (SkillNames.ContainsValue(name)) {
            return "$"; // skill names are green
        }

        if (blueStuff.Contains(name)) {
            return "*"; // blue stuff is blue
        }

        if (orangeStuff.Contains(name)) {
            return "#"; // orange stuff is orange
        }

        if (redStuff.Contains(name)) {
            return "@"; // red stuff is red
        }

        return ""; // this could have been a poem
    }

    // TEMP F2CHECK -- remove before merge, along with every Verified() call.
    public static Action<string> ParityLog;

    /// <summary>
    /// Returns the literal the old code would have printed, logging when
    /// Message() disagrees. Lets a call site be converted with no change in
    /// what the player sees until the log comes back clean.
    /// </summary>
    public static string Verified(string code, string id, string old) {
        var mine = Message(code, id);
        if (mine != old && ParityLog != null) {
            ParityLog($"F2CHECK PARITY {code}|{id}: old \"{old}\" != new \"{mine}\"");
        }

        return old;
    }
}

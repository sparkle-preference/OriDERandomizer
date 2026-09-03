using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Naming items and reading cross-world lines; must stay free of game types.
public static class RandomizerItems {
    public static readonly Dictionary<string, string> SkillNames = new Dictionary<string, string> {
        { "0", "Bash" }, { "2", "Charge Flame" }, { "3", "Wall Jump" }, { "4", "Stomp" }, { "5", "Double Jump" },
        { "8", "Charge Jump" }, { "12", "Climb" }, { "14", "Glide" }, { "50", "Dash" }, { "51", "Grenade" }, { "15", "Spirit Flame" }
    };

    public static readonly Dictionary<string, string> EventNames = new Dictionary<string, string> {
        { "0", "Water Vein" }, { "1", "Clean Water" }, { "2", "Gumon Seal" }, { "3", "Wind Restored" }, { "4", "Sunstone" }, { "5", "Warmth Returned" }
    };

    // RB names; the ids and what they mean live in RandomizerItemIDs.txt.
    private static readonly Dictionary<string, string> UpgradeNames = new Dictionary<string, string> {
        { "17", "Water Vein Shard" }, { "19", "Gumon Seal Shard" }, { "21", "Sunstone Shard" },
        { "28", "Warmth Fragment" }, { "1108", "Autoplayer" }, { "1109", "Pickup Drop" },
        { "0", "Mega Health" }, { "1", "Mega Energy" }, { "2", "Go Home" },
        { "3", "Kill Ori" }, { "4", "Air Refresh" },
        { "6", "Attack Upgrade" }, { "8", "Explosion Power Upgrade" }, { "9", "Spirit Light Efficiency" },
        { "10", "Extra Air Dash" }, { "11", "Charge Dash Efficiency" }, { "12", "Extra Double Jump" },
        { "13", "Health Regeneration" }, { "15", "Energy Regeneration" }, { "30", "Bleeding" },
        { "31", "Health Leech" }, { "32", "Energy Leech" }, { "33", "Skill Velocity Upgrade" },
        { "34", "Disable Warping" }, { "35", "Enable Warping" }, { "36", "Underwater Skill Usage" },
        { "37", "Jump Upgrade" }, { "38", "Mini Health" }, { "39", "Mini Energy" },
        { "40", "Remove Wall Jump" }, { "41", "Remove Charge Flame" }, { "42", "Remove Double Jump" },
        { "43", "Remove Bash" }, { "44", "Remove Stomp" }, { "45", "Remove Glide" },
        { "46", "Remove Climb" }, { "47", "Remove Charge Jump" }, { "48", "Remove Dash" },
        { "49", "Remove Grenade" }, { "81", "Stomp/Grenade Hint" }, { "200", "Quick Flame" },
        { "201", "Spark Flame" }, { "202", "Charge Flame Burn" }, { "203", "Split Flame" },
        { "204", "Ultra Light Burst" }, { "205", "Cinder Flame" }, { "206", "Ultra Stomp" },
        { "207", "Rapid Flame" }, { "208", "Charge Flame Blast" }, { "209", "Ultra Split Flame" },
        { "210", "Spirit Magnet" }, { "211", "Drop Efficiency" }, { "212", "Health Efficiency" },
        { "213", "Ultra Spirit Magnet" }, { "214", "Energy Efficiency" }, { "215", "Spirit Efficiency" },
        { "216", "Spirit Potency" }, { "217", "Health Regen (Ability)" }, { "218", "Energy Regen (Ability)" },
        { "219", "Sense" }, { "220", "Rekindle" }, { "221", "Regroup" },
        { "222", "Charge Flame Efficiency" }, { "223", "Air Dash" }, { "224", "Ultra Soul Link" },
        { "225", "Charge Dash" }, { "226", "Water Breath" }, { "227", "Soul Link Efficiency" },
        { "228", "Triple Jump" }, { "229", "Ultra Defense" }, { "230", "Remove Quick Flame" },
        { "231", "Remove Spark Flame" }, { "232", "Remove Charge Flame Burn" }, { "233", "Remove Split Flame" },
        { "234", "Remove Ultra Light Burst" }, { "235", "Remove Cinder Flame" }, { "236", "Remove Ultra Stomp" },
        { "237", "Remove Rapid Flame" }, { "238", "Remove Charge Flame Blast" }, { "239", "Remove Ultra Split Flame" },
        { "240", "Remove Spirit Magnet" }, { "241", "Remove Drop Efficiency" }, { "242", "Remove Health Efficiency" },
        { "243", "Remove Ultra Spirit Magnet" }, { "244", "Remove Energy Efficiency" }, { "245", "Remove Spirit Efficiency" },
        { "246", "Remove Spirit Potency" }, { "247", "Remove Health Regen (Ability)" }, { "248", "Remove Energy Regen (Ability)" },
        { "249", "Remove Sense" }, { "250", "Remove Rekindle" }, { "251", "Remove Regroup" },
        { "252", "Remove Charge Flame Efficiency" }, { "253", "Remove Air Dash" }, { "254", "Remove Ultra Soul Link" },
        { "255", "Remove Charge Dash" }, { "256", "Remove Water Breath" }, { "257", "Remove Soul Link Efficiency" },
        { "258", "Remove Triple Jump" }, { "259", "Remove Ultra Defense" }, { "300", "Glades Pool Keystone" },
        { "301", "Lower Spirit Caverns Keystone" }, { "302", "Grotto Keystone" }, { "303", "Swamp Keystone" },
        { "304", "Upper Spirit Caverns Keystone" }, { "305", "Lower Ginso Keystone" }, { "306", "Upper Ginso Keystone" },
        { "307", "Misty Keystone" }, { "308", "Forlorn Keystone" }, { "309", "Lower Sorrow Keystone" },
        { "310", "Mid Sorrow Keystone" }, { "311", "Upper Sorrow Keystone" }, { "313", "Glades Pool Door Hint" },
        { "314", "Lower Spirit Caverns Door Hint" }, { "315", "Grotto Door Hint" }, { "316", "Swamp Door Hint" },
        { "317", "Upper Spirit Caverns Door Hint" }, { "318", "Lower Ginso Door Hint" }, { "319", "Upper Ginso Door Hint" },
        { "320", "Misty Door Hint" }, { "321", "Forlorn Door Hint" }, { "322", "Lower Sorrow Door Hint" },
        { "323", "Mid Sorrow Door Hint" }, { "324", "Upper Sorrow Door Hint" }, { "410", "Enhanced Spirit Flame" },
        { "411", "Enhanced Wall Jump" }, { "412", "Enhanced Charge Flame" }, { "413", "Enhanced Double Jump" },
        { "414", "Enhanced Bash" }, { "415", "Enhanced Stomp" }, { "416", "Enhanced Glide" },
        { "417", "Enhanced Climb" }, { "418", "Enhanced Charge Jump" }, { "419", "Enhanced Dash" },
        { "420", "Enhanced Grenade" }, { "422", "Enhanced Clean Water" }, { "900", "Wall Jump Tree" },
        { "901", "Charge Flame Tree" }, { "902", "Double Jump Tree" }, { "903", "Bash Tree" },
        { "904", "Stomp Tree" }, { "905", "Glide Tree" }, { "906", "Climb Tree" },
        { "907", "Charge Jump Tree" }, { "908", "Grenade Tree" }, { "909", "Dash Tree" },
        { "911", "Glades Relic" }, { "912", "Grove Relic" }, { "913", "Grotto Relic" },
        { "914", "Blackroot Relic" }, { "915", "Swamp Relic" }, { "916", "Ginso Relic" },
        { "917", "Valley Relic" }, { "918", "Misty Relic" }, { "919", "Forlorn Relic" },
        { "920", "Sorrow Relic" }, { "921", "Horu Relic" }, { "1100", "Enable Frag-Sense variant" }
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
        { 4087, "Warp to Credits" },
    };

    // RB 40-49 take a skill away. A log wants Name's "Remove Bash"; the
    // player wants the red "@Bash Lost!!@" for the same id.
    private static readonly HashSet<string> SkillLossIds = new HashSet<string> {
        "40", "41", "42", "43", "44", "45", "46", "47", "48", "49"
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

    /// <summary>Multipickup value -> its parts. "//" is a literal slash, and an
    /// odd trailing piece is dropped, matching RandomizerAction.Decompose.</summary>
    private static List<string[]> MultiParts(string value) {
        var parts = new List<string[]>();
        string first = null;
        var cur = new StringBuilder();
        for (var i = 0; i < (value ?? "").Length; i++) {
            if (value[i] != '/') {
                cur.Append(value[i]);
            } else if (i < value.Length - 1 && value[i + 1] == '/') {
                cur.Append('/');
                i++;
            } else if (first == null) {
                first = cur.ToString();
                cur.Length = 0;
            } else {
                parts.Add(new[] { first, cur.ToString() });
                first = null;
                cur.Length = 0;
            }
        }

        if (first != null) {
            parts.Add(new[] { first, cur.ToString() });
        }

        return parts;
    }

    /// <summary>The one place a code and an id become something a player reads.</summary>
    public static string Name(string code, string id) {
        // a negative id removes one instead of granting it; "Remove X" is the
        // wording the server and the RB table already use
        int signed;
        if (!string.IsNullOrEmpty(id) && id[0] == '-' && int.TryParse(id, out signed) && signed < 0) {
            return "Remove " + Name(code, (-signed).ToString());
        }

        switch (code) {
            case "MU":
            case "RP": {
                var names = MultiParts(id).Select(p => Name(p[0], p[1])).ToArray();
                var joined = string.Join(", ", names);
                return code == "RP" ? "Repeatable: " + joined : joined;
            }

            case "SH":
                return "Message: " + id;
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
                if (UpgradeNames.ContainsKey(id)) {
                    return UpgradeNames[id];
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
        // "X Lost!" is what the player wants told to them as it happens; a log
        // or a map wants Name's third-person "Remove X" instead
        int signed;
        if (!string.IsNullOrEmpty(id) && id[0] == '-' && int.TryParse(id, out signed) && signed < 0) {
            return Name(code, (-signed).ToString()) + " Lost!";
        }

        if (code == "RB" && SkillLossIds.Contains(id)) {
            var skill = Name(code, id);
            return "@" + (skill.StartsWith("Remove ") ? skill.Substring(7) : skill) + " Lost!!@";
        }

        if (code == "TP") {
            var color = ColorOf(Name(code, id));
            return color + id + " teleporter activated" + color;
        }

        return ColorWrap(Name(code, id));
    }

    public static string ColorWrap(string input) {
        return ColorWrapAs(input, input);
    }

    // the colour belongs to the item; what is written may be shorter than its name
    public static string ColorWrapAs(string name, string shown) {
        var color = ColorOf(name);
        return color + shown + color;
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
}

using System;
using System.Collections.Generic;
using Game;

// Multiworld client logic: the owner-side half of the slot-bitfield grant
// transport. Our seed's manifest lines (pseudo-locations -2..-257) describe
// what each of our 256 slots contains and whose world it sits in; the tick
// response's field 6 carries which of our slots other players have found.
// We grant ourselves the difference.
//
// Granted-slot bookkeeping lives in ordinary save items, which roll back on
// death/reload: a death after a grant reverts the item and the bookkeeping
// together, and the next tick simply re-grants. This is the whole design --
// see MULTIWORLD_NOTES.md (server repo era) for why the alternatives lose
// items to rollbacks.
public static class RandomizerMW
{
    // save item ids 940-947 hold the granted-slots bitfields (8 x 32 bits).
    // NOTE: must stay OUTSIDE 1500-1599, which RandomizerInventory preserves
    // through death/reload -- granted bits have to roll back with the save.
    public const int GrantedSlotsBase = 940;

    public class ManifestEntry
    {
        public int Finder;
        public string Code;
        public string Id;
        public string Zone;
    }

    public static Dictionary<int, ManifestEntry> Manifest = new Dictionary<int, ManifestEntry>();

    private static HashSet<int> warnedSlots = new HashSet<int>();

    public static void Reset()
    {
        Manifest.Clear();
        warnedSlots.Clear();
    }

    public static bool IsManifestLine(int coords, string code)
    {
        return code == "MW" && coords <= -2 && coords >= -257;
    }

    // manifest line: <-(slot+2)>|MW|<finder>,<code>,<id>|<zone>
    // (id may itself contain commas, e.g. TW warps, so split at most twice)
    public static void AddManifestEntry(int coords, string value, string zone)
    {
        try
        {
            int slot = -coords - 2;
            string[] parts = value.Split(new char[] { ',' }, 3);
            ManifestEntry entry = new ManifestEntry();
            entry.Finder = int.Parse(parts[0]);
            entry.Code = parts[1];
            entry.Id = parts[2];
            entry.Zone = zone;
            Manifest[slot] = entry;

            // our dungeon keys living in someone else's world still get
            // clues: the manifest knows whose world and which zone
            if (Randomizer.CluesMode && entry.Code == "EV")
            {
                int evId;
                if (int.TryParse(entry.Id, out evId) && evId % 2 == 0)
                    RandomizerClues.AddClue($"P{entry.Finder} {zone}", evId / 2);
            }
        }
        catch (Exception e)
        {
            Randomizer.LogError($"MW.AddManifestEntry({coords}, {value}): {e.Message}");
        }
    }

    // grants beyond this in one tick get one grouped summary instead of a
    // message per item (a release can dump dozens of slots at once)
    public const int BatchMessageThreshold = 3;

    // tick field 6: 8 ";"-joined 32-bit uints. Returns true if anything was
    // granted, so the caller can refresh logic.
    public static bool OnSlotsField(string field)
    {
        bool granted = false;
        try
        {
            if (string.IsNullOrEmpty(field) || !Characters.Sein || Characters.Sein.Inventory == null)
                return false;
            string[] parts = field.Split(';');

            // first pass: which grantable slots are new this tick?
            List<int> pending = new List<int>();
            uint[] serverFields = new uint[8];
            for (int i = 0; i < 8 && i < parts.Length; i++)
            {
                if (!uint.TryParse(parts[i], out serverFields[i]))
                    continue;
                uint local = AsUint(Characters.Sein.Inventory.GetRandomizerItem(GrantedSlotsBase + i));
                uint diff = serverFields[i] & ~local;
                for (int bit = 0; bit < 32 && diff != 0; bit++)
                    if ((diff & (1u << bit)) != 0)
                        pending.Add(i * 32 + bit);
            }
            if (pending.Count == 0)
                return false;

            bool batch = pending.Count > BatchMessageThreshold;
            List<ManifestEntry> batched = new List<ManifestEntry>();
            foreach (int slot in pending)
            {
                if (!GrantSlot(slot, batch, batched))
                    continue;
                int i = slot / 32;
                uint local = AsUint(Characters.Sein.Inventory.GetRandomizerItem(GrantedSlotsBase + i));
                Characters.Sein.Inventory.SetRandomizerItem(GrantedSlotsBase + i, AsInt(local | (1u << (slot % 32))));
                granted = true;
            }
            if (batched.Count > 0)
                ShowBatchMessage(batched);
        }
        catch (Exception e)
        {
            Randomizer.LogError("MW.OnSlotsField: " + e.Message);
        }
        return granted;
    }

    private static bool GrantSlot(int slot, bool batch, List<ManifestEntry> batched)
    {
        if (!Manifest.ContainsKey(slot))
        {
            if (!warnedSlots.Contains(slot))
            {
                warnedSlots.Add(slot);
                Randomizer.LogError($"MW: server reports slot {slot} found, but this seed has no manifest entry for it. Wrong or outdated seed file?");
            }
            return false;
        }
        ManifestEntry entry = Manifest[slot];
        if (batch)
        {
            // squelch per-item messages; ShowBatchMessage summarizes after
            RandomizerSwitch.SilentMode = true;
            try
            {
                RandomizerSwitch.GivePickup(new RandomizerAction(entry.Code, entry.Id), 0, false);
            }
            finally
            {
                RandomizerSwitch.SilentMode = false;
            }
            batched.Add(entry);
        }
        else
        {
            // one combined line: "[pickup] from Player N"
            RandomizerSwitch.MessageSuffix = $" from Player {entry.Finder}";
            try
            {
                RandomizerSwitch.GivePickup(new RandomizerAction(entry.Code, entry.Id), 0, false);
            }
            finally
            {
                RandomizerSwitch.MessageSuffix = null;
            }
        }
        return true;
    }

    private static Dictionary<string, string> SkillNames = new Dictionary<string, string>() {
        {"0", "Bash"}, {"2", "Charge Flame"}, {"3", "Wall Jump"}, {"4", "Stomp"}, {"5", "Double Jump"},
        {"8", "Charge Jump"}, {"12", "Climb"}, {"14", "Glide"}, {"50", "Dash"}, {"51", "Grenade"}, {"15", "Spirit Flame"}
    };
    private static Dictionary<string, string> EventNames = new Dictionary<string, string>() {
        {"0", "Water Vein"}, {"1", "Clean Water"}, {"2", "Gumon Seal"}, {"3", "Wind Restored"}, {"4", "Sunstone"}, {"5", "Warmth Returned"}
    };

    private static string Counted(int n, string singular, string plural, string wrap = "")
    {
        return $"{wrap}{n}{(n == 1 ? singular : plural)}";
    }

    private HashSet<string> blueStuff = new HashSet<string>() {"Water Vein", "Ginso Teleporter", "Clean Water"};
    private HashSet<string> orangeStuff = new HashSet<string>() {"Gumon Seal", "Forlorn Teleporter", "Wind Restored"};
    private HashSet<string> redStuff = new HashSet<string>() {"Sunstone", "Horu Teleporter", "Warmth Returned"};

    private static string colorWrap(string input) {
        if SkillNames.ContainsValue(input) return $"${input}$"; // skill names are green
        if blueStuff.Contains(input) return $"*{input}*";       // blue stuff is blue
        if orangeStuff.Contains(input) return $"#{input}#";     // orange stuff is orange
        if redStuff.Contains(input) return $"@{input}@";        // red stuff is red
        return input;                                           // this could have been a poem
    }

    // skills, then world events (+ shards/frags), then teleporters/warps, then a counts line
    private static void ShowBatchMessage(List<ManifestEntry> entries)
    {
        try
        {
            List<string> skills = new List<string>();
            List<string> events = new List<string>();
            List<string> travel = new List<string>();
            int hc = 0, ec = 0, ac = 0, ks = 0, ms = 0, exp = 0, rb = 0, wvs = 0, gss = 0, sss = 0, wfg = 0, other = 0;
            HashSet<int> finders = new HashSet<int>();
            foreach (ManifestEntry entry in entries)
            {
                finders.Add(entry.Finder);
                switch (entry.Code)
                {
                    case "SK":
                        skills.Add(colorWrap(SkillNames.ContainsKey(entry.Id) ? SkillNames[entry.Id] : "Unknown Skill " + entry.Id));
                        break;
                    case "EV":
                        events.Add(colorWrap(EventNames.ContainsKey(entry.Id) ? EventNames[entry.Id] : "Unknown Event " + entry.Id));
                        break;
                    case "TP":
                        travel.Add(colorWrap(entry.Id + " Teleporter"));
                        break;
                    case "TW":
                        travel.Add(entry.Id.Split(',')[0]);
                        break;
                    case "HC": hc++; break;
                    case "EC": ec++; break;
                    case "AC": ac++; break;
                    case "KS": ks++; break;
                    case "MS": ms++; break;
                    case "EX":
                        int val;
                        if (int.TryParse(entry.Id, out val))
                            exp += val;
                        break;
                    case "RB": 
                        if(entity.Id == 17)
                            wvs++;
                        else if(entity.Id == 19)
                            gss++;
                        else if(entity.Id == 21)
                            sss++;
                        else if(entity.Id == 28)
                            wfg++;
                        else
                            rb++; 
                        break;
                    default: other++; break;
                }
            }
            if(wvs > 0) events.Add(Counted(wvs, "Water Vein Shard", "Water Vein Shards", "*"));
            if(gss > 0) events.Add(Counted(gss, "Gumon Seal Shard", "Gumon Seal Shards", "#"));
            if(sss > 0) events.Add(Counted(sss, "Sunstone Shard", "Sunstone Shards", "@"));
            if(wfg > 0) events.Add(Counted(wfg, "Warmth Fragment", "Warmth Fragments", "@"));
            List<string> lines = new List<string>();
            if (skills.Count > 0)
                lines.Add(string.Join(", ", skills.ToArray()));
            if (events.Count > 0)
                lines.Add(string.Join(", ", events.ToArray()));
            if (travel.Count > 0)
                lines.Add(string.Join(", ", travel.ToArray()));
            List<string> counts = new List<string>();
            if (hc > 0) counts.Add(Counted(hc, "Health Cell", "Health Cells"));
            if (ec > 0) counts.Add(Counted(ec, "Energy Cell", "Energy Cells"));
            if (ac > 0) counts.Add(Counted(ac, "Ability Cell", "Ability Cells"));
            if (ks > 0) counts.Add(Counted(ks, "Keystone", "Keystones"));
            if (ms > 0) counts.Add(Counted(ms, "Mapstone", "Mapstones"));
            if (rb > 0) counts.Add(Counted(rb, "Bonus Pickup", "Bonus Pickups"));
            if (other > 0) counts.Add(Counted(other, "other item", "other items"));
            if (exp > 0) counts.Add(exp.ToString() + " Spirit Light");
            if (counts.Count > 0)
                lines.Add(string.Join(", ", counts.ToArray()));
            if (lines.Count > 0) // TODO: add the player(s) from finders to this above the linebreak
                RandomizerSwitch.PickupMessage("Received:\n" + string.Join("\n", lines.ToArray()), 480);
        }
        catch (Exception e)
        {
            Randomizer.LogError("MW.ShowBatchMessage: " + e.Message);
        }
    }

    public static uint AsUint(int value)
    {
        return BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
    }

    public static int AsInt(uint value)
    {
        return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
    }
}

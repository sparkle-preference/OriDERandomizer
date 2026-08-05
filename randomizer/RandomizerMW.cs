using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        public int Slot;
        public int Finder;
        public string Code;
        public string Id;
        public string Zone;
    }

    public static Dictionary<int, ManifestEntry> Manifest = new Dictionary<int, ManifestEntry>();

    private static HashSet<int> warnedSlots = new HashSet<int>();

    // tick field 7: ";"-joined "{pid}.{name}" pairs (name for unclaimed
    // players is just "Player N")
    public static Dictionary<int, string> PlayerNames = new Dictionary<int, string>();

    // --- Archipelago ---
    // coord -> {recipient token, bare item name}, from a reserved line's 5th
    // field. Empty until the seed is re-downloaded with the room connected.
    public static Dictionary<int, string[]> ApItems = new Dictionary<int, string[]>();

    // slot -> who found it, from the apfrom tick signal. "" means you did.
    public static Dictionary<int, string> SlotSenders = new Dictionary<int, string>();

    // only AP grants straddle ticks; native multiworld grants immediately
    public static bool ApGrants = false;

    public static void Reset()
    {
        Manifest.Clear();
        warnedSlots.Clear();
        PlayerNames.Clear();
        ApItems.Clear();
        SlotSenders.Clear();
        ApGrants = false;
        pendingSlots.Clear();
        windowTicks = 0;
    }

    public static string PlayerName(int pid, bool shortName = false)
    {
        if (PlayerNames.TryGetValue(pid, out var name)) {
            return name;
        }

        return shortName ? $"P{pid}" : $"Player {pid}";
    }

    private static readonly Regex NameRef = new Regex(@"(?<![A-Za-z0-9])P(\d+)");
    private static readonly Regex PidToken = new Regex(@"^P(\d+)$");

    // an AP token is either "P<pid>" -- a world of this same game, whose
    // real name arrives on the tick -- or a room name to print verbatim
    public static string ApName(string token)
    {
        Match m = PidToken.Match(token ?? "");
        return m.Success ? PlayerName(int.Parse(m.Groups[1].Value)) : token;
    }

    public static int OwnPid()
    {
        string[] parts = (Randomizer.SyncId ?? "").Split('.');
        int pid;
        return parts.Length > 1 && int.TryParse(parts[1], out pid) ? pid : 0;
    }

    public static bool IsSelf(string token)
    {
        return token == "P" + OwnPid();
    }

    // reserved AP line: <coord>|MW|<shadow>,<slot>,<label>|<zone>|<to>;<item>
    public static void AddApLine(int coords, string apField)
    {
        if (string.IsNullOrEmpty(apField))
            return;
        string[] parts = apField.Split(new char[] { ';' }, 2);
        if (parts.Length == 2)
            ApItems[coords] = parts;
    }

    // signal payload: "<slot>=<sender>;<slot>=<sender>", sender "" = you
    public static void OnApFromSignal(string payload)
    {
        try
        {
            ApGrants = true;
            foreach (string pair in payload.Split(';'))
            {
                int eq = pair.IndexOf('=');
                int slot;
                if (eq > 0 && int.TryParse(pair.Substring(0, eq), out slot))
                    SlotSenders[slot] = pair.Substring(eq + 1);
            }
        }
        catch (Exception e)
        {
            Randomizer.LogError("MW.OnApFromSignal: " + e.Message);
        }
    }

    // who to name on a grant: the apfrom signal when Archipelago sent it,
    // the manifest's finder otherwise (plain multiworld). "" = yourself.
    private static string SenderFor(ManifestEntry entry)
    {
        string token;
        if (SlotSenders.TryGetValue(entry.Slot, out token))
            return token == "" ? "" : ApName(token);
        return PlayerName(entry.Finder);
    }

    // display-time substitution for clue/hint strings baked as "P<n>" at seed
    // parse (names arrive later, via the tick).
    public static string ResolveNames(string text)
    {
        try
        {
            return NameRef.Replace(text, m => int.TryParse(m.Groups[1].Value, out var pid) ? PlayerName(pid, true) + "'s" : m.Value);
        }
        catch (Exception e)
        {
            Randomizer.LogError("MW.ResolveNames: " + e.Message);
            return text;
        }
    }

    public static void OnNamesField(string field)
    {
        try
        {
            foreach (string pair in field.Split(';'))
            {
                int dot = pair.IndexOf('.');
                int pid;
                if (dot > 0 && int.TryParse(pair.Substring(0, dot), out pid) && pair.Length > dot + 1)
                    PlayerNames[pid] = pair.Substring(dot + 1);
            }
        }
        catch (Exception e)
        {
            Randomizer.LogError("MW.OnNamesField: " + e.Message);
        }
    }

    public static bool IsManifestLine(int coords, string code)
    {
        return code == "MW" && coords <= -2 && coords >= -257;
    }

    // has the item at this manifest pseudo-location already been granted to us?
    public static bool ManifestLocGranted(int coords)
    {
        int slot = -coords - 2;
        if (slot < 0 || slot > 255 || !Characters.Sein)
            return false;
        uint local = (uint)Characters.Sein.Inventory.GetRandomizerItem(GrantedSlotsBase + slot / 32);
        return (local & (1u << (slot % 32))) != 0;
    }

    // manifest line: <-(slot+2)>|MW|<finder>,<code>,<id>|<zone>[|<holder>]
    // (id may itself contain commas, e.g. TW warps, so split at most twice)
    public static void AddManifestEntry(int coords, string value, string zone, string holder = null)
    {
        try
        {
            int slot = -coords - 2;
            string[] parts = value.Split(new char[] { ',' }, 3);
            ManifestEntry entry = new ManifestEntry();
            entry.Slot = slot;
            entry.Finder = int.Parse(parts[0]);
            entry.Code = parts[1];
            entry.Id = parts[2];
            entry.Zone = zone;
            Manifest[slot] = entry;

            // whose world holds this. Archipelago works it out at download
            // time and puts it in field 5, because its shadow finder names
            // nobody; plain multiworld's finder is the answer already.
            string who = string.IsNullOrEmpty(holder) ? $"P{entry.Finder}" : holder;
            string clue = string.IsNullOrEmpty(zone) ? who : $"{who} {zone}";

            // our dungeon keys living in someone else's world still get
            // clues: the manifest knows whose world and which zone
            if (Randomizer.CluesMode && entry.Code == "EV")
            {
                int evId;
                if (int.TryParse(entry.Id, out evId) && evId % 2 == 0)
                    RandomizerClues.AddClue(clue, evId / 2);
            }

            // same for keysanity door keys; the clue's coords are the manifest
            // pseudo-location, resolved as found via the granted-slot bits
            if (Randomizer.Keysanity.IsActive && entry.Code == "RB")
            {
                int rbId;
                if (int.TryParse(entry.Id, out rbId))
                    Randomizer.Keysanity.AddClue(rbId, coords, clue);
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

    // Archipelago hands one batch over as several ReceivedItems (a room's
    // collect runs once per source world), and the bridge only coalesces
    // what arrives inside its own window -- the rest straddles the tick
    // boundary. One quiet tick catches those; anything above one item then
    // summarises instead of printing a message each.
    public const int ApGrantWindowTicks = 1;
    public const int ApBatchMessageThreshold = 1;

    private static List<int> pendingSlots = new List<int>();
    private static int windowTicks = 0;

    // tick field 6: 8 ";"-joined 32-bit uints. Returns true if anything was
    // granted, so the caller can refresh logic.
    public static bool OnSlotsField(string field)
    {
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
                uint local = (uint)Characters.Sein.Inventory.GetRandomizerItem(GrantedSlotsBase + i);
                uint diff = serverFields[i] & ~local;
                for (int bit = 0; bit < 32 && diff != 0; bit++)
                    if ((diff & (1u << bit)) != 0)
                        pending.Add(i * 32 + bit);
            }

            if (!ApGrants)
                return pending.Count > 0 && Grant(pending, BatchMessageThreshold);

            // pending is a level, not an edge: it stays set until we grant, so
            // only a NEW slot may re-arm the window
            bool grew = false;
            foreach (int slot in pending)
                if (!pendingSlots.Contains(slot))
                {
                    pendingSlots.Add(slot);
                    grew = true;
                }
            if (grew)
            {
                windowTicks = ApGrantWindowTicks;   // more may still be coming
                return false;
            }
            if (pendingSlots.Count == 0 || --windowTicks > 0)
                return false;
            List<int> ready = pendingSlots;
            pendingSlots = new List<int>();
            return Grant(ready, ApBatchMessageThreshold);
        }
        catch (Exception e)
        {
            Randomizer.LogError("MW.OnSlotsField: " + e.Message);
        }
        return false;
    }

    private static bool Grant(List<int> slots, int threshold)
    {
        bool granted = false;
        bool batch = slots.Count > threshold;
        // grants during the credits roll happen silently
        bool silent = Randomizer.CreditsActive;
        List<ManifestEntry> batched = new List<ManifestEntry>();
        foreach (int slot in slots)
        {
            if (!GrantSlot(slot, batch || silent, batched))
                continue;
            int i = slot / 32;
            uint local = (uint)Characters.Sein.Inventory.GetRandomizerItem(GrantedSlotsBase + i);
            Characters.Sein.Inventory.SetRandomizerItem(GrantedSlotsBase + i, (int)(local | (1u << (slot % 32))));
            granted = true;
        }
        if (batched.Count > 0 && !silent)
            ShowBatchMessage(batched);
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
        int coords = -slot - 2;
        if (batch)
        {
            // squelch per-item messages; ShowBatchMessage summarizes after
            RandomizerSwitch.SilentMode = true;
            try
            {
                RandomizerSwitch.GivePickup(new RandomizerAction(entry.Code, entry.Id), coords, false);
            }
            finally
            {
                RandomizerSwitch.SilentMode = false;
            }
            batched.Add(entry);
        }
        else
        {
            // one combined line: "[pickup] from [player]", or just the
            // pickup when Archipelago handed back something we found
            string sender = SenderFor(entry);
            RandomizerSwitch.MessageSuffix = sender == "" ? null : $" from {sender}";
            try
            {
                RandomizerSwitch.GivePickup(new RandomizerAction(entry.Code, entry.Id), coords, false);
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
        return $"{wrap}{n} {(n == 1 ? singular : plural)}{wrap}";
    }

    private static HashSet<string> blueStuff = new HashSet<string>() {"Water Vein", "Ginso Teleporter", "Clean Water"};
    private static HashSet<string> orangeStuff = new HashSet<string>() {"Gumon Seal", "Forlorn Teleporter", "Wind Restored"};
    private static HashSet<string> redStuff = new HashSet<string>() {"Sunstone", "Horu Teleporter", "Warmth Returned"};

    public static string ColorWrap(string input) {
        if(SkillNames.ContainsValue(input)) return $"${input}$"; // skill names are green
        if(blueStuff.Contains(input)) return $"*{input}*";       // blue stuff is blue
        if(orangeStuff.Contains(input)) return $"#{input}#";     // orange stuff is orange
        if(redStuff.Contains(input)) return $"@{input}@";        // red stuff is red
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
            HashSet<string> finders = new HashSet<string>();
            foreach (ManifestEntry entry in entries)
            {
                string sender = SenderFor(entry);
                if (sender != "")
                    finders.Add(sender);
                switch (entry.Code)
                {
                    case "SK":
                        skills.Add(ColorWrap(SkillNames.ContainsKey(entry.Id) ? SkillNames[entry.Id] : "Unknown Skill " + entry.Id));
                        break;
                    case "EV":
                        events.Add(ColorWrap(EventNames.ContainsKey(entry.Id) ? EventNames[entry.Id] : "Unknown Event " + entry.Id));
                        break;
                    case "TP":
                        travel.Add(ColorWrap(entry.Id + " Teleporter"));
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
                        if(entry.Id == "17")
                            wvs++;
                        else if(entry.Id == "19")
                            gss++;
                        else if(entry.Id == "21")
                            sss++;
                        else if(entry.Id == "28")
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
            // TODO: maybe get their names even though it'll be so much work (probs a refactor on name handling in general misery emoji)
            if (rb > 0) counts.Add(Counted(rb, "Bonus Pickup", "Bonus Pickups"));
            if (hc > 0) counts.Add(Counted(hc, "Health Cell", "Health Cells"));
            if (ec > 0) counts.Add(Counted(ec, "Energy Cell", "Energy Cells"));
            if (ac > 0) counts.Add(Counted(ac, "Ability Cell", "Ability Cells"));
            if (ks > 0) counts.Add(Counted(ks, "Keystone", "Keystones"));
            if (ms > 0) counts.Add(Counted(ms, "Mapstone", "Mapstones"));
            if (other > 0) counts.Add(Counted(other, "other item", "other items"));
            if (exp > 0) counts.Add(exp.ToString() + " Spirit Light");
            if (counts.Count > 0)
                lines.Add(string.Join(", ", counts.ToArray()));
            if (lines.Count > 0)
            {
                List<string> finderNames = new List<string>(finders);
                finderNames.Sort();
                // no names at all means Archipelago handed back only things
                // we found ourselves
                string header = finderNames.Count > 0
                    ? $"Received from {string.Join(", ", finderNames.ToArray())}:\n"
                    : "Received:\n";
                RandomizerSwitch.PickupMessage(header + string.Join("\n", lines.ToArray()), 480);
            }
        }
        catch (Exception e)
        {
            Randomizer.LogError("MW.ShowBatchMessage: " + e.Message);
        }
    }
}

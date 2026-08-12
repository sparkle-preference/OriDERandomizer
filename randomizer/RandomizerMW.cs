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
public static class RandomizerMW {
    // save item ids 940-947 hold the granted-slots bitfields (8 x 32 bits).
    // NOTE: must stay OUTSIDE 1500-1599, which RandomizerInventory preserves
    // through death/reload -- granted bits have to roll back with the save.
    public const int GrantedSlotsBase = 940;

    public class ManifestEntry {
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

    // slot -> the hint Archipelago sold us for it, from tick field 8. Only
    // ever answers to our own requests, so it is empty on every other seed.
    public static Dictionary<int, string> ApHints = new Dictionary<int, string>();

    // only AP grants straddle ticks; native multiworld grants immediately
    public static bool ApGrants;

    public static void Reset() {
        Manifest.Clear();
        warnedSlots.Clear();
        PlayerNames.Clear();
        ApItems.Clear();
        ApSelfSlots.Clear();
        SlotSenders.Clear();
        ApHints.Clear();
        ApGrants = false;
        pendingSlots.Clear();
        windowTicks = 0;
        hintsAsked.Clear();
        hintResendTicks = 0;
    }

    public static string PlayerName(int pid, bool shortName = false) {
        if (PlayerNames.TryGetValue(pid, out var name)) {
            return name;
        }

        return shortName ? $"P{pid}" : $"Player {pid}";
    }

    private static readonly Regex NameRef = new Regex(@"(?<![A-Za-z0-9])P(\d+)");
    private static readonly Regex PidToken = new Regex(@"^P(\d+)$");

    // an AP token is either "P<pid>" -- a world of this same game, whose
    // real name arrives on the tick -- or a room name to print verbatim
    public static string ApName(string token) {
        var m = PidToken.Match(token ?? "");
        return m.Success ? PlayerName(int.Parse(m.Groups[1].Value)) : token;
    }

    public static int OwnPid() {
        var parts = (Randomizer.SyncId ?? "").Split('.');
        return parts.Length > 1 && int.TryParse(parts[1], out var pid) ? pid : 0;
    }

    public static bool IsSelf(string token) {
        return token == "P" + OwnPid();
    }

    // reserved AP line:
    //   <coord>|MW|<shadow>,<slot>,<label>|<zone>|<to>;<item>[|<own slot>]
    // field 6 is present only when the item is ours, and names the manifest
    // slot the bridge will fill for this location's check (the 4.2.12
    // pairing), so contact can grant it without the room round trip.
    public static void AddApLine(int coords, string apField, string ownSlot = null) {
        if (string.IsNullOrEmpty(apField)) {
            return;
        }

        var parts = apField.Split(new[] { ';' }, 2);
        if (parts.Length != 2) {
            return;
        }

        ApItems[coords] = parts;
        if (!string.IsNullOrEmpty(ownSlot) && int.TryParse(ownSlot, out var slot)) {
            ApSelfSlots[coords] = slot;
        }
    }

    // coord -> our own manifest slot, for reserved locations holding our item
    public static Dictionary<int, int> ApSelfSlots = new Dictionary<int, int>();

    /// <summary>
    /// Contact with a reserved location holding our own item. Grants it now
    /// rather than waiting for the room to hand it back: the bridge fills
    /// this check into exactly the promised slot, so the granted bit doubles
    /// as the location's spent marker — server-backed, redelivered by the
    /// tick after any rollback (death, quit-to-menu, restart).
    /// Returns false when there is nothing to grant here.
    /// </summary>
    public static bool GrantSelfItem(int coords) {
        if (!ApSelfSlots.TryGetValue(coords, out var slot) || !Manifest.ContainsKey(slot)) {
            return false;
        }

        if (SlotGranted(slot)) {
            // came back to it after a rollback: the item is already ours
            var name = ApItems.TryGetValue(coords, out var ap) ? ap[1] : "That";
            RandomizerSwitch.PickupMessage(ColorWrap(name) + " (already collected)");
            return true;
        }

        // "" is the apfrom token for "you found this yourself", so the grant
        // message carries no "from" suffix -- it reads as an ordinary pickup
        SlotSenders[slot] = "";
        Grant(new List<int> { slot }, ApBatchMessageThreshold);
        return true;
    }

    /// <summary>
    /// True when this location holds our own Archipelago item and that item is
    /// already in the save. Its coord bit rolls back on death, but the granted
    /// slot does not stay lost -- the server re-sets it -- so the map should
    /// treat the location as spent.
    /// </summary>
    public static bool SelfItemCollected(int coords) {
        return ApSelfSlots.TryGetValue(coords, out var slot) && SlotGranted(slot);
    }

    // has this manifest slot already been granted into the save?
    public static bool SlotGranted(int slot) {
        if (slot < 0 || slot > 255 || !Characters.Sein) {
            return false;
        }

        var local = (uint)Characters.Sein.Inventory.GetRandomizerItem(GrantedSlotsBase + slot / 32);
        return (local & (1u << (slot % 32))) != 0;
    }

    // signal payload: "<slot>=<sender>;<slot>=<sender>", sender "" = you
    public static void OnApFromSignal(string payload) {
        try {
            ApGrants = true;
            foreach (var pair in payload.Split(';')) {
                var eq = pair.IndexOf('=');
                if (eq > 0 && int.TryParse(pair.Substring(0, eq), out var slot)) {
                    SlotSenders[slot] = pair.Substring(eq + 1);
                }
            }
        } catch (Exception e) {
            Randomizer.LogError("MW.OnApFromSignal: " + e.Message);
        }
    }

    // who to name on a grant: the apfrom signal when Archipelago sent it,
    // the manifest's finder otherwise (plain multiworld). "" = yourself.
    private static string SenderFor(ManifestEntry entry) {
        if (SlotSenders.TryGetValue(entry.Slot, out var token)) {
            return token == "" ? "" : ApName(token);
        }

        return PlayerName(entry.Finder);
    }

    // display-time substitution for clue/hint strings baked as "P<n>" at seed
    // parse (names arrive later, via the tick).
    public static string ResolveNames(string text) {
        try {
            return NameRef.Replace(text, m => int.TryParse(m.Groups[1].Value, out var pid) ? PlayerName(pid, true) + "'s" : m.Value);
        } catch (Exception e) {
            Randomizer.LogError("MW.ResolveNames: " + e.Message);
            return text;
        }
    }

    // --- progressive Archipelago hints ---
    //
    // Ori reveals its clues as you play: dungeon keys at 3/6/9 trees, a
    // keysanity door hint when you touch the door, Stomp and Grenade when the
    // Forlorn escape ends. All three are save state only we can see, so we
    // name the slots we need (tick field aph) and the server buys the hint;
    // the answer arrives on field 8 and stands in for the baked clue.

    // manifest slots we have told the server about, and the countdown to
    // repeating that for the ones it has not answered
    private static HashSet<int> hintsAsked = new HashSet<int>();
    private static int hintResendTicks;
    public const int HintResendPeriod = 30;
    public const int MaxHintRequests = 8;

    // tick field 8: ";"-joined "<slot>=<text>"
    public static void OnApHintsField(string field) {
        try {
            foreach (var pair in field.Split(';')) {
                var eq = pair.IndexOf('=');
                if (eq > 0 && int.TryParse(pair.Substring(0, eq), out var slot)) {
                    ApHints[slot] = pair.Substring(eq + 1);
                }
            }
        } catch (Exception e) {
            Randomizer.LogError("MW.OnApHintsField: " + e.Message);
        }
    }

    // the bought hint for a manifest slot, or the clue baked at seed parse.
    // Every display site reads through here, so a seed with no AP hints shows
    // exactly what it always showed.
    public static string ApHintOr(int slot, string baked) {
        if (slot >= 0 && ApHints.TryGetValue(slot, out var text) && text != "") {
            return text;
        }

        return baked;
    }

    public static void WantHint(List<int> needed, int slot) {
        if (slot >= 0 && needed.Count < MaxHintRequests && !ApHints.ContainsKey(slot)
            && Manifest.ContainsKey(slot) && !needed.Contains(slot)) {
            needed.Add(slot);
        }
    }

    // "<slot>.<slot>" for the tick, or null when there is nothing to ask.
    // The needed set is a level (it survives death and Alt+L, because it is
    // derived from the save), so a newly revealed slot goes out at once and
    // an unanswered one is only repeated every HintResendPeriod ticks.
    public static string HintRequestField() {
        try {
            if (Randomizer.SyncMode != 5 || Manifest.Count == 0 || !Characters.Sein) {
                return null;
            }

            var needed = new List<int>();
            // the two bounded sets go first: keysanity alone can offer 40 rows
            // and would otherwise fill the budget before these are ever asked
            if (Randomizer.CluesMode) {
                RandomizerClues.WantHints(needed);
            }

            if (RandomizerBonus.ForlornEscapeHint()) {
                WantHint(needed, Randomizer.StompSlot);
                WantHint(needed, Randomizer.GrenadeSlot);
            }

            if (Randomizer.Keysanity.IsActive) {
                Randomizer.Keysanity.WantHints(needed);
            }

            if (needed.Count == 0) {
                hintsAsked.Clear();
                return null;
            }

            var grew = false;
            foreach (var slot in needed) {
                if (!hintsAsked.Contains(slot)) {
                    grew = true;
                }
            }

            if (!grew && --hintResendTicks > 0) {
                return null;
            }

            hintsAsked.Clear();
            hintsAsked.UnionWith(needed);
            hintResendTicks = HintResendPeriod;
            needed.Sort();
            return string.Join(".", needed.ConvertAll(slot => slot.ToString()).ToArray());
        } catch (Exception e) {
            Randomizer.LogError("MW.HintRequestField: " + e.Message);
            return null;
        }
    }

    public static void OnNamesField(string field) {
        try {
            foreach (var pair in field.Split(';')) {
                var dot = pair.IndexOf('.');
                if (dot > 0 && int.TryParse(pair.Substring(0, dot), out var pid) && pair.Length > dot + 1) {
                    PlayerNames[pid] = pair.Substring(dot + 1);
                }
            }
        } catch (Exception e) {
            Randomizer.LogError("MW.OnNamesField: " + e.Message);
        }
    }

    public static bool IsManifestLine(int coords, string code) {
        return code == "MW" && coords <= -2 && coords >= -257;
    }

    // has the item at this manifest pseudo-location already been granted to us?
    public static bool ManifestLocGranted(int coords) {
        return SlotGranted(-coords - 2);
    }

    // manifest line: <-(slot+2)>|MW|<finder>,<code>,<id>|<zone>[|<holder>]
    // (id may itself contain commas, e.g. TW warps, so split at most twice)
    public static void AddManifestEntry(int coords, string value, string zone, string holder = null) {
        try {
            var slot = -coords - 2;
            var parts = value.Split(new[] { ',' }, 3);
            var entry = new ManifestEntry();
            entry.Slot = slot;
            entry.Finder = int.Parse(parts[0]);
            entry.Code = parts[1];
            entry.Id = parts[2];
            entry.Zone = zone;
            Manifest[slot] = entry;

            // whose world holds this. Archipelago works it out at download
            // time and puts it in field 5, because its shadow finder names
            // nobody; plain multiworld's finder is the answer already.
            var who = string.IsNullOrEmpty(holder) ? $"P{entry.Finder}" : holder;
            var clue = string.IsNullOrEmpty(zone) ? who : $"{who} {zone}";

            // an exported warp still needs its logic node registered: the
            // seed-parse path that does that only sees plain TW lines
            if (entry.Code == "TW") {
                var warp = entry.Id.Split(',');
                if (warp.Length > 3 && !Randomizer.WarpLogicLocations.ContainsKey(warp[0])) {
                    Randomizer.WarpLogicLocations.Add(warp[0], warp[3]);
                }
            }

            // our dungeon keys living in someone else's world still get
            // clues: the manifest knows whose world and which zone
            if (Randomizer.CluesMode && entry.Code == "EV") {
                if (int.TryParse(entry.Id, out var evId) && evId % 2 == 0) {
                    RandomizerClues.AddClue(clue, evId / 2, slot);
                }
            }

            // the Forlorn escape names where Stomp and Grenade went, and an
            // exported one has no local SK line to read that from. The zone
            // only moves when field 5 answered it: plain multiworld keeps the
            // "MIA" it has always printed.
            if (entry.Code == "SK" && (entry.Id == "4" || entry.Id == "51")) {
                var stomp = entry.Id == "4";
                if (stomp) {
                    Randomizer.StompSlot = slot;
                } else {
                    Randomizer.GrenadeSlot = slot;
                }

                if (!string.IsNullOrEmpty(holder)) {
                    if (stomp) {
                        Randomizer.StompZone = clue;
                    } else {
                        Randomizer.GrenadeZone = clue;
                    }
                }
            }

            // same for keysanity door keys; the clue's coords are the manifest
            // pseudo-location, resolved as found via the granted-slot bits
            if (Randomizer.Keysanity.IsActive && entry.Code == "RB") {
                if (int.TryParse(entry.Id, out var rbId)) {
                    Randomizer.Keysanity.AddClue(rbId, coords, clue);
                }
            }
        } catch (Exception e) {
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
    private static int windowTicks;

    // tick field 6: 8 ";"-joined 32-bit uints. Returns true if anything was
    // granted, so the caller can refresh logic.
    public static bool OnSlotsField(string field) {
        try {
            if (string.IsNullOrEmpty(field) || !Characters.Sein || Characters.Sein.Inventory == null) {
                return false;
            }

            var parts = field.Split(';');

            // first pass: which grantable slots are new this tick?
            var pending = new List<int>();
            var serverFields = new uint[8];
            for (var i = 0; i < 8 && i < parts.Length; i++) {
                if (!uint.TryParse(parts[i], out serverFields[i])) {
                    continue;
                }

                var local = (uint)Characters.Sein.Inventory.GetRandomizerItem(GrantedSlotsBase + i);
                var diff = serverFields[i] & ~local;
                for (var bit = 0; bit < 32 && diff != 0; bit++) {
                    if ((diff & (1u << bit)) != 0) {
                        pending.Add(i * 32 + bit);
                    }
                }
            }

            if (!ApGrants) {
                return pending.Count > 0 && Grant(pending, BatchMessageThreshold);
            }

            // pending is a level, not an edge: it stays set until we grant, so
            // only a NEW slot may re-arm the window
            var grew = false;
            foreach (var slot in pending) {
                if (!pendingSlots.Contains(slot)) {
                    pendingSlots.Add(slot);
                    grew = true;
                }
            }

            if (grew) {
                windowTicks = ApGrantWindowTicks; // more may still be coming
                return false;
            }

            if (pendingSlots.Count == 0 || --windowTicks > 0) {
                return false;
            }

            var ready = pendingSlots;
            pendingSlots = new List<int>();
            return Grant(ready, ApBatchMessageThreshold);
        } catch (Exception e) {
            Randomizer.LogError("MW.OnSlotsField: " + e.Message);
        }

        return false;
    }

    private static bool Grant(List<int> slots, int threshold) {
        var granted = false;
        var batch = slots.Count > threshold;
        // grants during the credits roll happen silently
        var silent = Randomizer.CreditsActive;
        var batched = new List<ManifestEntry>();
        foreach (var slot in slots) {
            if (!GrantSlot(slot, batch || silent, batched)) {
                continue;
            }

            var i = slot / 32;
            var local = (uint)Characters.Sein.Inventory.GetRandomizerItem(GrantedSlotsBase + i);
            Characters.Sein.Inventory.SetRandomizerItem(GrantedSlotsBase + i, (int)(local | (1u << (slot % 32))));
            granted = true;
        }

        if (batched.Count > 0 && !silent) {
            ShowBatchMessage(batched);
        }

        return granted;
    }

    private static bool GrantSlot(int slot, bool batch, List<ManifestEntry> batched) {
        if (!Manifest.ContainsKey(slot)) {
            if (!warnedSlots.Contains(slot)) {
                warnedSlots.Add(slot);
                Randomizer.LogError($"MW: server reports slot {slot} found, but this seed has no manifest entry for it. Wrong or outdated seed file?");
            }

            return false;
        }

        var entry = Manifest[slot];
        var coords = -slot - 2;
        if (batch) {
            // squelch per-item messages; ShowBatchMessage summarizes after
            RandomizerSwitch.SilentMode = true;
            try {
                RandomizerSwitch.GivePickup(new RandomizerAction(entry.Code, entry.Id), coords, false);
            } finally {
                RandomizerSwitch.SilentMode = false;
            }

            batched.Add(entry);
        } else {
            // one combined line: "[pickup] from [player]", or just the
            // pickup when Archipelago handed back something we found
            var sender = SenderFor(entry);
            RandomizerSwitch.MessageSuffix = sender == "" ? null : $" from {sender}";
            try {
                RandomizerSwitch.GivePickup(new RandomizerAction(entry.Code, entry.Id), coords, false);
            } finally {
                RandomizerSwitch.MessageSuffix = null;
            }
        }

        return true;
    }

    private static Dictionary<string, string> SkillNames = new Dictionary<string, string> {
        { "0", "Bash" }, { "2", "Charge Flame" }, { "3", "Wall Jump" }, { "4", "Stomp" }, { "5", "Double Jump" },
        { "8", "Charge Jump" }, { "12", "Climb" }, { "14", "Glide" }, { "50", "Dash" }, { "51", "Grenade" }, { "15", "Spirit Flame" }
    };

    private static Dictionary<string, string> EventNames = new Dictionary<string, string> {
        { "0", "Water Vein" }, { "1", "Clean Water" }, { "2", "Gumon Seal" }, { "3", "Wind Restored" }, { "4", "Sunstone" }, { "5", "Warmth Returned" }
    };

    private static string Counted(int n, string singular, string plural, string wrap = "") {
        return $"{wrap}{n} {(n == 1 ? singular : plural)}{wrap}";
    }

    private static HashSet<string> blueStuff = new HashSet<string> { "Water Vein", "Ginso Teleporter", "Clean Water" };
    private static HashSet<string> orangeStuff = new HashSet<string> { "Gumon Seal", "Forlorn Teleporter", "Wind Restored" };
    private static HashSet<string> redStuff = new HashSet<string> { "Sunstone", "Horu Teleporter", "Warmth Returned" };

    public static string ColorWrap(string input) {
        if (SkillNames.ContainsValue(input)) {
            return $"${input}$"; // skill names are green
        }

        if (blueStuff.Contains(input)) {
            return $"*{input}*"; // blue stuff is blue
        }

        if (orangeStuff.Contains(input)) {
            return $"#{input}#"; // orange stuff is orange
        }

        if (redStuff.Contains(input)) {
            return $"@{input}@"; // red stuff is red
        }

        return input; // this could have been a poem
    }

    // skills, then world events (+ shards/frags), then teleporters/warps, then a counts line
    private static void ShowBatchMessage(List<ManifestEntry> entries) {
        try {
            var skills = new List<string>();
            var events = new List<string>();
            var travel = new List<string>();
            int hc = 0, ec = 0, ac = 0, ks = 0, ms = 0, exp = 0, rb = 0, wvs = 0, gss = 0, sss = 0, wfg = 0, other = 0;
            var finders = new HashSet<string>();
            var anySelf = false;
            foreach (var entry in entries) {
                var sender = SenderFor(entry);
                if (sender != "") {
                    finders.Add(sender);
                } else {
                    anySelf = true;
                }

                switch (entry.Code) {
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
                        if (int.TryParse(entry.Id, out var val)) {
                            exp += val;
                        }

                        break;
                    case "RB":
                        if (entry.Id == "17") {
                            wvs++;
                        } else if (entry.Id == "19") {
                            gss++;
                        } else if (entry.Id == "21") {
                            sss++;
                        } else if (entry.Id == "28") {
                            wfg++;
                        } else {
                            rb++;
                        }

                        break;
                    default: other++; break;
                }
            }

            if (wvs > 0) {
                events.Add(Counted(wvs, "Water Vein Shard", "Water Vein Shards", "*"));
            }

            if (gss > 0) {
                events.Add(Counted(gss, "Gumon Seal Shard", "Gumon Seal Shards", "#"));
            }

            if (sss > 0) {
                events.Add(Counted(sss, "Sunstone Shard", "Sunstone Shards", "@"));
            }

            if (wfg > 0) {
                events.Add(Counted(wfg, "Warmth Fragment", "Warmth Fragments", "@"));
            }

            var lines = new List<string>();
            if (skills.Count > 0) {
                lines.Add(string.Join(", ", skills.ToArray()));
            }

            if (events.Count > 0) {
                lines.Add(string.Join(", ", events.ToArray()));
            }

            if (travel.Count > 0) {
                lines.Add(string.Join(", ", travel.ToArray()));
            }

            var counts = new List<string>();
            // TODO: maybe get their names even though it'll be so much work (probs a refactor on name handling in general misery emoji)
            if (rb > 0) {
                counts.Add(Counted(rb, "Bonus Pickup", "Bonus Pickups"));
            }

            if (hc > 0) {
                counts.Add(Counted(hc, "Health Cell", "Health Cells"));
            }

            if (ec > 0) {
                counts.Add(Counted(ec, "Energy Cell", "Energy Cells"));
            }

            if (ac > 0) {
                counts.Add(Counted(ac, "Ability Cell", "Ability Cells"));
            }

            if (ks > 0) {
                counts.Add(Counted(ks, "Keystone", "Keystones"));
            }

            if (ms > 0) {
                counts.Add(Counted(ms, "Mapstone", "Mapstones"));
            }

            if (other > 0) {
                counts.Add(Counted(other, "other item", "other items"));
            }

            if (exp > 0) {
                counts.Add(exp + " Spirit Light");
            }

            if (counts.Count > 0) {
                lines.Add(string.Join(", ", counts.ToArray()));
            }

            if (lines.Count > 0) {
                var finderNames = new List<string>(finders);
                finderNames.Sort();
                // sorted first, so "self" lands last in a mixed batch
                if (anySelf && finderNames.Count > 0) {
                    finderNames.Add("self");
                }

                // no names at all means Archipelago handed back only things
                // we found ourselves
                var header = finderNames.Count > 0
                    ? $"Received from {string.Join(", ", finderNames.ToArray())}:\n"
                    : "Received:\n";
                RandomizerSwitch.PickupMessage(header + string.Join("\n", lines.ToArray()), 480);
            }
        } catch (Exception e) {
            Randomizer.LogError("MW.ShowBatchMessage: " + e.Message);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game;

// Multiworld, owner side: manifest lines (pseudo-locations -2..-257) say what our 256 slots hold and tick field 6
// which were found, and we grant the difference. Granted bits are save items: rollbacks revert item and bit together.
public static class RandomizerMW {
    // granted-slot bitfields, 8 x 32 bits: must stay outside KeptOnDeath so they roll back with the save
    public const int GrantedSlotsBase = 940;

    public const int GrantedSlotsLast = 947;

    // tick field 10 items this save has applied: outside KeptOnDeath too, so a rollback re-grants
    public const int ExtraItemsApplied = 948;

    public class ManifestEntry {
        public int Slot;
        public int Finder;
        public string Code;
        public string Id;
        public string Zone;
        // who to credit when there is no slot to look up
        public string Sender;
    }

    public static Dictionary<int, ManifestEntry> Manifest = new Dictionary<int, ManifestEntry>();

    private static HashSet<int> warnedSlots = new HashSet<int>();

    // tick field 7: ";"-joined "<pid>.<name>" pairs
    public static Dictionary<int, string> PlayerNames = new Dictionary<int, string>();

    // --- Archipelago ---
    // coord -> {recipient token, bare item name}, from reserved lines
    public static Dictionary<int, string[]> ApItems = new Dictionary<int, string[]>();

    // slot -> who found it, from the apfrom tick signal. "" means you did.
    public static Dictionary<int, string> SlotSenders = new Dictionary<int, string>();

    // slot -> the hint Archipelago sold us for it, from tick field 8
    public static Dictionary<int, string> ApHints = new Dictionary<int, string>();

    // only AP grants straddle ticks; native multiworld grants immediately
    public static bool ApGrants;

    public static void Reset() {
        // a room that never sends tick field 9 must not inherit the last room's release
        Released = false;
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

    // an AP token: "P<pid>" (a world of this game, named by the tick) or a room name, verbatim
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
    //   <coord>|MW|<shadow>,<slot>,<recipient>,<own slot>,<code>,<id>|<zone>
    // own slot -1 means unpromised; an empty recipient means nobody scouted it yet.
    public static void AddApLine(int coords, string value) {
        var parts = value.Split(new[] { ',' }, 6);
        if (parts.Length != 6 || !int.TryParse(parts[0], out var owner)) {
            return;
        }

        if (Randomizer.PlayerCount <= 0 || owner <= Randomizer.PlayerCount) {
            return;   // a plain cross-world line, not a reserved one
        }

        if (string.IsNullOrEmpty(parts[2])) {
            return;
        }

        ApItems[coords] = new[] { parts[2], RandomizerItems.Name(parts[4], parts[5]) };
        if (int.TryParse(parts[3], out var slot) && slot >= 0) {
            ApSelfSlots[coords] = slot;
        }
    }

    // coord -> our own manifest slot, for reserved locations holding our item
    public static Dictionary<int, int> ApSelfSlots = new Dictionary<int, int>();

    /// <summary>
    /// Contact with a reserved location holding our own item: grant it now. The bridge fills this
    /// check into the promised slot, so the granted bit doubles as the spent marker. False if none.
    /// </summary>
    public static bool GrantSelfItem(int coords) {
        if (!ApSelfSlots.TryGetValue(coords, out var slot) || !Manifest.ContainsKey(slot)) {
            return false;
        }

        if (SlotGranted(slot)) {
            // came back to it after a rollback: the item is already ours
            var name = ApItems.TryGetValue(coords, out var ap) ? ap[1] : "That";
            RandomizerSwitch.PickupMessage(RandomizerItems.ColorWrap(name) + " (already collected)");
            return true;
        }

        // "" = found it yourself: the grant message gets no "from" suffix
        SlotSenders[slot] = "";
        Grant(new List<int> { slot }, ApBatchMessageThreshold);
        return true;
    }

    /// <summary>
    /// Our own Archipelago item here is already in the save. Its coord bit rolls back on death but
    /// the server re-sets the slot, so the map treats the location as spent.
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

    // who to name on a grant: apfrom's sender for AP, else the manifest's finder; "" = yourself
    private static string SenderFor(ManifestEntry entry) {
        if (entry.Sender != null) {
            return entry.Sender;
        }

        if (SlotSenders.TryGetValue(entry.Slot, out var token)) {
            return token == "" ? "" : ApName(token);
        }

        return PlayerName(entry.Finder);
    }

    // display-time "P<n>" -> name for clues baked at seed parse; names arrive later on the tick
    public static string ResolveNames(string text) {
        try {
            return NameRef.Replace(text, m => int.TryParse(m.Groups[1].Value, out var pid) ? PlayerName(pid, true) + "'s" : m.Value);
        } catch (Exception e) {
            Randomizer.LogError("MW.ResolveNames: " + e.Message);
            return text;
        }
    }

    // --- progressive Archipelago hints ---
    // Clues that reveal as you play name their slots in the tick's aph field; the server buys the
    // hint and field 8 carries the answer, which stands in for the baked clue.

    // slots already told to the server, and the countdown to repeating unanswered ones
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

    // the bought hint for a slot, else the baked clue; every display site reads through here
    public static bool HasApHint(int slot) {
        return slot >= 0 && ApHints.TryGetValue(slot, out var text) && text != "";
    }

    public static string ApHintOr(int slot, string baked) {
        return HasApHint(slot) ? ApHints[slot] : baked;
    }

    public static void WantHint(List<int> needed, int slot) {
        if (slot >= 0 && needed.Count < MaxHintRequests && !ApHints.ContainsKey(slot)
            && Manifest.ContainsKey(slot) && !needed.Contains(slot)) {
            needed.Add(slot);
        }
    }

    // "<slot>.<slot>" for the tick, or null. The needed set is a level derived from the save: a new
    // slot goes out at once, an unanswered one again every HintResendPeriod ticks.
    public static string HintRequestField() {
        try {
            if (Randomizer.SyncMode != 5 || Manifest.Count == 0 || !Characters.Sein) {
                return null;
            }

            var needed = new List<int>();
            // bounded sets first: keysanity alone can fill the budget
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

    // --- release ---
    // tick field 9: "1" once this world is released (other owners' items handed out). Read every
    // tick, not remembered: a save scummed back past the credits must not forget it.
    public static bool Released;

    public static void OnReleasedField(string field) {
        Released = field.Trim() == "1";
    }

    // only other owners' items are spent by a release; our own follow SelfItemCollected
    public static bool ReleasedAway(RandomizerAction pickup) {
        if (!Released || pickup == null || pickup.Action != "MW") {
            return false;
        }

        var parts = pickup.ValAsStr().Split(',');
        int owner;
        return parts.Length > 0 && int.TryParse(parts[0], out owner) && owner != Us;
    }

    // "<game>.<player>" -- the player half is who we are on the wire.
    private static int Us {
        get {
            var id = Randomizer.SyncId;
            if (string.IsNullOrEmpty(id)) {
                return 0;
            }

            var parts = id.Split('.');
            int pid;
            return parts.Length > 1 && int.TryParse(parts[1], out pid) ? pid : 0;
        }
    }

    public static bool IsManifestLine(int coords, string code) {
        return code == "MW" && coords <= -2 && coords >= -257;
    }

    // has the item at this manifest pseudo-location already been granted to us?
    public static bool ManifestLocGranted(int coords) {
        return SlotGranted(-coords - 2);
    }

    // a warp from outside the seed still needs its logic node; seed parse registers plain TW lines only
    private static void RegisterWarpLogic(ManifestEntry entry) {
        if (entry.Code == "TW") {
            var warp = entry.Id.Split(',');
            if (warp.Length > 3 && !Randomizer.WarpLogicLocations.ContainsKey(warp[0])) {
                Randomizer.WarpLogicLocations.Add(warp[0], warp[3]);
            }
        }
    }

    // manifest line: <-(slot+2)>|MW|<finder>,<holder>,<code>,<id>|<zone>; bounded split, TW ids hold commas
    public static void AddManifestEntry(int coords, string value, string zone) {
        try {
            var slot = -coords - 2;
            var parts = value.Split(new[] { ',' }, 4);
            var entry = new ManifestEntry();
            entry.Slot = slot;
            entry.Finder = int.Parse(parts[0]);
            entry.Code = parts[2];
            entry.Id = parts[3];
            entry.Zone = zone;
            Manifest[slot] = entry;

            // holder is empty in plain multiworld, where the finder already is whose world it is
            var holder = parts[1];
            var who = string.IsNullOrEmpty(holder) ? $"P{entry.Finder}" : holder;
            var clue = string.IsNullOrEmpty(zone) ? who : $"{who} {zone}";

            RegisterWarpLogic(entry);

            // clues for our dungeon keys placed in other worlds
            if (Randomizer.CluesMode && entry.Code == "EV") {
                if (int.TryParse(entry.Id, out var evId) && evId % 2 == 0 && evId <= 4) {
                    RandomizerClues.AddClue(clue, evId / 2, slot);
                }
            }

            // the Forlorn escape names where Stomp and Grenade went; one in another world has no SK line
            if (entry.Code == "SK" && (entry.Id == "4" || entry.Id == "51")) {
                if (entry.Id == "4") {
                    Randomizer.StompSlot = slot;
                    Randomizer.StompZone = clue;
                } else {
                    Randomizer.GrenadeSlot = slot;
                    Randomizer.GrenadeZone = clue;
                }
            }

            // keysanity door keys too; the clue's coords are the pseudo-loc, found via the granted bits
            if (Randomizer.Keysanity.IsActive && entry.Code == "RB") {
                if (int.TryParse(entry.Id, out var rbId)) {
                    Randomizer.Keysanity.AddClue(rbId, coords, clue);
                }
            }
        } catch (Exception e) {
            Randomizer.LogError($"MW.AddManifestEntry({coords}, {value}): {e.Message}");
        }
    }

    // more grants than this in one tick get one grouped summary; five is what alt+T holds
    public const int BatchMessageThreshold = 5;

    // AP splits one batch over several ReceivedItems; one quiet tick collects the stragglers
    public const int ApGrantWindowTicks = 1;

    public const int ApBatchMessageThreshold = 5;

    private static List<int> pendingSlots = new List<int>();
    private static int windowTicks;

    // tick field 6: 8 ";"-joined uints; true if anything was granted
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

            // pending is a level, not an edge: only a NEW slot re-arms the window
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
            // a slot can be granted between the tick that saw it and this one: never twice
            if (SlotGranted(slot) || !GrantSlot(slot, batch || silent, batched)) {
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

        Give(Manifest[slot], -slot - 2, batch, batched);
        return true;
    }

    private static void Give(ManifestEntry entry, int coords, bool batch, List<ManifestEntry> batched) {
        if (batch) {
            // squelch per-item messages; ShowBatchMessage summarizes after
            var squelched = RandomizerSwitch.SilentMode;
            RandomizerSwitch.SilentMode = true;
            try {
                RandomizerSwitch.GivePickup(new RandomizerAction(entry.Code, entry.Id), coords, false);
            } finally {
                RandomizerSwitch.SilentMode = squelched;
                RandomizerSwitch.SeedSilent = false;
            }

            batched.Add(entry);
        } else {
            // "[pickup] from [player]", or just the pickup when it was our own find
            var sender = SenderFor(entry);
            RandomizerSwitch.MessageSuffix = sender == "" ? null : $" from {sender}";
            try {
                RandomizerSwitch.GivePickup(new RandomizerAction(entry.Code, entry.Id), coords, false);
            } finally {
                RandomizerSwitch.MessageSuffix = null;
            }
        }
    }

    // tick field 10: Archipelago items with no slot, ";"-joined and append-only; true if any were given
    public static bool OnExtraItemsField(string field) {
        try {
            if (string.IsNullOrEmpty(field) || !Characters.Sein || Characters.Sein.Inventory == null) {
                return false;
            }

            var items = field.Split(';');
            var applied = Characters.Sein.Inventory.GetRandomizerItem(ExtraItemsApplied);
            if (applied >= items.Length) {
                return false;
            }

            var silent = Randomizer.CreditsActive;
            var batch = items.Length - applied > ApBatchMessageThreshold;
            var batched = new List<ManifestEntry>();
            for (var i = applied; i < items.Length; i++) {
                try {
                    var entry = ParseExtraItem(items[i]);
                    if (entry == null) {
                        throw new FormatException("malformed");
                    }

                    RegisterWarpLogic(entry);
                    // coords 0: the pickup is offworld and no location reports found
                    Give(entry, 0, batch || silent, batched);
                } catch (Exception e) {
                    Randomizer.LogError($"MW: extra item {i} ({items[i]}): {e.Message}");
                }

                // counted even when unreadable, or one bad item would stall the rest
                Characters.Sein.Inventory.SetRandomizerItem(ExtraItemsApplied, i + 1);
            }

            if (batched.Count > 0 && !silent) {
                ShowBatchMessage(batched);
            }

            return true;
        } catch (Exception e) {
            Randomizer.LogError("MW.OnExtraItemsField: " + e.Message);
        }

        return false;
    }

    // one field 10 item, percent-escaped "code|id"; null if malformed
    public static ManifestEntry ParseExtraItem(string raw) {
        var text = UnescapeTickItem(raw ?? "");
        var bar = text.IndexOf('|');
        if (bar <= 0 || bar == text.Length - 1) {
            return null;
        }

        return new ManifestEntry { Slot = -1, Code = text.Substring(0, bar), Id = text.Substring(bar + 1), Sender = "Archipelago" };
    }

    // the server escapes only % , ; (as %25 %2C %3B); any other % passes through as sent
    public static string UnescapeTickItem(string text) {
        if (text.IndexOf('%') < 0) {
            return text;
        }

        var sb = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++) {
            var hex = text[i] == '%' && i + 2 < text.Length ? text.Substring(i + 1, 2).ToUpperInvariant() : "";
            var c = hex == "25" ? '%' : hex == "2C" ? ',' : hex == "3B" ? ';' : '\0';
            if (c == '\0') {
                sb.Append(text[i]);
            } else {
                sb.Append(c);
                i += 2;
            }
        }

        return sb.ToString();
    }


    private static string Counted(int n, string singular, string plural, string wrap = "") {
        return $"{wrap}{n} {(n == 1 ? singular : plural)}{wrap}";
    }

    private static readonly string[] DungeonWells = { "Ginso", "Forlorn", "Horu" };

    // dungeons first in game order, then the rest as they came; colors come from "<id> Teleporter"
    private static List<string> Wells(List<string> ids) {
        var order = new List<string>();
        foreach (var want in DungeonWells) {
            if (ids.Contains(want)) {
                order.Add(want);
            }
        }

        foreach (var id in ids) {
            if (!order.Contains(id)) {
                order.Add(id);
            }
        }

        var shown = new List<string>();
        foreach (var id in order) {
            shown.Add(RandomizerItems.ColorWrapAs(id + " Teleporter", id));
        }

        return shown;
    }

    // "Warp to X" is the seed's own name for it; under a Warps: heading the prefix is noise
    private static List<string> Warps(List<string> names) {
        var shown = new List<string>();
        foreach (var name in names) {
            shown.Add(name.StartsWith("Warp to ") ? name.Substring(8) : name);
        }

        return shown;
    }

    // skills, then world events (+ shards/frags), then teleporters, warps, then a counts line
    private static void ShowBatchMessage(List<ManifestEntry> entries) {
        try {
            var skills = new List<string>();
            var events = new List<string>();
            var wells = new List<string>();
            var warps = new List<string>();
            // event ids are already the order the game hands them out in
            var worldEvents = new string[6];
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
                        skills.Add(RandomizerItems.ColorWrap(RandomizerItems.Name(entry.Code, entry.Id)));
                        break;
                    case "EV":
                        int world;
                        if (int.TryParse(entry.Id, out world) && world >= 0 && world < worldEvents.Length) {
                            worldEvents[world] = RandomizerItems.ColorWrap(RandomizerItems.Name(entry.Code, entry.Id));
                        } else {
                            events.Add(RandomizerItems.ColorWrap(RandomizerItems.Name(entry.Code, entry.Id)));
                        }

                        break;
                    case "TP":
                        wells.Add(entry.Id);
                        break;
                    case "TW":
                        warps.Add(RandomizerItems.Name(entry.Code, entry.Id));
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

            // ahead of the shards, which are progress towards the same events
            for (var i = worldEvents.Length - 1; i >= 0; i--) {
                if (worldEvents[i] != null) {
                    events.Insert(0, worldEvents[i]);
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

            if (wells.Count > 0) {
                lines.Add("Teleporters: " + string.Join(", ", Wells(wells).ToArray()));
            }

            if (warps.Count > 0) {
                lines.Add("Warps: " + string.Join(", ", Warps(warps).ToArray()));
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

                // no names: Archipelago handed back only our own finds
                var header = finderNames.Count > 0
                    ? $"Received from {string.Join(", ", finderNames.ToArray())}:\n"
                    : "Received:\n";
                // top-anchored so a tall list grows down, not off the top; a second per ten items, capped
                var frames = 480 + 60 * (entries.Count / 10);
                RandomizerSwitch.PickupMessage(
                    "ANCHORTOP" + header + string.Join("\n", lines.ToArray()),
                    frames > 1800 ? 1800 : frames);
            }
        } catch (Exception e) {
            Randomizer.LogError("MW.ShowBatchMessage: " + e.Message);
        }
    }
}

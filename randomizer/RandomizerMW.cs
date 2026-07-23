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
            for (int i = 0; i < 8 && i < parts.Length; i++)
            {
                uint server = 0;
                if (!uint.TryParse(parts[i], out server))
                    continue;
                uint local = AsUint(Characters.Sein.Inventory.GetRandomizerItem(GrantedSlotsBase + i));
                if ((server & ~local) == 0)
                    continue;
                for (int bit = 0; bit < 32; bit++)
                {
                    if ((server & ~local & (1u << bit)) == 0)
                        continue;
                    if (GrantSlot(i * 32 + bit))
                    {
                        // mark only successful grants: a slot with no manifest
                        // entry (wrong seed file?) stays pending so a corrected
                        // seed (Alt+L) can still deliver it
                        local |= 1u << bit;
                        granted = true;
                    }
                }
                Characters.Sein.Inventory.SetRandomizerItem(GrantedSlotsBase + i, AsInt(local));
            }
        }
        catch (Exception e)
        {
            Randomizer.LogError("MW.OnSlotsField: " + e.Message);
        }
        return granted;
    }

    private static bool GrantSlot(int slot)
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
        return true;
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

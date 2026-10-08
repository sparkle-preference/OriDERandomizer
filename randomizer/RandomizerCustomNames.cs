using System;
using System.Collections.Generic;
using System.IO;
using Game;

public static class RandomizerCustomNames {
    public static string FileName = "ExpNames.txt";

    public static List<String> DefaultList = new List<String> {
        "Apples", "Bananas", "Bells", "Bits", "Bolts", "Boonbucks", "Boxings", "Brick", "Brownie Points",
        "Bytes", "Cash", "Coins", "Comments", "Credits", "Crowns", "Diamonds", "Dollars", "Dollerydoos", "Doubloons", "Drams", "EXP", "Echoes", "Emeralds", "Euros",
        "Exalted Orbs", "Experience", "Farthings", "Fish", "Fun", "GP", "Gallons", "Geo", "Gil", "Glod", "Gold", "Hryvnia", "Hugs", "Kalganids", "Leaves", "Likes",
        "Marbles", "Minerals", "Money", "Munny", "Nobles", "Notes", "Nuts", "Nuyen", "Ori Money", "Pesos", "Pieces of Eight", "Points", "Poké", "Pons", "Pounds Sterling",
        "Quatloos", "Quills", "Rings", "Rubies", "Runes", "Rupees", "Sapphires", "Sheep", "Shillings", "Silver", "Slivers", "Socks", "Solari", "Souls", "Sovereigns",
        "Spheres", "Spirit Bucks", "Spirit Light", "Stamps", "Stonks", "Strawberries", "Subs", "Tickets", "Tokens", "Vespene Gas", "Wheat", "Widgets", "Wood", "XP",
        "Yen", "Zenny", "Zloty"
    };

    public static List<String> ExpNames;

    public static void ParseExpNames() {
        if (!File.Exists(FileName)) {
            WriteDefaultFile();
        }

        try {
            ExpNames = new List<string>(File.ReadAllLines(FileName));
            if (ExpNames.Count == 0) {
                ExpNames = DefaultList;
            }
        } catch (Exception e) {
            Randomizer.LogError("Error parsing ExpNames: " + e.Message);
            ExpNames = DefaultList;
        }
    }

    public static void WriteDefaultFile() {
        using (var writer = new StreamWriter(FileName, false)) {
            foreach (var name in DefaultList) {
                writer.WriteLine(name);
            }
        }
    }

    public static string PickupNamesFile = "PickupNames.txt";

    private static PickupNameRules pickupNames;

    private const string PickupNamesHelp =
        "// Custom Pickup Names: used when Custom Pickup Names is on in the Rando options.\n"
        + "// Lines in this file should have the following format:\n"
        + "// KEYS: new_name1 | new_name2 | ...\n"
        + "// A key is a comma-separated list of one or more of the following:\n"
        + "//   -- CODE|ID    (A specific pickup, like EV|1 (clean water))\n"
        + "//   -- CODE|LO-HI (A range of pickups with one code, like EX|1-9 (single-digit exp values))\n"
        + "//   -- CODE|*     (Any pickup with a code, like EV|* (any world event))\n"
        + "// A KS, MS, HC, EC or RB key can end in =N or =LO-HI to match only when {count} is in that range,\n"
        + "//   like MS|1=5 (when you now have 5 mapstones).\n"
        + "// In a new_name, {id} is replaced with the pickup's id, {count} with how many you now have (if applicable),\n"
        + "// and {name} with the original pickup's name. Use \\| to write a | inside a name.\n"
        + "// *#@$ color wrapping and <style> tags both apply - see SH Format.txt for more info.\n"
        + "\n"
        + "// Rules are matched from most to least specific: an exact id match always wins, followed by\n"
        + "//   the narrowest range wins if multiple match, and an * only if no exact or range matches.\n"
        + "//   Among keys equally specific about the id, one with a count wins.\n"
        + "// If you also have Randomized Experience Names enabled, any EX pickup not matched by a rule here\n"
        + "// will be named from ExpNames.txt instead.\n"
        + "\n"
        + "// Some examples (remove the \"// \"s at the start of a line to try them out!)\n"
        + "\n"
        + "//   // Replace small exp values with joke names:\n"
        + "// EX|1-25: {id} Misty revisits | {id} doorwarp attempts\n"
        + "\n"
        + "//   // Give proper thanks for the 1 Exp. (Overrides the earlier rule; ID is more specific than range)\n"
        + "// EX|1: Thanks, Torin\n"
        + "\n"
        + "//   // Make skill names very big so you're sure to notice them\n"
        + "// SK|*:<style fontscale=2.5 linescale=1>{name}</>\n"
        + "\n"
        + "//   // Replace \"Wind Restored\" with \"It Fucken Wimdy\", half the time (shoutouts to jmal116)\n"
        + "// EV|3: {name} | #It Fucken Wimdy#\n"
        + "\n"
        + "//   // Let you know that you got a shard and how many of those you have, but not which one.\n"
        + "// RB|17, RB|19, RB|21: A shard! You have {count}\n"
        + "\n"
        + "//   // Well, they're all stones, right?\n"
        + "// KS|1, MS|1, EV|4: A stone | useful rock | Maybe it was the Sunstone this time\n"
        + "\n"
        + "//   // Only on the fifth mapstone you're holding\n"
        + "// MS|1=5: A suspicious number of mapstones";

    public static void ParsePickupNames() {
        var problems = new List<string>();
        try {
            if (!File.Exists(PickupNamesFile)) {
                File.WriteAllText(PickupNamesFile, PickupNamesHelp + "\n\n" + RandomizerItems.IdReference() + "\n");
            }

            pickupNames = PickupNameRules.Parse(File.ReadAllLines(PickupNamesFile), problems);
        } catch (Exception e) {
            pickupNames = null;
            problems.Add(e.Message);
        }

        foreach (var problem in problems) {
            Randomizer.log(PickupNamesFile + ": " + problem);
        }

        if (problems.Count > 0 && RandomizerSettings.Customization.CustomPickupNames.Value) {
            var more = problems.Count > 1 ? " (and " + (problems.Count - 1) + " more in randomizer.log)" : "";
            var shown = problems[0].Replace("#", "").Replace("*", "").Replace("$", "").Replace("@", "");
            Randomizer.printInfo(RandomizerText.Error(PickupNamesFile + " " + shown) + more, 360);
        }
    }

    // a matching PickupNames.txt rule's name for a pickup, else the message as it was
    public static string Rename(string code, string id, int coords, string message) {
        if (pickupNames == null || !RandomizerSettings.Customization.CustomPickupNames.Value) {
            return message;
        }

        var count = Count(code, id);
        var names = pickupNames.For(code, id, count);
        if (names == null) {
            return message;
        }

        var name = names[new Random(31 * Randomizer.SeedMeta.GetHashCode() + coords + (code + id).GetHashCode()).Next(names.Length)];
        var keepsMessage = name.Contains("{name}");
        name = name.Replace("{id}", id).Replace("{count}", (count ?? 1).ToString()).Replace("{name}", message);
        var cue = RandomizerItems.CueFor(code, id);
        return keepsMessage || cue == null ? name : RandomizerItems.Cue(name, cue);
    }

    // how many you have once the pickup lands (RB counts are bumped before their message, the rest after);
    // null for a code outside PickupNameRules.Counted
    private static int? Count(string code, string id) {
        if (!PickupNameRules.Counted.Contains(code) || !Characters.Sein) {
            return null;
        }

        int.TryParse(id, out var n);
        var step = n < 0 ? -1 : 1;
        var inventory = Characters.Sein.Inventory;
        switch (code) {
            case "RB":
                return inventory.GetRandomizerItem(Math.Abs(n));
            case "KS":
                return inventory.Keystones + step;
            case "MS":
                return inventory.MapStones + step;
            case "HC":
                return Characters.Sein.Mortality.Health.HealthUpgradesCollected + step;
            case "EC":
                return (int)Characters.Sein.Energy.Max + step;
            default:
                return null;
        }
    }

    public static string ExpName(int p) {
        if (RandomizerSettings.Customization.RandomizedExpNames) {
            return ExpNames[new Random(31 * Randomizer.SeedMeta.GetHashCode() + p).Next(ExpNames.Count)];
        }

        return "Experience";
    }
}

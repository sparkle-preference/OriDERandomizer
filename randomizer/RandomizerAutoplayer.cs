using System;
using System.IO;
using System.Linq;
using Game;

// Two items that drive the game with nobody at the controls, off the
// once-a-second half of Randomizer.Tick. Neither is in the item pool.
public static class RandomizerAutoplayer {
    public const int AutoplayId = 1108;

    public const int DropFileId = 1109;

    // relative, so they land beside randomizer.dat in the rando directory
    public const string DropFile = "pickup.tmp";

    public const string TestPickupFile = "test_pickup.txt";

    private static readonly Random rng = new Random();

    private static bool idle;

    public static void Reset() {
        idle = false;
    }

    private static bool Holding(int id) {
        return Characters.Sein && Characters.Sein.Inventory != null
            && Characters.Sein.Inventory.GetRandomizerItem(id) > 0;
    }

    public static void Tick() {
        try {
            Drop();
            Autoplay();
        } catch (Exception e) {
            // this is test scaffolding; it must never cost the caller its tick
            Randomizer.LogError("Autoplayer: " + e.Message);
        }
    }

    private static void Autoplay() {
        if (!Holding(AutoplayId)) {
            return;
        }

        // Touched, not Collected: a repeatable never reports collected and would be picked forever
        var open = RandomizerLocationManager.LocationsByKey.Values
            .Where(loc => loc.Reachable && !loc.Touched && loc.Pickup != null)
            .ToList();
        if (open.Count == 0) {
            if (!idle) {
                idle = true;
                Randomizer.log("Autoplayer: nothing reachable left");
            }

            return;
        }

        idle = false;
        var pick = open[rng.Next(open.Count)];
        Randomizer.log($"Autoplayer: {pick.Name} ({pick.Zone}) holds {pick.Pickup}, {open.Count} reachable");
        pick.Give();
    }

    // The Grant Test Pickup bind: gives whatever pickup test_pickup.txt holds,
    // off the books like a Drop. The file stays put; a keypress cannot loop.
    public static void GrantTestPickup() {
        if (!File.Exists(TestPickupFile)) {
            return;
        }

        string content;
        try {
            content = File.ReadAllText(TestPickupFile).Replace("\r\n", "\n").Trim();
        } catch (Exception e) {
            Randomizer.LogError("Autoplayer.GrantTestPickup read: " + e.Message);
            return;
        }

        var bar = content.IndexOf('|');
        if (bar < 1) {
            return;
        }

        try {
            var action = new RandomizerAction(content.Substring(0, bar), content.Substring(bar + 1));
            Randomizer.log($"Autoplayer: test pickup {action}");

            RandomizerSwitch.FromFile = true;
            try {
                RandomizerSwitch.GivePickup(action, 0, false);
            } finally {
                RandomizerSwitch.FromFile = false;
            }

            RandomizerLocationManager.UpdateReachable();
        } catch (Exception e) {
            Randomizer.LogError($"Autoplayer.GrantTestPickup({content}): {e.Message}");
        }
    }

    private static void Drop() {
        if (!Holding(DropFileId) || !File.Exists(DropFile)) {
            return;
        }

        string content;
        try {
            content = File.ReadAllText(DropFile).Trim();
        } catch (Exception e) {
            Randomizer.LogError("Autoplayer.Drop read: " + e.Message);
            return;
        }

        try {
            File.Delete(DropFile);
        } catch (Exception e) {
            // granting anyway would re-grant every second until the file goes away
            Randomizer.LogError("Autoplayer.Drop could not delete " + DropFile + ": " + e.Message);
            return;
        }

        var bar = content.IndexOf('|');
        if (bar < 1) {
            return;
        }

        try {
            var action = new RandomizerAction(content.Substring(0, bar), content.Substring(bar + 1));
            Randomizer.log($"Autoplayer: dropped {action}");

            // coords 0, not found locally: claims no location and sends nothing to the server
            RandomizerSwitch.FromFile = true;
            try {
                RandomizerSwitch.GivePickup(action, 0, false);
            } finally {
                RandomizerSwitch.FromFile = false;
            }

            // drops skip the sync tick that would otherwise refresh logic
            RandomizerLocationManager.UpdateReachable();
        } catch (Exception e) {
            Randomizer.LogError($"Autoplayer.Drop({content}): {e.Message}");
        }
    }
}

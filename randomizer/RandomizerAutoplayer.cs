using System;
using System.IO;
using System.Linq;
using Game;

// Two ways to drive a build with nobody at the controls, each held as an item
// so a seed can hand it out at spawn. Autoplay takes one reachable location a
// second; the drop file grants whatever a process outside the game leaves in
// it. Both run off the once-a-second half of Randomizer.Tick.
public static class RandomizerAutoplayer {
    public const int AutoplayId = 1108;

    public const int DropFileId = 1109;

    // relative, so it lands beside randomizer.dat in the rando directory
    public const string DropFile = "pickup.tmp";

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

        // Touched, not Collected: a repeatable location never reports itself
        // collected, so it would be picked forever
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

            // coords 0 and not-found-locally: this came from outside the seed,
            // so it claims no location and sends nothing to the server
            RandomizerSwitch.GivePickup(action, 0, false);

            // a dropped item skips the sync tick, which is what otherwise
            // refreshes logic when an item arrives from outside the seed
            RandomizerLocationManager.UpdateReachable();
        } catch (Exception e) {
            Randomizer.LogError($"Autoplayer.Drop({content}): {e.Message}");
        }
    }
}

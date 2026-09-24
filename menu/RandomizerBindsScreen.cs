using System.Collections.Generic;

public class RandomizerBindsScreen : CustomSettingsScreen {
    // Ordered by how often a run reaches for them, not by how the file lists them.
    public override void InitScreen() {
        DefaultTooltip = "Click on an action to add or remove binds";

        AddHeader("BASIC");
        AddRandomizerBind("Warp", "Open the teleportation map to start a warp");
        AddRandomizerBind("Map Warp", "Hold to warp to a highlighted target from the area map");
        AddRandomizerBind("Reload Seed", "Load the seed file from disk (and re-initialize the randomizer)");
        AddRandomizerBind("Replay Message", "Show the last displayed message again; hold to see the last five");
        AddRandomizerBind("Show Progress", "Display total pickup count, dungeon keys, and goal progress");

        AddHeader("LISTING");
        AddRandomizerBind("List Trees", "List collected and uncollected Skill Trees");
        AddRandomizerBind("List Map Altars", "List collected and uncollected Mapstones by zone");
        AddRandomizerBind("List Teleporters", "List collected and uncollected teleporters");
        AddRandomizerBind("List Relics", "List collected and uncollected Relics");
        AddRandomizerBind("Show Bonuses", "List held bonus items");
        AddRandomizerBind("Show Keysanity Progress", "(Keysanity only) list which doors you have keystones for, and any unlocked Keysanity hints");
        AddRandomizerBind("Show Stats", "Cycle through the stats pages displayed during the credits");

        AddHeader("CONTROLS");
        var isTapMode = RandomizerSettings.Controls.DoubleBash.Value == RandomizerSettings.DoubleBashMode.Tap;
        AddRandomizerBind("Double Bash", $"{(isTapMode ? "Tap" : "Hold")} while Bashing to Double Bash.");
        if (RandomizerSettings.Controls.GrenadeJump.Value == RandomizerSettings.GrenadeJumpMode.Auto) {
            // it's not meaningful to show this in a different gjump mode.
            AddRandomizerBind("Grenade Jump", "Press with Wall Charge Jump charged to perform a Grenade Jump");
        }
        AddRandomizerBind("Reset Grenade Aim", "(Controller) Reset Grenade Aim to a neutral position");
        AddRandomizerBind("Suppress Autofire", "(With Autofire set to Hold) Hold this to enable using Charge Flame normally");
        AddRandomizerBind("Color Shift", "Toggle color shift mode, which randomizes Ori's color every time a pickup is collected");

        AddHeader("MENUS");
        AddRandomizerBind("Toggle Map Mode", "Toggle item filters on the map");
        AddRandomizerBind("Menu Skip Backwards", "Jumps back/up a page in scrolling menus and the save select screen");
        AddRandomizerBind("Menu Skip Forwards", "Jumps forwards/down a page in scrolling menus and the save select screen");
        AddRandomizerBind("Menu Home", "Jumps to the top of a long menu");
        AddRandomizerBind("Menu End", "Jumps to the bottom of a long menu");

        AddHeader("PRACTICE MODE");
        AddRandomizerBind("Create Practice Segment", "Create a practice segment from your current location");
        AddRandomizerBind("Practice Menu", "Opens the practice menu"); // TODO: what do we use this for?
        AddRandomizerBind("Retry Practice Segment", "Restart a currently-running segment you are practicing");
        AddRandomizerBind("Open Practice Editor Page", "Opens the practice editor in your browser"); // TODO: what do we use this for?

        AddHeader("BONUS SKILLS");
        AddRandomizerBind("Bonus Switch", "Cycle between active bonus skills");
        AddRandomizerBind("Bonus Toggle", "Use the active bonus skill");
        for (var slot = 1; slot <= BonusSlots; slot++) {
            AddRandomizerBind($"Bonus {slot}", $"Use the bonus skill in slot {slot}");
        }

        AddHeader("CHAOS");
        AddRandomizerBind("Toggle Chaos", "Toggle periodic chaos effect triggering");
        AddRandomizerBind("Chaos Verbosity", "Toggle chaos effect verbosity");
        AddRandomizerBind("Force Chaos Effect", "Trigger a chaos effect right now");


        // test rigging, shown only with the dev settings on
        if (RandomizerSettings.Dev != null && RandomizerSettings.Dev.Value) {
            AddHeader("DEV");
            AddRandomizerBind("Spawn Echo", "Spawn in a ghost echo");
            AddRandomizerBind("Clear All Echoes", "Remove all echoes");
            AddRandomizerBind("Grant Test Pickup", "Grant the contents of test_pickup.txt");
            AddRandomizerBind("Start Practice Debug", "Load into practice/debug.bfrp");
        }

        ScrollAfter(Footer());
        BindLegend();
    }

    public override string ResetQuestion {
        get { return "Reset ALL rando binds to default?"; }
    }

    // every action, rows or not: Grenade Jump and the dev binds may have none
    public override void ResetToDefaults() {
        Backup(RandomizerRebinding.BindsFile);
        RandomizerRebinding.UseDefaults();
        foreach (var control in GetComponentsInChildren<RandomizerBindControl>(true)) {
            control.Reset();
        }

        RandomizerRebinding.WriteBindsToFile();
        AfterReset(RandomizerRebinding.BindsFile);
    }

    protected override void SnapshotUnlisted() {
        unlisted.Clear();
        var rows = new HashSet<string>();
        foreach (var control in GetComponentsInChildren<RandomizerBindControl>(true)) {
            rows.Add(control.Action);
        }

        foreach (var action in RandomizerRebinding.DefaultBinds.Keys) {
            if (!rows.Contains(action)) {
                unlisted[action] = RandomizerRebinding.BindNamed(action).ToString();
            }
        }
    }

    protected override bool UnlistedChanged {
        get {
            foreach (var pair in unlisted) {
                if (RandomizerRebinding.BindNamed(pair.Key).ToString() != pair.Value) {
                    return true;
                }
            }

            return false;
        }
    }

    protected override void RestoreUnlisted() {
        foreach (var pair in unlisted) {
            if (RandomizerRebinding.BindNamed(pair.Key).ToString() != pair.Value) {
                RandomizerRebinding.SetBinds(pair.Key, pair.Value);
            }
        }
    }

    // the file form of each action with no row here, as the page was entered
    private readonly Dictionary<string, string> unlisted = new Dictionary<string, string>();

    private const int BonusSlots = 9;
}

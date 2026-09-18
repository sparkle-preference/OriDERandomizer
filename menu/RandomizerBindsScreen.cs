public class RandomizerBindsScreen : CustomSettingsScreen {
    // Ordered by how often a run reaches for them, not by how the file lists them.
    public override void InitScreen() {
        DefaultTooltip = "Click on an action to add or remove binds";

        AddHeader("GETTING OUT OF TROUBLE");
        AddRandomizerBind("Warp", "Gets you out of where you are, to somewhere else.");
        AddRandomizerBind("Map Warp", "Hold this on the world map to warp to the spot you picked.");

        AddHeader("WHAT THE SEED IS DOING");
        AddRandomizerBind("Show Progress", "How much of the seed you have found so far.");
        AddRandomizerBind("Show Keysanity Progress", "Which dungeon keys you are holding.");
        AddRandomizerBind("Show Stats", "Times, pickups and the other numbers for this run.");
        AddRandomizerBind("Replay Message", "Shows the last message again. Hold for more of them.");
        AddRandomizerBind("Reload Seed", "Re-reads the seed file, for when you have just changed it.");

        AddHeader("FINDING THINGS");
        AddRandomizerBind("List Trees", "Which ability trees you have visited.");
        AddRandomizerBind("List Map Altars", "Which map stones you have turned in.");
        AddRandomizerBind("List Teleporters", "Which teleporters you can warp to.");
        AddRandomizerBind("List Relics", "Which relics you have found.");

        AddHeader("PLAYING");
        AddRandomizerBind("Double Bash", "Queues a second bash out of the first.");
        AddRandomizerBind("Grenade Jump", "Throws and jumps in one press, with Grenade Jump set to Auto.");
        AddRandomizerBind("Reset Grenade Aim", "Points a held grenade back the way Ori is facing.");
        AddRandomizerBind("Suppress Autofire", "Hold to stop Spirit Flame firing on its own.");
        AddRandomizerBind("Toggle Map Mode", "Cycles what the world map is showing.");
        AddRandomizerBind("Color Shift", "Recolours Ori. Press again to put it back.");

        AddHeader("BONUS SKILLS");
        AddRandomizerBind("Show Bonuses", "Lists the bonus items you are carrying.");
        AddRandomizerBind("Bonus Switch", "Cycles which bonus skill is the active one.");
        AddRandomizerBind("Bonus Toggle", "Uses the active bonus skill.");
        for (var slot = 1; slot <= BonusSlots; slot++) {
            AddRandomizerBind("Bonus " + slot, "Uses the bonus skill in slot " + slot + ".");
        }

        AddHeader("PRACTICE");
        AddRandomizerBind("Practice Menu", "Opens the practice menu.");
        AddRandomizerBind("Create Practice Segment", "Starts a new practice segment from where you are.");
        AddRandomizerBind("Retry Practice Segment", "Restarts the segment you are practising.");
        AddRandomizerBind("Open Practice Editor Page", "Opens the practice editor in your browser.");

        AddHeader("CHAOS");
        AddRandomizerBind("Toggle Chaos", "Turns chaos effects on and off.");
        AddRandomizerBind("Chaos Verbosity", "Turns the messages about chaos effects on and off.");
        AddRandomizerBind("Force Chaos Effect", "Fires a chaos effect right now.");

        AddHeader("MENUS");
        AddRandomizerBind("Menu Skip Backwards", "Jumps back a page in long menus and the save list.");
        AddRandomizerBind("Menu Skip Forwards", "Jumps on a page in long menus and the save list.");
        AddRandomizerBind("Menu Home", "Jumps to the top of a long menu.");
        AddRandomizerBind("Menu End", "Jumps to the bottom of a long menu.");

        // Test rigging rather than play: shown to whoever has already said they want the rest
        // of the dev settings.
        if (RandomizerSettings.Dev != null && RandomizerSettings.Dev.Value) {
            AddHeader("DEV");
            AddRandomizerBind("Spawn Echo", "Spawns a ghost echo where you are.");
            AddRandomizerBind("Clear All Echoes", "Removes every echo that has been spawned.");
            AddRandomizerBind("Grant Test Pickup", "Gives the test pickup, for trying messages out.");
            AddRandomizerBind("Start Practice Debug", "Loads practice/debug.bfrp without the chooser.");
        }

        ScrollAfter(Footer());
        BindLegend();
    }

    public override string ResetQuestion {
        get { return "Reset ALL rando binds to default?"; }
    }

    public override void ResetToDefaults() {
        Backup(RandomizerRebinding.BindsFile);
        foreach (var control in GetComponentsInChildren<RandomizerBindControl>(true)) {
            RandomizerRebinding.SetBinds(control.Action, RandomizerRebinding.DefaultBinds[control.Action]);
            control.Reset();
        }

        RandomizerRebinding.WriteBindsToFile();
    }

    private const int BonusSlots = 9;
}

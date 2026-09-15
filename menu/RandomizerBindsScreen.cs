public class RandomizerBindsScreen : CustomSettingsScreen {
    // Ordered by how often a run reaches for them, not by how the file lists them.
    public override void InitScreen() {
        DefaultTooltip = "Click on an action to add or remove binds";

        AddHeader("GETTING AROUND");
        AddRandomizerBind("Warp");
        AddRandomizerBind("Map Warp");
        AddRandomizerBind("Reload Seed");

        AddHeader("WHAT THE SEED IS DOING");
        AddRandomizerBind("Show Progress");
        AddRandomizerBind("Show Keysanity Progress");
        AddRandomizerBind("Show Stats");
        AddRandomizerBind("Replay Message");

        AddHeader("FINDING THINGS");
        AddRandomizerBind("List Trees");
        AddRandomizerBind("List Map Altars");
        AddRandomizerBind("List Teleporters");
        AddRandomizerBind("List Relics");

        AddHeader("PLAYING");
        AddRandomizerBind("Double Bash");
        AddRandomizerBind("Grenade Jump");
        AddRandomizerBind("Reset Grenade Aim");
        AddRandomizerBind("Suppress Autofire");
        AddRandomizerBind("Toggle Map Mode");
        AddRandomizerBind("Color Shift");

        AddHeader("BONUS ITEMS");
        AddRandomizerBind("Show Bonuses");
        AddRandomizerBind("Bonus Switch");
        AddRandomizerBind("Bonus Toggle");
        for (var slot = 1; slot <= BonusSlots; slot++) {
            AddRandomizerBind("Bonus " + slot);
        }

        AddHeader("PRACTICE");
        AddRandomizerBind("Practice Menu");
        AddRandomizerBind("Create Practice Segment");
        AddRandomizerBind("Retry Practice Segment");
        AddRandomizerBind("Open Practice Editor Page");

        AddHeader("CHAOS");
        AddRandomizerBind("Toggle Chaos");
        AddRandomizerBind("Chaos Verbosity");
        AddRandomizerBind("Force Chaos Effect");

        AddHeader("MENUS");
        AddRandomizerBind("Menu Home");
        AddRandomizerBind("Menu End");
        AddRandomizerBind("Menu Skip Backwards");
        AddRandomizerBind("Menu Skip Forwards");

        // Test rigging rather than play: shown to whoever has already said they want the rest
        // of the dev settings.
        if (RandomizerSettings.Dev != null && RandomizerSettings.Dev.Value) {
            AddHeader("DEV");
            AddRandomizerBind("Spawn Echo");
            AddRandomizerBind("Clear All Echoes");
            AddRandomizerBind("Grant Test Pickup");
            AddRandomizerBind("Start Practice Debug");
        }

        AddButton("Reset Rando Binds", ResetBinds, "Puts every rando bind on this screen back to its default.");

        ScrollAfter(Footer());
        BindLegend();
    }

    private void ResetBinds() {
        Confirm("Reset ALL rando binds to default?", new[] { "OK", "CANCEL" }, answer => {
            if (answer == 0) {
                DoResetBinds();
            }
        });
    }

    private void DoResetBinds() {
        foreach (var control in GetComponentsInChildren<RandomizerBindControl>(true)) {
            RandomizerRebinding.SetBinds(control.Action, RandomizerRebinding.DefaultBinds[control.Action]);
            control.Reset();
        }

        RandomizerRebinding.WriteBindsToFile();
    }

    private const int BonusSlots = 9;
}

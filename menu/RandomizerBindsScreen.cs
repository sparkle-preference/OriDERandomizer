public class RandomizerBindsScreen : CustomSettingsScreen {
    public override void InitScreen() {
        DefaultTooltip = "Click on an action to add or remove binds";
        AddRandomizerBind("Warp");
        AddRandomizerBind("Show Progress");
        AddRandomizerBind("Reload Seed");
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
}

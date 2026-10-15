using Game;

public class RandomizerGinsoEscapeWaterCondition : Condition {
    // Returns true if we should start the ginso escape water.
    public override bool Validate(IContext context) {
        var softlockFixesDisabled = Characters.Sein.Inventory.GetRandomizerItem(1103) != 0;
        if (softlockFixesDisabled) {
            return true;
        }
        return !Randomizer.Inventory.FinishedGinsoEscape;
    }
}
using Game;

public class RandomizerSetInventoryAction : ActionMethod {
    public override void Perform(IContext context) {
        if (Characters.Sein == null || Characters.Sein.Inventory == null) {
            return;
        }
        if (modifier == Modifier.Set) {
            Randomizer.Inventory.SetRandomizerItem(inventorySlot, value);
        }
        if (modifier == Modifier.Add) {
            var currentValue = Randomizer.Inventory.GetRandomizerItem(inventorySlot);
            Randomizer.Inventory.SetRandomizerItem(inventorySlot, currentValue + value);
        }
    }

    public enum Modifier { Set, Add };
    public Modifier modifier;
    public int inventorySlot;
    public int value;
}

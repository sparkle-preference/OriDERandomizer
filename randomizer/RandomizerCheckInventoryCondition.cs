using Game;

public class RandomizerCheckInventoryCondition : Condition {
    public override bool Validate(IContext context) {
        if (Characters.Sein == null || Characters.Sein.Inventory == null) {
            return defaultResult;
        }
        int readValue = Characters.Sein.Inventory.GetRandomizerItem(inventorySlot);
        if (comparison == Comparison.Equal) {
            return readValue == value;
        }
        if (comparison == Comparison.NotEqual) {
            return readValue != value;
        }
        if (comparison == Comparison.LessThan) {
            return readValue < value;
        }
        if (comparison == Comparison.GreaterThan) {
            return readValue > value;
        }
        return defaultResult;
    }

    public enum Comparison { Equal, NotEqual, LessThan, GreaterThan };
    public Comparison comparison;
    public int inventorySlot;
    public int value;
    public bool defaultResult;
}
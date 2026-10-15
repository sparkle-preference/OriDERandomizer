public class RandomizerInputLockedCondition : Condition {
    public override bool Validate(IContext context) {
        return GameController.Instance.InputLocked == this.InputLocked;
    }

    public RandomizerInputLockedCondition() {
    }

    public bool InputLocked;
}

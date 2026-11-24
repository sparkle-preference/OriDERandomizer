public class RandomizerIsTriggerActiveCondition : Condition {
    public override bool Validate(IContext context) {
        return Target.Active == Active;
    }

    public Trigger Target;

    public bool Active;
}

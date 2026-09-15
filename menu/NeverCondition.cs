// For rows that exist to be read rather than chosen.
public class NeverCondition : Condition {
    public override bool Validate(IContext context) {
        return false;
    }
}

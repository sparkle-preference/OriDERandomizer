public class RandomizerHasCoordsCondition : Condition {
    public int coordsKey;

    public override bool Validate(IContext context) {
        return Randomizer.HaveCoord(coordsKey);
    }
}

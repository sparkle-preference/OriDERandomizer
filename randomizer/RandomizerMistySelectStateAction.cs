#nullable enable

public class RandomizerMistySelectStateAction : ActionMethod {
    public override void Perform(IContext context) {
        var state = Randomizer.MistyState;
        foreach (var stateCase in Cases)
        {
            if (stateCase.CurrentState is {} currentState && state.Value != currentState) {
                continue;
            }

            if (stateCase.HasCoords is {} coords && !Randomizer.HaveCoord(coords)) {
                continue;
            }

            state.Value = stateCase.NewState;
            return;
        }

        Randomizer.LogError($"Misty Select State Action {this} didn't match any case");
    }

    public StateCase[] Cases = [];

    public class StateCase(int? currentState, int? hasCoords, int newState) {
        public int? CurrentState = currentState;
        public int? HasCoords = hasCoords;
        public int NewState = newState;
    }
}

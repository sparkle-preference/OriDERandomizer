public class ShowWorldMapObjectiveAction : PerformingAction {
    public override void Perform(IContext context) {
    }

    public override void Stop() {
    }

    public override bool IsPerforming => isPerforming;

    public void OnFinish() {
        isPerforming = false;
    }

    public Objective Objective;

    private bool isPerforming;
}

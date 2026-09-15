using UnityEngine;

public class OptionsScreenLegendController : MonoBehaviour {
    private void Update() {
        GeneralLegend.Initialize();
        // No leaderboards screen means no Instance; the legend then behaves as it does
        // whenever that screen is not the one showing.
        var leaderboards = LeaderboardsB.Instance;
        if (leaderboards != null && leaderboards.IsVisible) {
            GeneralLegend.AnimatorDriver.ContinueBackwards();
        } else {
            GeneralLegend.AnimatorDriver.ContinueForward();
        }
    }

    public TransparencyAnimator GeneralLegend;
}

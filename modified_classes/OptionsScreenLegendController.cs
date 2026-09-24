using UnityEngine;

public class OptionsScreenLegendController : MonoBehaviour {
    private void Update() {
        GeneralLegend.Initialize();
        // no leaderboards Instance behaves as leaderboards not showing
        var leaderboards = LeaderboardsB.Instance;
        if (leaderboards != null && leaderboards.IsVisible) {
            GeneralLegend.AnimatorDriver.ContinueBackwards();
        } else {
            GeneralLegend.AnimatorDriver.ContinueForward();
        }
    }

    public TransparencyAnimator GeneralLegend;
}

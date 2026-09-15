using UnityEngine;

public class OptionsScreenLegendController : MonoBehaviour {
    private void Update() {
        if (LeaderboardsB.Instance.IsVisible) {
            GeneralLegend.Initialize();
            GeneralLegend.AnimatorDriver.ContinueBackwards();
        } else {
            GeneralLegend.Initialize();
            GeneralLegend.AnimatorDriver.ContinueForward();
        }
    }

    public TransparencyAnimator GeneralLegend;
}

using UnityEngine;

public class RandomizerMistyAnimationFixAction : ActionMethod {
    public override void Perform(IContext context) {
        if (m_originalCurve == null) {
            m_originalCurve = Animator.AnimationCurve;
        }

        Animator.AnimationCurve = Randomizer.MistyState.Value == 2 ? m_constantCurve : m_originalCurve;
    }

    public CameraSettingsZoneAnimator Animator;

    private AnimationCurve m_originalCurve;

    private AnimationCurve m_constantCurve = AnimationCurve.Linear(0f, 1f, 0f, 1f);
}

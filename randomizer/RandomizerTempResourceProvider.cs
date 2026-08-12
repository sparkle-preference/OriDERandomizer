using Game;
using UnityEngine;

// Feeds the split health/energy display (see RandomizerTempResourceUI): base
// layers capped at the permanent max, the temp row showing only the over-max
// (temporary) amount. UseMinVisual mirrors the vanilla Min/Max provider pair.
public class RandomizerTempResourceProvider : FloatValueProvider {
    public bool Energy;

    public bool Overflow;

    public bool UseMinVisual;

    public float DivideBy = 1f;

    private float smoothed;

    private bool smoothedInit;

    public override float GetFloatValue() {
        if (Characters.Sein == null) {
            return 0f;
        }

        float visual, current, max;
        if (Energy) {
            visual = UseMinVisual ? Characters.Sein.Energy.MinVisual : Characters.Sein.Energy.MaxVisual;
            current = Characters.Sein.Energy.Current;
            max = Characters.Sein.Energy.Max;
        } else {
            visual = UseMinVisual ? Characters.Sein.Mortality.Health.VisualMinAmount : Characters.Sein.Mortality.Health.VisualMaxAmount;
            current = Characters.Sein.Mortality.Health.Amount;
            max = Characters.Sein.Mortality.Health.MaxHealth;
        }

        if (RandomizerSettings.Customization.DisableTempResourceRows) {
            // vanilla display: base layers uncapped, temp rows empty
            smoothedInit = false;
            return (Overflow ? 0f : visual) / DivideBy;
        }

        if (!Overflow) {
            return Mathf.Min(visual, max) / DivideBy;
        }

        // the temp row tracks true damage in a few frames instead of the slow
        // vanilla visual settle -- repeated hits must read at a glance. Gains
        // snap, like the vanilla pickup feel.
        var target = Mathf.Max(0f, current - max);
        if (!smoothedInit || target > smoothed) {
            smoothedInit = true;
            smoothed = target;
        } else {
            smoothed = Mathf.MoveTowards(smoothed, target, Mathf.Max((smoothed - target) * 0.5f, DivideBy * 0.25f));
        }

        return smoothed / DivideBy;
    }
}

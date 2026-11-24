using Game;
using UnityEngine;

// Feeds RandomizerTempResourceUI: base layers capped at the permanent max, temp rows the overflow.
// UseMinVisual mirrors the vanilla Min/Max provider pair.
public class RandomizerTempResourceProvider : FloatValueProvider {
    public bool Energy;

    public bool Overflow;

    public bool UseMinVisual;

    // legacy mode's dimmed copy of the whole bar, drawn behind the capped one
    public bool Inline;

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
            if (Overflow) {
                smoothedInit = false;
                return 0f;
            }

            // inline: the base stops at max when the dimmed copy behind it is up, else draws it all
            if (!Inline) {
                return (RandomizerTempResourceUI.InlineReady(Energy) ? Mathf.Min(visual, max) : visual) / DivideBy;
            }
        } else if (!Overflow) {
            return Mathf.Min(visual, max) / DivideBy;
        }

        // the temp row settles on damage in a few frames, not the slow vanilla settle; gains snap
        var target = Inline ? (current > max ? current : 0f) : Mathf.Max(0f, current - max);
        if (!smoothedInit || target > smoothed) {
            smoothedInit = true;
            smoothed = target;
        } else {
            smoothed = Mathf.MoveTowards(smoothed, target, Mathf.Max((smoothed - target) * 0.5f, DivideBy * 0.25f));
        }

        return smoothed / DivideBy;
    }
}

using UnityEngine;

// A filling ring for hold-to-confirm gestures, borrowed from the loading bar: a soul-flame
// circle with an authored 0..1 fill that lives from boot rather than with Sein, which is what
// lets a menu the title screen can open draw one.
public static class RandomizerHoldRing {
    public const float Seconds = 0.6f;

    // Past this the press is a hold under way, so letting go early means neither -- a hesitant
    // press should not leave a key bound behind it.
    public const float Tap = 0.2f;

    // Any of these can be the channel a clone was left invisible on. With only _Color you get
    // the one child that never moves and none of the animated halves.
    private static readonly string[] Alphas = {
        "_Color", "_TintColor", "_MaskDissolveColor", "_AdditiveLayerColor"
    };

    // by eye against a key glyph in the legend
    private const float Size = 0.8f;

    private const float Lift = 0.1f;

    public static void Draw(Renderer over, float progress) {
        var ring = Ring(over);
        if (ring == null) {
            return;
        }

        ring.SetActive(true);
        var at = over.transform.position;
        ring.transform.position = new Vector3(at.x, at.y, at.z - Lift);
        ring.transform.localScale = new Vector3(Size, Size, 1f);
        if (fill != null) {
            fill.SampleValue(Mathf.Clamp01(progress), true);
        }
    }

    public static void Hide() {
        if (ring != null) {
            ring.SetActive(false);
        }
    }

    // The driver reads the real load progress, so it goes before the timeline is ours. Losing
    // it also leaves BaseAnimator.AnimatorDriver handing back a fresh driver with no animator
    // bound, which samples nothing and reports nothing -- sample the sequence itself.
    private static GameObject Ring(Renderer over) {
        if (ring != null) {
            return ring;
        }

        if (missing) {
            return null;
        }

        var source = Source();
        if (source == null) {
            missing = true;
            Randomizer.log("hold ring: no loading bar to borrow; holds will have no ring");
            return null;
        }

        ring = (GameObject)Object.Instantiate(source);
        ring.name = "randomizerHoldRing";
        var driver = ring.GetComponent<FloatProviderAnimatorDriver>();
        if (driver != null) {
            Object.DestroyImmediate(driver);
        }

        fill = ring.GetComponent<TimelineSequence>();
        foreach (var renderer in ring.GetComponentsInChildren<Renderer>(true)) {
            renderer.enabled = true;
            renderer.gameObject.layer = over.gameObject.layer;
            renderer.sortingLayerID = over.sortingLayerID;
            renderer.sortingOrder = over.sortingOrder + 1;
            Opaque(renderer.material);
        }

        return ring;
    }

    private static void Opaque(Material material) {
        if (material == null) {
            return;
        }

        foreach (var name in Alphas) {
            if (material.HasProperty(name)) {
                var color = material.GetColor(name);
                material.SetColor(name, new Color(color.r, color.g, color.b, 1f));
            }
        }
    }

    // The bar carries the provider that reads the prewarmer, which is what names it among the
    // things loaded at boot.
    private static GameObject Source() {
        foreach (var progress in Resources.FindObjectsOfTypeAll<UberShaderPrewarmerProgress>()) {
            if (progress != null && progress.GetComponent<TimelineSequence>() != null) {
                return progress.gameObject;
            }
        }

        return null;
    }

    private static GameObject ring;

    private static TimelineSequence fill;

    private static bool missing;
}

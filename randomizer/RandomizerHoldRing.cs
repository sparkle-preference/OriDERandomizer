using UnityEngine;

// Borrowed game art driven as a progress fill: clone it, remove its driver, force it visible,
// sample its timeline. The map warp ring and the menus' hold gestures use it.
public class RandomizerHoldRing {
    // hold length, and the longest press that is still a tap; a press between the two is neither
    public const float Seconds = 0.6f;

    public const float Tap = 0.2f;

    // one ring for every legend's held hint, as only one hold runs at a time
    private static RandomizerHoldRing shared;

    private static bool ringless;

    // the glyph the shared ring was last sorted against
    private static Renderer sortedBy;

    // by eye against a key glyph: the ring reads as around it rather than behind it
    private const float GlyphSpan = 1.875f;

    private const float GlyphLift = 0.1f;

    // Fills the shared ring round a legend's key glyph.
    public static void Around(Renderer glyph, float progress) {
        if (glyph == null || ringless) {
            return;
        }

        // the holder is a plain object and never goes null with the scene its clone was in
        if (shared == null || shared.Object == null) {
            shared = new RandomizerHoldRing();
            if (!shared.Adopt(LoadingBar(), null, "randomizerHoldRing")) {
                Randomizer.log("hold ring: no loading bar to borrow; holds will have no ring");
                shared = null;
                ringless = true;
                return;
            }

            shared.Fade(1f);
            sortedBy = null;
        }

        if (glyph != sortedBy) {
            sortedBy = glyph;
            shared.Match(glyph, glyph.gameObject.layer);
        }

        shared.Show(true);
        var at = glyph.transform.position;
        shared.Place(new Vector3(at.x, at.y, at.z - GlyphLift));
        shared.Widen(glyph.bounds.size.y * GlyphSpan);
        shared.Progress(progress);
    }

    public static void Hide() {
        if (shared != null) {
            shared.Show(false);
        }
    }

    // A legend slot's leftmost key glyph, as a held hint's key is written first. Not the first child:
    // icon clones keep their creation order when the text changes.
    public static Renderer Glyph(Transform slot) {
        var icons = slot == null ? null : slot.GetComponentInChildren<CatlikeCoding.TextBox.MoonIconRenderer>(true);
        if (icons == null) {
            return null;
        }

        Renderer leftmost = null;
        foreach (var renderer in icons.GetComponentsInChildren<Renderer>(true)) {
            if (leftmost == null || renderer.transform.position.x < leftmost.transform.position.x) {
                leftmost = renderer;
            }
        }

        return leftmost;
    }

    // the boot loading bar: the one soul-flame ring alive outside gameplay
    private static GameObject LoadingBar() {
        foreach (var progress in Resources.FindObjectsOfTypeAll<UberShaderPrewarmerProgress>()) {
            if (progress != null && progress.GetComponent<TimelineSequence>() != null) {
                return progress.gameObject;
            }
        }

        return null;
    }

    // any of these may carry a clone's zero alpha; only the alpha is overruled, never the color
    private static readonly string[] Alphas = {
        "_Color", "_TintColor", "_MaskDissolveColor", "_AdditiveLayerColor"
    };

    public GameObject Object { get; private set; }

    // Clone and take manual control: the driver goes and the timeline is sampled with force,
    // since AnimatorDriver.Sample skips a scrub to where the animator thinks it already is.
    public bool Adopt(GameObject source, Transform parent, string name) {
        if (source == null) {
            return false;
        }

        Object = (GameObject)UnityEngine.Object.Instantiate(source);
        Object.name = name;
        if (parent != null) {
            Object.transform.parent = parent;
        }

        foreach (var driver in Object.GetComponentsInChildren<FloatProviderAnimatorDriver>(true)) {
            UnityEngine.Object.DestroyImmediate(driver);
        }

        RandomizerGhost.Quiet(Object);
        foreach (var renderer in Object.GetComponentsInChildren<Renderer>(true)) {
            renderer.enabled = true;
            Collect(renderer.material);
        }

        foreach (var animator in Object.GetComponentsInChildren<BaseAnimator>(true)) {
            if (animator is TimelineSequence) {
                fill = animator;
                break;
            }
        }

        natural = Object.transform.localScale;
        Progress(0f);
        return true;
    }

    // Sorted just over `sorting`; the layer is separate because the two do not always agree.
    public void Match(Renderer sorting, int layer) {
        if (Object == null) {
            return;
        }

        foreach (var renderer in Object.GetComponentsInChildren<Renderer>(true)) {
            renderer.gameObject.layer = layer;
            if (sorting != null) {
                renderer.sortingLayerID = sorting.sortingLayerID;
                renderer.sortingOrder = sorting.sortingOrder + 1;
            }
        }
    }

    public void Show(bool on) {
        if (Object != null) {
            Object.SetActive(on);
        }
    }

    public void Place(Vector3 at) {
        if (Object != null) {
            Object.transform.position = at;
        }
    }

    // Scaled so the ring itself spans this, rather than the glow and backdrop around it.
    public void Widen(float span) {
        if (Object == null || Span <= 0.0001f) {
            return;
        }

        Object.transform.localScale = natural * (span / Span);
    }

    public void Progress(float t) {
        if (fill != null) {
            fill.SampleValue(Mathf.Clamp01(t) * Full, true);
        }
    }

    public void Fade(float alpha) {
        for (var i = 0; i < paints.Count; i++) {
            var color = paints[i].GetColor(keys[i]);
            paints[i].SetColor(keys[i], new Color(color.r, color.g, color.b, alpha));
        }
    }

    // Width of the ring halves, not the glow. Read only while shown: hidden renderers have no
    // bounds, and the fallback it then caches sticks.
    public float Span {
        get {
            if (span > 0.0001f || Object == null) {
                return span;
            }

            Object.transform.localScale = natural;
            var widest = 0f;
            var ring = 0f;
            foreach (var renderer in Object.GetComponentsInChildren<Renderer>(true)) {
                if (renderer == null) {
                    continue;
                }

                if (renderer.bounds.size.x > widest) {
                    widest = renderer.bounds.size.x;
                }

                var material = renderer.sharedMaterial;
                var texture = material == null ? null : material.mainTexture;
                if (texture != null && texture.name == "soulflameCircle" &&
                        renderer.bounds.size.x > ring) {
                    ring = renderer.bounds.size.x;
                }
            }

            ring = ring > 0.0001f ? ring : widest;
            // a last resort that keeps it on screen rather than microscopic
            span = ring > 0.0001f ? ring : natural.x;
            return span;
        }
    }

    // timeline position where the fill is full; any flourish after it goes unused
    public float Full = 1f;

    private void Collect(Material material) {
        if (material == null) {
            return;
        }

        foreach (var name in Alphas) {
            if (material.HasProperty(name)) {
                paints.Add(material);
                keys.Add(name);
            }
        }
    }

    private BaseAnimator fill;

    private Vector3 natural = Vector3.one;

    private float span;

    private readonly System.Collections.Generic.List<Material> paints =
        new System.Collections.Generic.List<Material>();

    private readonly System.Collections.Generic.List<string> keys =
        new System.Collections.Generic.List<string>();
}

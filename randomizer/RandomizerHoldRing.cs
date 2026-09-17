using UnityEngine;

// A piece of the game's own art, borrowed and scrubbed as a fill: clone it, take the driver
// that normally feeds it away, force it visible, and sample its timeline at a progress. The
// map's warp ring and the settings screens' hold gesture are this recipe over different art.
public class RandomizerHoldRing {
    // How long a hold runs, and how long a press may be before it counts as one. Past the tap
    // window and let go early is neither, so a hesitant press leaves nothing behind it.
    public const float Seconds = 0.6f;

    public const float Tap = 0.2f;

    // Any of these can be the channel a clone was left invisible on, and with only _Color you
    // get the parts that never move and none of the rest. The widget is many colors, so only
    // the alpha is overruled -- flattening it to one tint throws away the thing worth having.
    private static readonly string[] Alphas = {
        "_Color", "_TintColor", "_MaskDissolveColor", "_AdditiveLayerColor"
    };

    public GameObject Object { get; private set; }

    // Clone and take manual control. The driver has to go -- it is reading whatever the art was
    // built to follow -- and from then on the timeline is sampled directly, because
    // AnimatorDriver.Sample asks for forceSample false and skips a scrub that lands where the
    // animator believes it already is.
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

    // Drawn where the reference draws, and over it. The layer is passed separately because the
    // thing whose sorting is worth copying is not always on the layer its group sits on.
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

    // Hidden while it plays and an inactive renderer has no bounds, so the size is not knowable
    // until the frame it is first shown. The ring halves are the ring; everything else is glow
    // and background reaching well past it.
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

    // How much of the timeline the fill occupies; the rest is whatever flourish the art plays
    // once it is full, which a hold has no use for.
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

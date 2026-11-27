using UnityEngine;

// Over a ghost whose player is in a menu: a brush glyph for pause, map and title, or their skill tree,
// which bursts on a level-up. It follows position only, so Ori's flip and scale stay out of it.
public class RandomizerGhostBadge {
    public enum Kind { None, Pause, Map, Tree, Title }

    // Statics, so the look can be tuned live. World units: Height is one skill-tree icon tall.
    internal static float Height = 1.46f;

    // the badge's bottom edge, over the ghost's center
    internal static float Lift = 0.83f;

    // the glyphs' paint; full white blooms away the map's folds
    internal static float Paper = 0.85f;

    internal static float Sideways = 0f;

    internal static float Opacity = 1f;

    internal static float FlashLength = 1.5f;

    // 1 is the skill menu's own gain effect, scaled to a dot
    internal static float BurstScale = 1f;

    // the tree's lines and glow, over what the skill menu draws them at
    internal static float TreeStrength = 2.5f;

    // the tree's lines and dots past their own color; their opacity is already full
    internal static float TreeBrightness = 1.2f;

    // below 0 the red lane is drawn as the menu has it; otherwise every red is pulled to this hue (degrees)
    internal static float RedHue = 20f;

    // the red lane alone, past its own color, once its hue is pulled
    internal static float RedLift = 1.3f;

    // A dark backing behind the tree, as the briefing overlay has: its outline grown and feathered, baked by
    // build_tree_shadow.py from Layers as a square OutlineSpan tree heights wide about the tree's center.
    internal static float TreeShadow = 0.65f;

    private const float OutlineSpan = 1.4f;

    // the tree sits this much higher than the glyphs
    internal static float TreeRaise = 0.1f;

    public RandomizerGhostBadge(Color shade, Renderer order) {
        Shade = shade;
        Order = order;
    }

    // Called every tick the ghost is shown; None lets the badge fade out with what it last showed.
    public void Show(Kind kind, uint skills) {
        Want = kind != Kind.None;
        if (kind == Kind.None) {
            return;
        }

        if (kind != Showing) {
            Showing = kind;
            Dirty = true;
        }

        if (kind == Kind.Tree && skills != Skills) {
            Skills = skills;
            Dirty = true;
        }
    }

    // A new level-up cancels whatever burst is running.
    public void Flash(int bit) {
        if (bit < 0 || bit >= RandomizerGhost.TreeSkills) {
            return;
        }

        FlashBit = bit;
        FlashStart = Time.time;
        var random = new System.Random(bit * 7919 + Mathf.RoundToInt(Time.time * 1000f));
        for (var i = 0; i < Sparks.Length; i++) {
            Sparks[i] = new Vector3((float)(random.NextDouble() * Mathf.PI * 2),
                2f + 3f * (float)random.NextDouble(), (float)random.NextDouble());
        }
    }

    // Every frame: steps the fade, follows the ghost, repaints when anything changed.
    public void Step(Vector3 ghost, float outer, float fadeIn, float fadeOut) {
        var beat = Want ? fadeIn : fadeOut;
        var target = Want ? 1f : 0f;
        Alpha = beat > 0.001f ? Mathf.MoveTowards(Alpha, target, Time.deltaTime / beat) : target;
        if (Alpha <= 0f && !Want) {
            if (Root != null && Root.activeSelf) {
                Root.SetActive(false);
            }

            return;
        }

        if (!Build()) {
            return;
        }

        if (!Root.activeSelf) {
            Root.SetActive(true);
            Dirty = true;
        }

        var lift = Lift + (Showing == Kind.Tree ? TreeRaise : 0f);
        Root.transform.position = ghost + new Vector3(Sideways, lift, -0.01f);
        Root.transform.rotation = Quaternion.identity;
        var alpha = Alpha * outer * Opacity;
        var flashing = Showing == Kind.Tree && FlashBit >= 0 && Time.time - FlashStart <= FlashLength;
        if (Dirty || flashing || FlashShown || Mathf.Abs(alpha - Painted) > 0.002f) {
            Paint(alpha, flashing);
        }
    }

    public void Hide() {
        Alpha = 0f;
        if (Root != null) {
            Root.SetActive(false);
        }
    }

    public void Drop() {
        if (Root != null) {
            Object.Destroy(Root);
        }

        Root = null;
    }

    private bool Build() {
        if (Root != null) {
            return true;
        }

        if (Order == null) {
            return false;
        }

        Root = new GameObject("ghostBadge");
        Object.DontDestroyOnLoad(Root);
        GlyphGlow = Quad(Root, "glow", Glyph("ghost_pause_glow.png"), 0f);
        GlyphCore = Quad(Root, "core", Glyph("ghost_pause.png"), -0.002f);
        var size = GlyphFrame * Height;
        foreach (var quad in new[] { GlyphGlow, GlyphCore }) {
            if (quad != null) {
                quad.transform.localPosition = new Vector3(0f, Height / 2f, quad.transform.localPosition.z);
                quad.transform.localScale = new Vector3(size, size, 1f);
            }
        }

        Dirty = true;
        return true;
    }

    private const string DepthFlip = "_DepthFlipScreen";

    private GameObject Quad(GameObject parent, string name, Texture2D texture, float z) {
        var quad = RandomizerQuad.BuildTextured(name, texture, Order);
        if (quad == null) {
            return null;
        }

        // Terrain's depth reaches past its art and would cut the badge, so it draws with no depth test.
        var renderer = quad.GetComponent<Renderer>();
        if (OverShader == null) {
            OverShader = Shader.Find("UI/Default");
        }

        if (OverShader != null) {
            var over = new Material(OverShader);
            over.mainTexture = texture;
            over.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
            over.SetVector("_ClipRect", new Vector4(-1e7f, -1e7f, 1e7f, 1e7f));
            renderer.sharedMaterial = over;
            // and after all of the world's art, the foreground's included
            renderer.sortingLayerName = "farForeground";
            renderer.sortingOrder = 32000;
        } else if (Order.sharedMaterial != null && Order.sharedMaterial.HasProperty(DepthFlip)) {
            // the hint material's GUI depth sinks the quad behind any scenery that writes depth
            renderer.sharedMaterial.SetVector(DepthFlip, Order.sharedMaterial.GetVector(DepthFlip));
        }

        quad.transform.parent = parent.transform;
        quad.transform.localPosition = new Vector3(0f, 0f, z);
        quad.transform.localRotation = Quaternion.identity;
        return quad;
    }

    private static Shader OverShader;

    // The game's quad material reads _Color at double strength, UI/Default at single: Tint 1 is the texture as it is.
    // The burst at double strength, standing in for the menu effect's additive glow.
    private static void Glare(GameObject quad, Color color) {
        Apply(quad, color * 2f);
    }

    internal static void Tint(GameObject quad, Color color) {
        Apply(quad, color);
    }

    private static void Apply(GameObject quad, Color color) {
        if (quad != null) {
            var renderer = quad.GetComponent<Renderer>();
            var single = OverShader != null && renderer.sharedMaterial.shader == OverShader;
            UberShaderAPI.SetColor(renderer, single ? color : color * 0.5f, true);
        }
    }

    private void Paint(float alpha, bool flashing) {
        Painted = alpha;
        Dirty = false;
        var glyph = Showing != Kind.Tree;
        if (glyph && GlyphTexture != Showing) {
            GlyphTexture = Showing;
            var name = Showing == Kind.Map ? "ghost_map" : (Showing == Kind.Title ? "ghost_title" : "ghost_pause");
            Retexture(GlyphGlow, Glyph(name + "_glow.png"));
            Retexture(GlyphCore, Glyph(name + ".png"));
        }

        SetActive(GlyphGlow, glyph);
        SetActive(GlyphCore, glyph);
        Tint(GlyphGlow, new Color(Shade.r, Shade.g, Shade.b, alpha));
        Tint(GlyphCore, new Color(Paper, Paper, Paper, alpha));

        if (!glyph) {
            BuildTree();
        }

        if (TreeObject != null) {
            TreeObject.SetActive(!glyph);
            if (!glyph) {
                PaintTree(alpha, flashing ? (Time.time - FlashStart) / FlashLength : -1f);
            }
        }

        FlashShown = flashing;
    }

    private static void Retexture(GameObject quad, Texture2D texture) {
        if (quad != null && texture != null) {
            quad.GetComponent<Renderer>().material.SetTexture("_MainTex", texture);
        }
    }

    private static void SetActive(GameObject target, bool active) {
        if (target != null && target.activeSelf != active) {
            target.SetActive(active);
        }
    }

    // ---- the skill tree ----

    // The skill menu's lane art, in units of the badge's height, centered on the badge.
    internal const float TreeAspect = 1.0621f;

    internal struct Layer {
        public string Path;
        public float X;
        public float Y;
        public float Size;
        public float Alpha;
    }

    internal static readonly Layer[] Layers = {
        new Layer { Path = "templateAs/treeBackground", X = -0.0283f, Y = -0.0031f, Size = 1.0941f, Alpha = 0.4f },
        new Layer { Path = "templateBs/treeBackground", X = -0.0283f, Y = -0.0031f, Size = 1.0941f, Alpha = 0.4f },
        new Layer { Path = "templateCs/treeBackgroundA", X = -0.0283f, Y = -0.0031f, Size = 1.0941f, Alpha = 0.4f },
        new Layer { Path = "templateCs/treeBackgroundB", X = -0.0283f, Y = -0.0031f, Size = 1.0941f, Alpha = 0.4f },
        new Layer { Path = "templateAs/treeLines", X = -0.0263f, Y = -0.0034f, Size = 1.0095f, Alpha = 0.6f },
        new Layer { Path = "templateBs/treeLines", X = -0.0263f, Y = -0.0034f, Size = 1.0095f, Alpha = 0.6f },
        new Layer { Path = "templateCs/treeLinesA", X = 0.0263f, Y = -0.0023f, Size = 1.0095f, Alpha = 0.6f },
        new Layer { Path = "templateCs/treeLinesB", X = -0.0037f, Y = -0.0043f, Size = 1.0095f, Alpha = 0.6f }
    };

    // each skill's ring, in RandomizerGhost.TreeOrder
    internal static readonly float[] Rings = {
        0.2233f, -0.3902f, 0.0826f, -0.3958f, -0.0568f, -0.3849f, -0.1822f, -0.3557f, -0.3019f, -0.3022f,
        -0.3902f, -0.2136f, -0.4473f, -0.0966f, -0.4660f, 0.0430f, -0.4494f, 0.1927f, -0.4027f, 0.3280f,
        0.2014f, -0.3159f, 0.0716f, -0.3054f, -0.0495f, -0.2744f, -0.1612f, -0.2007f, -0.2352f, -0.0978f,
        -0.2545f, 0.0354f, -0.2315f, 0.1609f, -0.1692f, 0.2675f, -0.0709f, 0.3474f, 0.0507f, 0.3958f,
        0.2485f, -0.2403f, 0.1140f, -0.2081f, 0.0145f, -0.1126f, 0.0007f, 0.0188f, 0.0677f, 0.1366f,
        0.1898f, 0.1984f, 0.3285f, 0.2089f, 0.4479f, 0.1563f, 0.4470f, 0.0329f, 0.3191f, 0.0078f
    };

    internal static readonly Color[] Lanes = {
        new Color(251f / 255f, 50f / 255f, 42f / 255f),
        new Color(210f / 255f, 142f / 255f, 250f / 255f),
        new Color(94f / 255f, 228f / 255f, 253f / 255f)
    };

    internal static Color Lit(Color lane, float lift) {
        return new Color(Mathf.Min(1f, lane.r + lift), Mathf.Min(1f, lane.g + lift), Mathf.Min(1f, lane.b + lift), 1f);
    }

    private void BuildTree() {
        if (TreeObject != null || Root == null) {
            return;
        }

        TreeObject = new GameObject("tree");
        TreeObject.transform.parent = Root.transform;
        TreeObject.transform.localPosition = new Vector3(0f, Height / 2f, 0f);
        TreeObject.transform.localRotation = Quaternion.identity;
        Shadow = Quad(TreeObject, "shadow", Glyph("ghost_tree_shadow.png"), 0.004f);
        if (Shadow != null) {
            Shadow.transform.localScale = new Vector3(OutlineSpan * Height, OutlineSpan * Height, 1f);
        }

        LayerQuads = new GameObject[Layers.Length];
        for (var i = 0; i < Layers.Length; i++) {
            var layer = Layers[i];
            var texture = MenuTexture(layer.Path);
            if (texture == null) {
                continue;
            }

            LayerQuads[i] = Quad(TreeObject, layer.Path, texture, i < 4 ? 0f : -0.002f);
            if (LayerQuads[i] != null) {
                LayerQuads[i].transform.localPosition = new Vector3(layer.X * Height, layer.Y * Height, LayerQuads[i].transform.localPosition.z);
                LayerQuads[i].transform.localScale = new Vector3(layer.Size * Height, layer.Size * Height, 1f);
            }
        }

        var dotSize = DotSpan * TreeAspect * Height;
        Dots = new GameObject[RandomizerGhost.TreeSkills];
        for (var bit = 0; bit < Dots.Length; bit++) {
            Dots[bit] = Quad(TreeObject, "skill" + bit, Glyph("ghost_dot.png"), -0.004f);
            if (Dots[bit] != null) {
                Dots[bit].transform.localPosition = new Vector3(Rings[bit * 2] * Height, Rings[bit * 2 + 1] * Height, -0.004f);
                Dots[bit].transform.localScale = new Vector3(dotSize, dotSize, 1f);
            }
        }
    }

    // a lit dot and its glow, as wide as the tree times this
    internal const float DotSpan = 2f * 2.6f * 0.024f;

    private static Color Lane(Color color, bool red) {
        return red ? new Color(color.r * RedLift, color.g * RedLift, color.b * RedLift, color.a) : color;
    }

    // the red lane's layer textures with every red pulled to RedHue, made once per hue
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Warmed =
        new System.Collections.Generic.Dictionary<string, Texture2D>();

    private static Texture2D Warm(Texture2D source) {
        if (source == null || RedHue < 0f) {
            return source;
        }

        var key = source.name + "@" + RedHue;
        Texture2D done;
        if (Warmed.TryGetValue(key, out done) && done != null) {
            return done;
        }

        // the menu's textures aren't readable, so they're copied through a render texture
        var film = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, film);
        var was = RenderTexture.active;
        RenderTexture.active = film;
        done = new Texture2D(source.width, source.height, TextureFormat.ARGB32, true);
        done.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(film);
        var pixels = done.GetPixels32();
        for (var i = 0; i < pixels.Length; i++) {
            if (pixels[i].a > 0) {
                pixels[i] = Hued(pixels[i], RedHue);
            }
        }

        done.SetPixels32(pixels);
        done.wrapMode = TextureWrapMode.Clamp;
        done.Apply(true, true);
        Warmed[key] = done;
        return done;
    }

    // a hue within 30 degrees of red is set to hue (fading by 40); saturation, value and alpha stay
    private static Color Hued(Color pixel, float hue) {
        var max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
        var span = max - Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
        if (span <= 0f) {
            return pixel;
        }

        var signed = 60f * (max == pixel.r ? (pixel.g - pixel.b) / span
            : max == pixel.g ? (pixel.b - pixel.r) / span + 2f : (pixel.r - pixel.g) / span + 4f);
        signed = signed > 180f ? signed - 360f : signed;
        var near = Mathf.Abs(signed);
        if (near >= 40f) {
            return pixel;
        }

        signed += (hue - signed) * Mathf.Min(1f, (40f - near) / 10f);
        var h = (signed + 360f) % 360f / 60f;
        var s = span / max;
        return new Color(Channel(5f, h, s, max), Channel(3f, h, s, max), Channel(1f, h, s, max), pixel.a);
    }

    private static float Channel(float n, float h, float s, float v) {
        var k = (n + h) % 6f;
        return v - v * s * Mathf.Max(0f, Mathf.Min(k, Mathf.Min(4f - k, 1f)));
    }

    private void PaintTree(float alpha, float flash) {
        SetActive(Shadow, TreeShadow > 0f);
        if (TreeShadow > 0f) {
            Tint(Shadow, new Color(0f, 0f, 0f, Mathf.Min(1f, alpha * TreeShadow)));
        }

        if (PaintedRedHue != RedHue) {
            PaintedRedHue = RedHue;
            for (var i = 0; i < Layers.Length; i++) {
                if (Layers[i].Path.StartsWith("templateAs/")) {
                    Retexture(LayerQuads[i], Warm(MenuTexture(Layers[i].Path)));
                }
            }
        }

        var shine = TreeBrightness;
        for (var i = 0; i < Layers.Length; i++) {
            var red = Layers[i].Path.StartsWith("templateAs/");
            Tint(LayerQuads[i], Lane(new Color(shine, shine, shine, Mathf.Min(1f, alpha * Layers[i].Alpha * TreeStrength)), red));
        }

        for (var bit = 0; bit < Dots.Length; bit++) {
            var lit = (Skills & (1u << bit)) != 0;
            SetActive(Dots[bit], lit);
            if (!lit) {
                continue;
            }

            var color = Lit(Lanes[bit / 10], 60f / 255f);
            if (bit < 10 && RedHue >= 0f) {
                color = Hued(color, RedHue);
            }

            if (bit == FlashBit && flash >= 0f) {
                // a freshly leveled dot burns white, then settles into its lane's color
                color = Color.Lerp(color, Color.white, Mathf.Max(0f, 1f - flash / 0.35f));
            }

            color = new Color(color.r * shine, color.g * shine, color.b * shine, alpha);
            Tint(Dots[bit], Lane(color, bit < 10));
        }

        Burst(alpha, flash);
    }

    // ---- the level-up burst: the skill menu's gain effect, scaled to one dot ----

    private void Burst(float alpha, float t) {
        if (t < 0f) {
            if (BurstObject != null && BurstObject.activeSelf) {
                BurstObject.SetActive(false);
            }

            return;
        }

        BuildBurst();
        if (BurstObject == null) {
            return;
        }

        BurstObject.SetActive(true);
        BurstObject.transform.localPosition = new Vector3(Rings[FlashBit * 2] * Height, Rings[FlashBit * 2 + 1] * Height, -0.006f);
        var lane = Lanes[FlashBit / 10];
        var hot = Lit(lane, 70f / 255f);
        var s = IconSpan * TreeAspect * Height;
        var b = s * BurstScale;

        var grow = 1f + 0.8f * EaseOut(t / 0.25f);
        Effect(Rays, 12f * b * (0.85f + 0.15f * EaseOut(t / 0.4f)), 12f * b * (0.85f + 0.15f * EaseOut(t / 0.4f)),
            t * 0.4f * Mathf.Rad2Deg, hot, alpha * Envelope(t, 0.06f, 0.7f) * 0.85f);
        var rt = Mathf.Min(1f, t / 0.8f);
        var ring = (0.15f + 0.85f * EaseOut(rt)) * 11f * b;
        Effect(Ring, ring, ring, 0f, hot, alpha * (1f - rt) * Mathf.Min(1f, t / 0.05f) * 0.9f);
        Effect(Streak, 4.6f * b, 0.32f * b, 0f, Color.white, alpha * Envelope(t, 0.04f, 0.35f));
        Effect(LaneGlow, 2.4f * s * grow, 2.4f * s * grow, 0f, lane, alpha * Envelope(t, 0.06f, 0.9f) * 0.9f);
        Effect(WhiteGlow, 1.4f * s * grow, 1.4f * s * grow, 0f, Color.white, alpha * Envelope(t, 0.03f, 0.45f));
        for (var i = 0; i < SparkQuads.Length; i++) {
            if (SparkQuads[i] == null) {
                continue;
            }

            var spark = Sparks[i];
            var distance = spark.y * b * EaseOut(t * 1.3f);
            var size = (0.5f + 0.5f * spark.z) * s * 0.6f;
            SparkQuads[i].transform.localPosition = new Vector3(Mathf.Cos(spark.x) * distance, Mathf.Sin(spark.x) * distance, -0.002f);
            SparkQuads[i].transform.localScale = new Vector3(size, size, 1f);
            Glare(SparkQuads[i], new Color(hot.r, hot.g, hot.b, alpha * Mathf.Max(0f, 1f - t) * (0.6f + 0.4f * spark.z)));
        }
    }

    // one skill icon, as wide as the tree times this
    internal const float IconSpan = 0.065f;

    private static float EaseOut(float x) {
        x = Mathf.Clamp01(x);
        return 1f - (1f - x) * (1f - x) * (1f - x);
    }

    // rises to 1 by peak, back to 0 by end
    private static float Envelope(float t, float peak, float end) {
        if (t < 0f || t > end) {
            return 0f;
        }

        return t < peak ? t / peak : 1f - (t - peak) / (end - peak);
    }

    private static void Effect(GameObject quad, float width, float height, float spin, Color color, float alpha) {
        if (quad == null) {
            return;
        }

        quad.transform.localScale = new Vector3(width, height, 1f);
        quad.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
        Glare(quad, new Color(color.r, color.g, color.b, alpha));
    }

    private void BuildBurst() {
        if (BurstObject != null || TreeObject == null) {
            return;
        }

        BurstObject = new GameObject("burst");
        BurstObject.transform.parent = TreeObject.transform;
        BurstObject.transform.localRotation = Quaternion.identity;
        var glow = EffectTexture("iconLightBackground") ?? Glyph("ghost_dot.png");
        Rays = Quad(BurstObject, "rays", EffectTexture("radialLightRays"), 0f);
        Ring = Quad(BurstObject, "ring", EffectTexture("theGiftLeafTransformationLightRing"), -0.0005f);
        LaneGlow = Quad(BurstObject, "glow", glow, -0.001f);
        Streak = Quad(BurstObject, "streak", glow, -0.0015f);
        WhiteGlow = Quad(BurstObject, "core", glow, -0.002f);
        var spark = EffectTexture("sharedSingleSnowParticleA") ?? Glyph("ghost_dot.png");
        for (var i = 0; i < SparkQuads.Length; i++) {
            SparkQuads[i] = Quad(BurstObject, "spark", spark, -0.002f);
        }
    }

    // ---- textures ----

    // the brush glyphs and the dot ship in the dll; 352 px frames around a 256 px glyph
    private const float GlyphFrame = 352f / 256f;

    internal static Texture2D Glyph(string resource) {
        return RandomizerQuad.Texture(resource);
    }

    // The skill menu's own art, read off the live menu. Null until the menu exists.
    internal static Texture2D MenuTexture(string path) {
        var manager = SkillTreeManager.Instance;
        var lanes = manager == null ? null : Find(manager.transform.root, "treeLanes");
        var node = lanes == null ? null : lanes.Find(path);
        return TextureOf(node);
    }

    private static Texture2D EffectTexture(string name) {
        var manager = SkillTreeManager.Instance;
        var prefab = manager == null ? null : manager.GainSkillEffect;
        return prefab == null ? null : TextureOf(Find(prefab.transform, name));
    }

    internal static Texture2D TextureOf(Transform node) {
        var renderer = node == null ? null : node.GetComponent<Renderer>();
        return renderer == null || renderer.sharedMaterial == null ? null : renderer.sharedMaterial.mainTexture as Texture2D;
    }

    internal static Transform Find(Transform root, string name) {
        if (root.name == name) {
            return root;
        }

        foreach (Transform child in root) {
            var hit = Find(child, name);
            if (hit != null) {
                return hit;
            }
        }

        return null;
    }

    private readonly Color Shade;

    private readonly Renderer Order;

    private GameObject Root;

    private GameObject GlyphGlow;

    private GameObject GlyphCore;

    // which glyph the quads carry now
    private Kind GlyphTexture = Kind.Pause;

    private GameObject TreeObject;

    private GameObject Shadow;

    // the RedHue its red lane's textures were made for; NaN until the first paint
    private float PaintedRedHue = float.NaN;

    private GameObject[] LayerQuads;

    private GameObject[] Dots;

    private GameObject BurstObject;

    private GameObject Rays;

    private GameObject Ring;

    private GameObject Streak;

    private GameObject LaneGlow;

    private GameObject WhiteGlow;

    private readonly GameObject[] SparkQuads = new GameObject[10];

    // per spark: angle, reach in burst units, and a 0..1 roll for size and brightness
    private readonly Vector3[] Sparks = new Vector3[10];

    private Kind Showing = Kind.None;

    private uint Skills;

    private bool Want;

    private float Alpha;

    private float Painted = -1f;

    private bool Dirty;

    private int FlashBit = -1;

    private float FlashStart;

    // the last paint drew a burst frame, so the next one has to clear it
    private bool FlashShown;
}

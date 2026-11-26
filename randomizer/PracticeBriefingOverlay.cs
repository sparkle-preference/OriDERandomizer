using System.Collections.Generic;
using Game;
using UnityEngine;

// The skill ring and tree over a practice briefing and each countdown, built where no game camera looks and
// photographed over black and over white, since the game's shaders never write the alpha we need.
public static class PracticeBriefingOverlay {
    // Statics, so the look can be tuned live; a change shows on the next Show.
    internal static float Opacity = 0.75f;

    // the ring's frame, as a share of the screen's height
    internal static float RingHeight = 0.42f;

    // each half's center, as a share of the half-width out from the middle
    internal static float Spread = 0.7f;

    // around Ori, in Ori heights: clear inside ClearInner, full strength from ClearOuter
    internal static float ClearInner = 0.5f;

    internal static float ClearOuter = 2f;

    internal static bool TreeIcons = true;

    // the keys and world events inside the ring: their circle and icons, as shares of the tracker's layout
    internal static float InnerCircle = 0.8f;

    internal static float InnerIcons = 1f;

    // the tree at this size: line and glow strength, the halo under owned skills, color saturation,
    // and the one hue (degrees) every red is pulled to
    internal static float Lines = 0.5f;

    internal static float Glow = 0.35f;

    internal static float Halo = 0.6f;

    internal static float Saturation = 1f;

    internal static float RedHue = 20f;

    // the plain dark backing, ring and tree apart: strength and reach past the art (a share of the
    // half's size); the edge's width is a share of the shape's radius
    internal static float Backing = 0.5f;

    internal static float BackingReach = 0.05f;

    internal static float TreeBacking = 0.8f;

    internal static float TreeReach = 0.03f;

    internal static float BackingEdge = 0.2f;

    // the photographs' resolution, at most this many pixels per screen height
    internal static int Detail = 1080;

    public static void Show() {
        Hide();
        Wanted = true;
        TryBuild();
    }

    // A load from the title finishes before the game makes its pause screen and skill tree, so the
    // build waits for both.
    private static void TryBuild() {
        if (InventoryManager.Instance == null || SkillTreeManager.Instance == null) {
            return;
        }

        Wanted = false;
        try {
            Build();
        } catch (System.Exception e) {
            Randomizer.LogError("briefing overlay: could not build -- " + e);
            Hide();
        }
    }

    // fade: 1 on the briefing, falling to 0 by GO
    public static void Tick(float fade) {
        if (Wanted) {
            TryBuild();
        }

        if (Panels.Count == 0) {
            return;
        }

        // the copies light their skills and pick their colors as they wake: a few frames to settle
        if (Settling > 0) {
            Settling--;
            if (Settling > 0) {
                return;
            }

            try {
                foreach (var panel in Panels) {
                    Photograph(panel);
                }
            } catch (System.Exception e) {
                Randomizer.LogError("briefing overlay: could not photograph -- " + e);
                Hide();
                return;
            }

            Drop(ref Parts);
        }

        var alpha = Opacity * Mathf.Clamp01(fade);
        foreach (var panel in Panels) {
            panel.Display.SetActive(alpha > 0.001f);
            if (Mathf.Abs(alpha - panel.Shown) > 0.002f) {
                panel.Shown = alpha;
                panel.Material.color = new Color(1f, 1f, 1f, alpha);
            }
        }
    }

    public static void Hide() {
        Wanted = false;
        Drop(ref Parts);
        foreach (var panel in Panels) {
            Drop(ref panel.Display);
            if (panel.Lens != null) {
                Object.Destroy(panel.Lens.gameObject);
            }

            if (panel.Film != null) {
                panel.Film.Release();
                Object.Destroy(panel.Film);
            }

            if (panel.Photo != null) {
                Object.Destroy(panel.Photo);
            }
        }

        Panels.Clear();
        Settling = 0;
    }

    private static void Drop(ref GameObject target) {
        if (target != null) {
            Object.Destroy(target);
        }

        target = null;
    }

    private class Panel {
        // in the GUI camera's units
        public Rect Area;
        public Camera Lens;
        public RenderTexture Film;
        public Texture2D Photo;
        public GameObject Display;
        public Material Material;
        public float Shown = -1f;
        // the backing's shape: a disc (center and radius), or else the art's own outline at this height
        public Vector2 DiscCenter;
        public float DiscRadius;
        public float TreeHeight;
    }

    private static readonly List<Panel> Panels = new List<Panel>();

    private static GameObject Parts;

    private static int Settling;

    // shown, but still waiting on the pause screen and skill tree to exist
    private static bool Wanted;

    // ---- building ----

    // no game camera draws this layer, so the parts are seen only through our lenses
    private const int Offstage = 31;

    // in the pause screen's own units: the ring's center (also its skills' center) and frame
    private static readonly Vector2 RingCenter = new Vector2(-4.16f, 0.14f);

    private const float RingFrame = 5.4f;

    // the ring's skills stand this share of its frame out from its center, and reach this much further
    private const float SkillRadius = 166f / 486f;

    private const float SkillReach = 0.11f;

    private static void Build() {
        var gui = Game.UI.Cameras.System.GUICamera.Camera;
        if (gui == null || Characters.Sein == null) {
            return;
        }

        var height = 2f * gui.orthographicSize;
        var width = height * Screen.width / Screen.height;
        var frame = RingHeight * height;
        var scale = frame / RingFrame;
        var left = -Spread * width / 2f;
        var right = Spread * width / 2f;

        Parts = new GameObject("briefingParts");
        Object.DontDestroyOnLoad(Parts);
        var inventory = InventoryManager.Instance;
        if (inventory != null) {
            // a closed pause screen leaves its parts faded out, and a copy would remember that as their
            // full strength; it fades them again itself when it next opens
            foreach (var fader in inventory.GetComponentsInChildren<TransparencyAnimator>(true)) {
                fader.RestoreToOriginalState();
            }

            Copy(inventory.transform.FindChild("skillsDefinitiveEdition"), RingCenter, new Vector2(left, 0f), scale);
        }

        Inside(new Vector2(left, 0f), SkillRadius * frame);
        var treeHeight = frame * 5.16f / RingFrame;
        Tree(new Vector2(right, 0f), treeHeight);
        foreach (var child in Parts.GetComponentsInChildren<Transform>(true)) {
            child.gameObject.layer = Offstage;
        }

        var pixels = Mathf.Min(Detail, Screen.height) / height;
        var disc = (SkillRadius + SkillReach + BackingReach) * frame;
        var half = Mathf.Max(frame / 2f, disc * (1f + BackingEdge / 2f)) * 1.02f;
        var ring = MakePanel(gui, new Rect(left - half, -half, 2f * half, 2f * half), pixels);
        ring.DiscCenter = new Vector2(left, 0f);
        ring.DiscRadius = disc;
        Panels.Add(ring);
        var treeSpan = treeHeight * (1.12f + 2f * (TreeReach + BackingEdge / 4f));
        var tree = MakePanel(gui, new Rect(right - treeSpan * RandomizerGhostBadge.TreeAspect / 2f, -treeSpan / 2f,
            treeSpan * RandomizerGhostBadge.TreeAspect, treeSpan), pixels);
        tree.TreeHeight = treeHeight;
        Panels.Add(tree);
        Settling = 3;
    }

    private static Panel MakePanel(Camera gui, Rect area, float pixels) {
        var panel = new Panel { Area = area };
        var w = Mathf.Max(8, Mathf.RoundToInt(area.width * pixels));
        var h = Mathf.Max(8, Mathf.RoundToInt(area.height * pixels));
        panel.Film = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        var lens = new GameObject("briefingLens");
        Object.DontDestroyOnLoad(lens);
        lens.transform.position = new Vector3(area.center.x, area.center.y, -8f);
        panel.Lens = lens.AddComponent<Camera>();
        panel.Lens.enabled = false;
        panel.Lens.orthographic = true;
        panel.Lens.orthographicSize = area.height / 2f;
        panel.Lens.aspect = area.width / area.height;
        panel.Lens.cullingMask = 1 << Offstage;
        panel.Lens.clearFlags = CameraClearFlags.SolidColor;
        panel.Lens.nearClipPlane = 0.3f;
        panel.Lens.farClipPlane = 100f;
        panel.Lens.targetTexture = panel.Film;

        panel.Material = new Material(Shader.Find("UI/Default"));
        panel.Material.color = new Color(1f, 1f, 1f, 0f);
        // the game reads the frame's alpha back, so this writes color only
        panel.Material.SetFloat("_ColorMask", (float)(UnityEngine.Rendering.ColorWriteMask.Red |
            UnityEngine.Rendering.ColorWriteMask.Green | UnityEngine.Rendering.ColorWriteMask.Blue));
        panel.Material.SetVector("_ClipRect", new Vector4(-1e7f, -1e7f, 1e7f, 1e7f));
        panel.Material.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
        panel.Display = new GameObject("briefingOverlay");
        Object.DontDestroyOnLoad(panel.Display);
        panel.Display.AddComponent<MeshFilter>().mesh = RandomizerQuad.UnitQuad();
        panel.Display.AddComponent<MeshRenderer>().sharedMaterial = panel.Material;
        panel.Display.layer = GuiLayer(gui);
        // behind the briefing's own text
        panel.Display.transform.position = new Vector3(area.center.x, area.center.y, 1f);
        panel.Display.transform.localScale = new Vector3(area.width, area.height, 1f);
        panel.Display.SetActive(false);
        return panel;
    }

    private static int GuiLayer(Camera gui) {
        for (var layer = 0; layer < 32; layer++) {
            if ((gui.cullingMask & (1 << layer)) != 0) {
                return layer;
            }
        }

        return 0;
    }

    // A live piece of the pause screen, so it shows what the pause screen would: the copy's own
    // OnEnable lights owned skills.
    private static GameObject Copy(Transform source, Vector2 center, Vector2 at, float scale) {
        if (source == null) {
            return null;
        }

        // copied asleep, so nothing in it runs before the menu's parts are stripped out
        var wasActive = source.gameObject.activeSelf;
        source.gameObject.SetActive(false);
        var copy = (GameObject)Object.Instantiate(source.gameObject);
        source.gameObject.SetActive(wasActive);
        foreach (var item in copy.GetComponentsInChildren<CleverMenuItem>(true)) {
            Object.DestroyImmediate(item);
        }

        foreach (var help in copy.GetComponentsInChildren<InventoryItemHelpText>(true)) {
            Object.DestroyImmediate(help);
        }

        var doomed = new List<GameObject>();
        foreach (var node in copy.GetComponentsInChildren<Transform>(true)) {
            var name = node.name;
            // the menu's cursor, its title and ability-name line, sounds and selection sparkle
            if (name == "inventorySelection" || name == "skillsTitle" || name == "abilityGroup" || name == "highlightSound" ||
                    name == "pressedSound" || name.Contains("Selectable") || node.GetComponent<ParticleSystem>() != null) {
                doomed.Add(node.gameObject);
            }
        }

        foreach (var node in doomed) {
            if (node != null) {
                Object.DestroyImmediate(node);
            }
        }

        var holder = new GameObject(source.name);
        holder.transform.parent = Parts.transform;
        holder.transform.localScale = new Vector3(scale, scale, 1f);
        holder.transform.localPosition = new Vector3(at.x - center.x * scale, at.y - center.y * scale, 0f);
        copy.transform.parent = holder.transform;
        copy.transform.localPosition = source.localPosition;
        copy.transform.localRotation = source.localRotation;
        copy.transform.localScale = source.localScale;
        copy.SetActive(true);
        return copy;
    }

    // ---- the world events ----

    // the tracker's art for each, and its angle on the inner circle: keys below, events above
    // (Warmth Returned changes nothing, so it is left out)
    private static readonly string[] Arts = { "water_vein", "gumon_seal", "sunstone", "clean_water", "wind_restored" };

    private static readonly float[] Angles = { 198f, 270f, 342f, 126f, 54f };

    // the tracker's inner circle and its icons' frame, in skill radii (its ring: center 376,369, radius 223)
    private const float InnerShare = 125f / 223f;

    private const float IconShare = 150f / 223f;

    // in Arts' order; the pause screen's own conditions sit on parts a copy strips
    private static bool Had(int index) {
        switch (index) {
            case 0: return Sein.World.Keys.GinsoTree;
            case 1: return Sein.World.Keys.ForlornRuins;
            case 2: return Sein.World.Keys.MountHoru;
            case 3: return Sein.World.Events.WaterPurified;
            default: return Sein.World.Events.WindRestored;
        }
    }

    // the dungeon keys and world events on a small circle inside the ring, in the tracker's art
    private static void Inside(Vector2 center, float skillRadius) {
        var radius = InnerShare * skillRadius * InnerCircle;
        var size = IconShare * skillRadius * InnerIcons;
        for (var i = 0; i < Arts.Length; i++) {
            var angle = Angles[i] * Mathf.Deg2Rad;
            var at = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            var icon = Quad(Parts, Arts[i], RandomizerGhostBadge.Glyph("briefing_" + Arts[i] + (Had(i) ? "" : "_grey") + ".png"),
                at, size, -0.05f);
            if (icon != null) {
                RandomizerGhostBadge.Tint(icon, new Color(1f, 1f, 1f, 1f));
            }
        }
    }

    // ---- the skill tree ----

    // each skill's menu item, in RandomizerGhost.TreeOrder
    private static readonly string[] Items = {
        "orange/01. quickFlame", "orange/02. sparkFlame", "orange/03. chargeFlameBurn", "orange/04. splitFlame",
        "orange/05. grenadeUpgrade", "orange/06. cinderFlame", "orange/07. ultraStomp", "orange/08. rapidFlame",
        "orange/09. chargeFlameBlast", "orange/10. ultraSplitFlame",
        "purple/01. soulMagnet", "purple/02. mapMarker", "purple/03. healthEfficiency", "purple/04. ultraSoulMagnet",
        "purple/05. energyEfficiency", "purple/06. abilityMarker", "purple/07. soulEfficiency", "purple/08. healthMarkers",
        "purple/09. energyMarkers", "purple/10. sense",
        "aqua/01. rekindle", "aqua/02. regroup", "aqua/03. chargeFlameEfficiency", "aqua/04. airDash",
        "aqua/05. ultraSoulFlame", "aqua/06. chargeDash", "aqua/07. waterBreath", "aqua/08. soulFlameEfficiency",
        "aqua/09. doubleJumpUpgrade", "aqua/10. ultraDefense"
    };

    private static void Tree(Vector2 at, float height) {
        var tree = new GameObject("tree");
        tree.transform.parent = Parts.transform;
        tree.transform.localPosition = new Vector3(at.x, at.y, 0f);
        var layers = RandomizerGhostBadge.Layers;
        for (var i = 0; i < layers.Length; i++) {
            var quad = Quad(tree, layers[i].Path, RandomizerGhostBadge.MenuTexture(layers[i].Path),
                new Vector2(layers[i].X * height, layers[i].Y * height), layers[i].Size * height, i < 4 ? 0f : -0.01f);
            var strength = i < 4 ? Glow : (TreeIcons ? Lines : 0.8f * Lines);
            RandomizerGhostBadge.Tint(quad, new Color(1f, 1f, 1f, strength));
        }

        var manager = SkillTreeManager.Instance;
        var menuItems = manager == null ? null : RandomizerGhostBadge.Find(manager.transform.root, "menuItems");
        var dot = RandomizerGhostBadge.Glyph("ghost_dot.png");
        var span = RandomizerGhostBadge.TreeAspect * height;
        var owned = RandomizerGhost.SkillBits();
        for (var bit = 0; bit < Items.Length; bit++) {
            var ring = new Vector2(RandomizerGhostBadge.Rings[bit * 2] * height, RandomizerGhostBadge.Rings[bit * 2 + 1] * height);
            var lane = RandomizerGhostBadge.Lanes[bit / 10];
            var has = (owned & (1u << bit)) != 0;
            if (!TreeIcons) {
                if (has) {
                    var lit = Quad(tree, "skill" + bit, dot, ring, RandomizerGhostBadge.DotSpan * span, -0.02f);
                    RandomizerGhostBadge.Tint(lit, RandomizerGhostBadge.Lit(lane, 60f / 255f));
                }

                continue;
            }

            var s = RandomizerGhostBadge.IconSpan * span;
            if (has) {
                var halo = Quad(tree, "halo" + bit, dot, ring, 2.2f * s, -0.02f);
                RandomizerGhostBadge.Tint(halo, new Color(lane.r, lane.g, lane.b, Halo));
            }

            var node = menuItems == null ? null : menuItems.Find(Items[bit] + "/abilityIconAquaflame");
            var icon = Quad(tree, "icon" + bit, RandomizerGhostBadge.TextureOf(node), ring, 1.1f * s, -0.03f);
            RandomizerGhostBadge.Tint(icon, has ? RandomizerGhostBadge.Lit(lane, 70f / 255f) : new Color(1f, 1f, 1f, 0.28f));
        }
    }

    private static GameObject Quad(GameObject parent, string name, Texture2D texture, Vector2 at, float size, float z) {
        var quad = RandomizerQuad.BuildTextured(name, texture, null);
        if (quad == null) {
            return null;
        }

        quad.transform.parent = parent.transform;
        quad.transform.localPosition = new Vector3(at.x, at.y, z);
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = new Vector3(size, size, 1f);
        return quad;
    }

    // ---- photographing ----

    // Ori's height in world units, for the clear circle
    private const float OriHeight = 1.3f;

    private static void Photograph(Panel panel) {
        var w = panel.Film.width;
        var h = panel.Film.height;
        var black = Shoot(panel, Color.black);
        var white = Shoot(panel, Color.white);

        // where Ori stands, in screen pixels, and how much of the screen one Ori height is
        var ori = new Vector2(-1e6f, -1e6f);
        var oriHeight = 1f;
        var sein = Characters.Sein;
        var world = Game.UI.Cameras.Current == null ? null : Game.UI.Cameras.Current.Camera;
        if (sein != null && world != null) {
            var spot = world.WorldToScreenPoint(sein.Position);
            var head = world.WorldToScreenPoint(sein.Position + Vector3.up * OriHeight);
            ori = new Vector2(spot.x, spot.y);
            oriHeight = Mathf.Max(1f, Mathf.Abs(head.y - spot.y));
        }

        var inner = ClearInner * oriHeight;
        var outer = Mathf.Max(inner + 1f, ClearOuter * oriHeight);
        var gui = Game.UI.Cameras.System.GUICamera.Camera;
        var guiHeight = 2f * gui.orthographicSize;
        var guiWidth = guiHeight * Screen.width / Screen.height;

        // over black the picture is color * alpha; over white it gains 1 - alpha
        var art = new float[w * h];
        for (var i = 0; i < art.Length; i++) {
            var b = black[i];
            var c = white[i];
            art[i] = Mathf.Clamp01(1f - ((c.r - b.r) + (c.g - b.g) + (c.b - b.b)) / 3f / 255f);
        }

        var back = Backdrop(panel, art, w, h);
        var photo = new Color32[w * h];
        for (var y = 0; y < h; y++) {
            var screenY = ((panel.Area.yMin + (y + 0.5f) / h * panel.Area.height) / guiHeight + 0.5f) * Screen.height;
            for (var x = 0; x < w; x++) {
                var i = y * w + x;
                var alpha = art[i];
                var under = back[i] * (panel.TreeHeight > 0f ? TreeBacking : Backing) * (1f - alpha);
                var total = alpha + under;
                if (total <= 0.002f) {
                    continue;
                }

                var b = black[i];
                var r = alpha > 0.002f ? b.r / alpha / 255f : 0f;
                var g = alpha > 0.002f ? b.g / alpha / 255f : 0f;
                var bl = alpha > 0.002f ? b.b / alpha / 255f : 0f;
                if (panel.TreeHeight > 0f) {
                    Grade(ref r, ref g, ref bl);
                }

                // the black backing adds coverage, not color
                var keep = alpha / total;
                var screenX = ((panel.Area.xMin + (x + 0.5f) / w * panel.Area.width) / guiWidth + 0.5f) * Screen.width;
                var distance = Vector2.Distance(new Vector2(screenX, screenY), ori);
                var clear = Mathf.Clamp01((distance - inner) / (outer - inner));
                photo[i] = new Color32(Channel(r * keep), Channel(g * keep), Channel(bl * keep),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(total) * clear * 255f));
            }
        }

        panel.Photo = new Texture2D(w, h, TextureFormat.ARGB32, false);
        panel.Photo.wrapMode = TextureWrapMode.Clamp;
        panel.Photo.SetPixels32(photo);
        panel.Photo.Apply(false, true);
        panel.Material.mainTexture = panel.Photo;
        Object.Destroy(panel.Lens.gameObject);
        panel.Lens = null;
        panel.Film.Release();
        Object.Destroy(panel.Film);
        panel.Film = null;
    }

    private static byte Channel(float value) {
        return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
    }

    // The game's red lane shades from orange to crimson along its length, so every hue within 30 degrees
    // of red (fading by 40) is pulled to RedHue; then saturation scales. Channels run 0 to 1.
    private static void Grade(ref float r, ref float g, ref float b) {
        var max = Mathf.Max(r, Mathf.Max(g, b));
        var span = max - Mathf.Min(r, Mathf.Min(g, b));
        var hue = 0f;
        if (span > 0f) {
            hue = 60f * (max == r ? ((g - b) / span) % 6f : max == g ? (b - r) / span + 2f : (r - g) / span + 4f);
            if (hue < 0f) {
                hue += 360f;
            }
        }

        var near = Mathf.Min(hue, 360f - hue);
        if (near < 40f) {
            var signed = hue > 180f ? hue - 360f : hue;
            hue = (signed + (RedHue - signed) * Mathf.Min(1f, (40f - near) / 10f) + 360f) % 360f;
        }

        var s = (max > 0f ? span / max : 0f) * Saturation;
        r = Hsv(5f, hue, s, max);
        g = Hsv(3f, hue, s, max);
        b = Hsv(1f, hue, s, max);
    }

    private static float Hsv(float n, float hue, float s, float v) {
        var k = (n + hue / 60f) % 6f;
        return v - v * s * Mathf.Max(0f, Mathf.Min(k, Mathf.Min(4f - k, 1f)));
    }

    // The backing's coverage per pixel, 0 to 1: a disc with a soft edge, or the art's outline grown
    // by the reach and feathered.
    private static float[] Backdrop(Panel panel, float[] art, int w, int h) {
        var back = new float[w * h];
        var perUnit = w / panel.Area.width;
        if (panel.DiscRadius > 0f) {
            var cx = (panel.DiscCenter.x - panel.Area.xMin) * perUnit;
            var cy = (panel.DiscCenter.y - panel.Area.yMin) * perUnit;
            var radius = panel.DiscRadius * perUnit;
            var edge = Mathf.Max(1f, BackingEdge * radius);
            for (var y = 0; y < h; y++) {
                for (var x = 0; x < w; x++) {
                    var d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    back[y * w + x] = Mathf.Clamp01((radius + edge / 2f - d) / edge);
                }
            }

            return back;
        }

        if (panel.TreeHeight <= 0f) {
            return back;
        }

        // coarse cells keep the grow and the blur cheap
        var size = panel.TreeHeight * perUnit;
        var cell = Mathf.Max(2, Mathf.RoundToInt(size / 64f));
        var cw = (w + cell - 1) / cell;
        var ch = (h + cell - 1) / cell;
        var outline = new bool[cw * ch];
        for (var y = 0; y < h; y += 2) {
            for (var x = 0; x < w; x += 2) {
                if (art[y * w + x] > 40f / 255f) {
                    outline[y / cell * cw + x / cell] = true;
                }
            }
        }

        var grow = Mathf.RoundToInt(TreeReach * size / cell);
        var grown = new float[cw * ch];
        for (var y = 0; y < ch; y++) {
            for (var x = 0; x < cw; x++) {
                if (!outline[y * cw + x]) {
                    continue;
                }

                for (var dy = -grow; dy <= grow; dy++) {
                    for (var dx = -grow; dx <= grow; dx++) {
                        var yy = y + dy;
                        var xx = x + dx;
                        if (yy >= 0 && yy < ch && xx >= 0 && xx < cw && dx * dx + dy * dy <= grow * grow) {
                            grown[yy * cw + xx] = 1f;
                        }
                    }
                }
            }
        }

        var blur = Mathf.RoundToInt(BackingEdge * size / 4f / cell);
        if (blur > 0) {
            BoxBlur(grown, cw, ch, blur);
            BoxBlur(grown, cw, ch, blur);
        }

        for (var y = 0; y < h; y++) {
            var gy = Mathf.Clamp((y + 0.5f) / cell - 0.5f, 0f, ch - 1f);
            var y0 = (int)gy;
            var y1 = Mathf.Min(ch - 1, y0 + 1);
            var fy = gy - y0;
            for (var x = 0; x < w; x++) {
                var gx = Mathf.Clamp((x + 0.5f) / cell - 0.5f, 0f, cw - 1f);
                var x0 = (int)gx;
                var x1 = Mathf.Min(cw - 1, x0 + 1);
                var fx = gx - x0;
                var top = Mathf.Lerp(grown[y0 * cw + x0], grown[y0 * cw + x1], fx);
                var bottom = Mathf.Lerp(grown[y1 * cw + x0], grown[y1 * cw + x1], fx);
                back[y * w + x] = Mathf.Min(1f, Mathf.Lerp(top, bottom, fy));
            }
        }

        return back;
    }

    // one box blur along the rows, then the columns, clamped at the edges
    private static void BoxBlur(float[] v, int w, int h, int r) {
        var t = new float[v.Length];
        var span = 2f * r + 1f;
        for (var y = 0; y < h; y++) {
            var sum = 0f;
            for (var x = -r; x <= r; x++) {
                sum += v[y * w + Mathf.Clamp(x, 0, w - 1)];
            }

            for (var x = 0; x < w; x++) {
                t[y * w + x] = sum / span;
                sum += v[y * w + Mathf.Min(w - 1, x + r + 1)] - v[y * w + Mathf.Max(0, x - r)];
            }
        }

        for (var x = 0; x < w; x++) {
            var sum = 0f;
            for (var y = -r; y <= r; y++) {
                sum += t[Mathf.Clamp(y, 0, h - 1) * w + x];
            }

            for (var y = 0; y < h; y++) {
                v[y * w + x] = sum / span;
                sum += t[Mathf.Min(h - 1, y + r + 1) * w + x] - t[Mathf.Max(0, y - r) * w + x];
            }
        }
    }

    private static Color32[] Shoot(Panel panel, Color background) {
        panel.Lens.backgroundColor = background;
        panel.Lens.Render();
        var was = RenderTexture.active;
        RenderTexture.active = panel.Film;
        var read = new Texture2D(panel.Film.width, panel.Film.height, TextureFormat.ARGB32, false);
        read.ReadPixels(new Rect(0f, 0f, panel.Film.width, panel.Film.height), 0, 0, false);
        RenderTexture.active = was;
        var pixels = read.GetPixels32();
        Object.Destroy(read);
        return pixels;
    }
}

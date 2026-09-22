using System.Collections.Generic;
using System.Linq;
using Game;
using UnityEngine;

// The pause menu says which skills are Enhanced: one drifting rainbow spans the skill wheel and
// each icon's own white wears the slice it sits in. "Enhanced" goes in front of the name, and
// the effect into the icon's pop-up.
public static class RandomizerEnhancedWheel {
    // wheel skill -> the inventory id its Enhanced form is held under
    private static readonly Dictionary<AbilityType, int> Items = new Dictionary<AbilityType, int> {
        { AbilityType.SpiritFlame, 410 }, { AbilityType.WallJump, 411 }, { AbilityType.ChargeFlame, 412 },
        { AbilityType.DoubleJump, 413 }, { AbilityType.Bash, 414 }, { AbilityType.Stomp, 415 },
        { AbilityType.Glide, 416 }, { AbilityType.Climb, 417 }, { AbilityType.ChargeJump, 418 },
        { AbilityType.Dash, 419 }, { AbilityType.Grenade, 420 },
    };

    private static readonly Dictionary<AbilityType, string> Effects = new Dictionary<AbilityType, string> {
        { AbilityType.SpiritFlame, "Sein's voice is restored, and she's got a lot to say." },
        { AbilityType.WallJump, "Comes with a free copy of $Climb$." },
        { AbilityType.ChargeFlame, "Hold to draw in nearby projectiles; scatter them all on release." },
        { AbilityType.DoubleJump, "Jump as many times as you like." },
        { AbilityType.Bash, "No longer needs a target." },
        { AbilityType.Stomp, "Stomp in any direction you want, aiming with [Stick]." },
        { AbilityType.Glide, "Glide upwards everywhere, ignoring the wind." },
        { AbilityType.Climb, "Passive immunity to spikes and heat damage." },
        { AbilityType.ChargeJump, "Applies Charge Jump damage and shielding while holding [ChargeJumpCharge]." },
        { AbilityType.Dash, "Now aimable with [Stick]. Also, Charge Dash costs less energy." },
        { AbilityType.Grenade, "Now breaks most walls and floors." },
    };

    // times the rainbow repeats around the wheel, and turns of hue the rim leads the centre by
    private const float Repeats = 6f;

    private const float Swirl = -0.2f;

    // how far the finished colour is lifted toward white
    private const float Pale = 0.15f;

    // turns per second the pattern drifts, and times a second each overlay is stepped on
    private const float Spin = 0.05f;

    private const float Rate = 20f;

    // the overlay's opacity, and how far past the ring the capture reaches
    private const float Alpha = 0.4f;

    private const float Margin = 1.15f;

    // how far in front of the icon the overlay sits
    private const float Lift = 0.05f;

    // a sprite's coverage below this is left alone, and what the rest is multiplied by
    private const float Floor = 0.5f;

    private const float Gain = 1.2f;

    // the curve the art's brightness is read through, below 1 lifting the dim middle
    private const float Tone = 0.8f;

    // characters the label rainbow crosses once, so long names read as more than one sweep
    private const int Every = 9;

    private const int Pixels = 256;

    // the rainbow as a ring of colours; a drift is a step around it
    private const int Steps = 1024;

    private const string WheelName = "randomizerEnhancedWheel";

    // a layer nothing else draws on, for the capture camera
    private const int CaptureLayer = 31;

    // the icon sprites' UberShader neutral: 0.5 is unlit white
    private const float Neutral = 0.5f;

    public static bool IsEnhanced(InventoryAbilityItem item) {
        int id;
        return item != null && Items.TryGetValue(item.Ability, out id)
            && Randomizer.Inventory != null && Randomizer.Inventory.GetRandomizerItem(id) > 0;
    }

    // An overlay on every Enhanced icon and none on the rest; the screen calls this as it opens,
    // and Tick keeps trying until the screen has finished fading in.
    public static void Refresh(InventoryManager screen) {
        if (dressed != screen) {
            plainly.Clear();
            for (var i = lit.Count - 1; i >= 0; i--) {
                Drop(lit[i]);
            }

            dressed = screen;
        }

        waiting = screen;
        Build(screen);
    }

    private static InventoryManager dressed;

    private static InventoryManager waiting;

    // The screen is still fading in when this first runs, so the capture waits for it. An icon's
    // own opacity is not waited on: a locked skill never reaches full, and its overlay divides
    // that dimming out instead.
    private static bool Lit(InventoryManager screen) {
        var fade = screen.GetComponent<TransparencyAnimator>();
        return fade == null || fade.FinalOpacity > 0.99f;
    }

    // how far an icon is dimmed right now, which the capture sees and the fade applies again
    private static float Dimming(InventoryAbilityItem item) {
        var fade = item.GetComponent<TransparencyAnimator>();
        return fade == null ? 1f : Mathf.Clamp(fade.FinalOpacity, 0.12f, 1f);
    }

    private static void Build(InventoryManager screen) {
        try {
            var items = screen.GetComponentsInChildren<InventoryAbilityItem>(true);
            var ready = Lit(screen);
            var centre = Centre(items);
            var span = Span(items, centre);
            var any = false;
            foreach (var item in items) {
                var wheel = item.transform.FindChild(WheelName);
                if (!IsEnhanced(item)) {
                    if (wheel != null) {
                        wheel.gameObject.SetActive(false);
                    }

                    continue;
                }

                any = true;
                if (wheel == null) {
                    if (!ready) {
                        continue;
                    }

                    wheel = MakeIconWheel(item, centre, span);
                    if (wheel == null) {
                        continue;
                    }
                }

                wheel.gameObject.SetActive(true);
            }

            // nothing Enhanced means nothing to wait for either, so Tick goes back to sleep
            if (ready || !any) {
                waiting = null;
            }
        } catch (System.Exception e) {
            Randomizer.log("enhanced wheel: " + e);
        }
    }

    // Registers new overlays with their fade animators a frame late, since the icons' initialise
    // after the screen's, then steps the drift on. Idle for a seed with no Enhanced skills.
    public static void Tick() {
        if (waiting == null && pending.Count == 0 && lit.Count == 0 && avatarBox == null) {
            return;
        }

        if (Time.frameCount == refreshed) {
            return;
        }

        for (var i = pending.Count - 1; i >= 0; i--) {
            if (pending[i] != null) {
                TransparencyAnimator.Register(pending[i]);
            }

            pending.RemoveAt(i);
        }

        if (waiting != null) {
            Build(waiting);
        } else if (Animated()) {
            spun += Spin * Time.unscaledDeltaTime;
            owed = Mathf.Min(owed + lit.Count * Rate * Time.unscaledDeltaTime, lit.Count);
            while (owed >= 1f) {
                owed -= 1f;
                Drift();
            }
        }

        if (avatarBox != null && avatarItem != null && DyeAvatarNow(avatarBox, avatarItem)) {
            avatarBox = null;
            avatarItem = null;
        }
    }

    private static bool Animated() {
        var setting = RandomizerSettings.Customization.EnhancedWheelAnimation;
        return setting == null || setting.Value;
    }

    private static Color32[] ramp;

    private static Color32[] Ring() {
        if (ramp != null) {
            return ramp;
        }

        ramp = new Color32[Steps];
        for (var i = 0; i < Steps; i++) {
            var shade = Palette((float)i / Steps);
            ramp[i] = new Color32((byte)Byte(shade.r), (byte)Byte(shade.g), (byte)Byte(shade.b), 255);
        }

        return ramp;
    }

    private static void Repaint(Painted painted, int step) {
        var ring = Ring();
        for (var i = 0; i < painted.Pixels.Length; i++) {
            var shade = ring[(painted.Place[i] + step) % Steps];
            var lit = painted.Lit[i];
            painted.Pixels[i] = new Color32((byte)((shade.r * lit) >> 8),
                (byte)((shade.g * lit) >> 8), (byte)((shade.b * lit) >> 8), painted.Cover[i]);
        }

        painted.Texture.SetPixels32(painted.Pixels);
        painted.Texture.Apply();
    }

    private static float spun;

    private static int drifting;

    private static float owed;

    private static readonly List<Painted> lit = new List<Painted>();

    // One overlay stepped to where the pattern has drifted to, round robin, so a frame pays for a
    // single texture however many icons are showing.
    private static void Drift() {
        if (lit.Count == 0) {
            return;
        }

        var painted = lit[drifting++ % lit.Count];
        // a pop-up closing or the screen going takes the quad, but never the texture we made
        if (painted.Quad == null) {
            Drop(painted);
            return;
        }

        Repaint(painted, (int)(Mathf.Repeat(spun, 1f) * Steps));
    }

    private static void Drop(Painted painted) {
        if (painted.Texture != null) {
            Object.Destroy(painted.Texture);
        }

        lit.Remove(painted);
    }

    private static readonly List<Transform> pending = new List<Transform>();

    private static int refreshed = -1;

    // the selection glow wears the colour the pattern has where that icon sits
    public static void Highlight(GameObject highlight, InventoryAbilityItem item) {
        if (highlight == null) {
            return;
        }

        var shade = Color.clear;
        if (IsEnhanced(item)) {
            Vector3 hub;
            float span;
            Wheel(item, out hub, out span);
            if (span > 0f) {
                var offset = item.transform.position - hub;
                var tone = Palette(Where(offset.x, offset.y, span) + spun);
                shade = new Color(tone.r * Neutral, tone.g * Neutral, tone.b * Neutral, 1f);
            }
        }

        foreach (var renderer in highlight.GetComponentsInChildren<Renderer>(true)) {
            if (renderer == null || renderer.sharedMaterial == null || renderer is ParticleSystemRenderer) {
                continue;
            }

            // the colour to go back to is remembered before the first tint: asking the renderer
            // afterwards hands back the instance we tinted, so the glow kept the last hue
            Color plain;
            if (!plainly.TryGetValue(renderer, out plain)) {
                plain = renderer.sharedMaterial.GetColor("_Color");
                plainly[renderer] = plain;
            }

            var material = renderer.material;
            var current = material.GetColor("_Color");
            var rgb = shade == Color.clear ? plain : shade;
            material.SetColor("_Color", new Color(rgb.r, rgb.g, rgb.b, current.a));
        }
    }

    private static readonly Dictionary<Renderer, Color> plainly = new Dictionary<Renderer, Color>();

    // The pop-up's avatar, once the box has arrived: the same slice of the pattern as its icon.
    public static void DyeAvatar(MessageBox box, InventoryAbilityItem item) {
        avatarBox = IsEnhanced(item) ? box : null;
        avatarItem = item;
    }

    private static MessageBox avatarBox;

    private static InventoryAbilityItem avatarItem;

    private static bool DyeAvatarNow(MessageBox box, InventoryAbilityItem item) {
        try {
            var mount = box.Avatar;
            if (mount == null || mount.childCount == 0) {
                return true;
            }

            var avatar = mount.GetChild(0);
            if (avatar.lossyScale.x < 0.001f) {
                return false;
            }

            var parts = Sprites(avatar, false);
            if (parts.Count == 0) {
                return true;
            }

            var bounds = parts[0].bounds;
            foreach (var part in parts) {
                bounds.Encapsulate(part.bounds);
            }

            Vector3 hub;
            float span;
            Wheel(item, out hub, out span);
            var size = Mathf.Max(bounds.size.x, bounds.size.y) * Margin;
            var painted = Capture(parts, bounds.center, size, new Pattern {
                Frame = item.transform.position,
                FrameSize = IconSize(item) * Mathf.Abs(item.transform.lossyScale.x),
                Hub = hub,
                Span = span,
            }, 1f);
            if (painted == null) {
                return true;
            }

            var quad = RandomizerQuad.BuildTextured("randomizerEnhancedAvatar", painted.Texture, parts[0]);
            if (quad == null) {
                Object.Destroy(painted.Texture);
                return true;
            }

            painted.Quad = quad.transform;
            lit.Add(painted);
            quad.GetComponent<Renderer>().sharedMaterial.SetColor("_Color", new Color(1f, 1f, 1f, Alpha));
            Last(quad.GetComponent<Renderer>(), avatar);
            quad.transform.position = bounds.center + Vector3.back * Lift;
            quad.transform.rotation = avatar.rotation;
            quad.transform.localScale = new Vector3(size, size, 1f);
            quad.transform.SetParent(avatar, true);
            pending.Add(quad.transform);
            return true;
        } catch (System.Exception e) {
            Randomizer.log("enhanced wheel: avatar: " + e);
            return true;
        }
    }

    private static Transform MakeIconWheel(InventoryAbilityItem item, Vector3 hub, float span) {
        var order = item.GetComponent<Renderer>();
        if (order == null) {
            return null;
        }

        // every mesh the icon draws with, since the circle around it lives in a different child
        // from one icon to the next and inventoryAbilitiesRing is the connector arc
        var parts = Sprites(item.transform, true);
        if (!parts.Contains(order)) {
            parts.Add(order);
        }

        var local = IconSize(item);
        var lossy = item.transform.lossyScale;
        var world = local * Mathf.Abs(lossy.x);
        var painted = Capture(parts, item.transform.position, world, new Pattern {
            Frame = item.transform.position,
            FrameSize = world,
            Hub = hub,
            Span = span,
        }, Dimming(item));
        if (painted == null) {
            return null;
        }

        var quad = RandomizerQuad.BuildTextured(WheelName, painted.Texture, order);
        if (quad == null) {
            Object.Destroy(painted.Texture);
            return null;
        }

        painted.Quad = quad.transform;
        lit.Add(painted);
        quad.GetComponent<Renderer>().sharedMaterial.SetColor("_Color", new Color(1f, 1f, 1f, Alpha));
        Last(quad.GetComponent<Renderer>(), item.transform);
        quad.transform.SetParent(item.transform, false);
        quad.transform.localPosition = new Vector3(0f, 0f, -Lift);
        // the capture is of the world, and one icon's transform mirrors it: undo that here or the
        // mask lands on the glyph back to front
        quad.transform.localScale = new Vector3(lossy.x < 0f ? -local : local,
            lossy.y < 0f ? -local : local, 1f);
        pending.Add(quad.transform);
        return quad.transform;
    }

    // The world rectangle a capture's pixels map onto, and the hub they turn around. The pop-up's
    // avatar borrows its icon's rectangle, so it shows the same slice.
    private struct Pattern {
        public Vector3 Frame;
        public float FrameSize;
        public Vector3 Hub;
        public float Span;
    }

    // What a repaint needs without going back to the gpu. Only Place moves as the pattern drifts,
    // and it moves by the same step for every pixel.
    private class Painted {
        public Transform Quad;
        public Texture2D Texture;
        public Color32[] Pixels;
        public ushort[] Place;
        public byte[] Lit;
        public byte[] Cover;
    }

    // The sprites' own white as a mask: a camera of ours renders them alone into a texture.
    private static Painted Capture(List<Renderer> parts, Vector3 centre, float size, Pattern pattern, float dim) {
        var rig = new GameObject("randomizerEnhancedCapture");
        var rt = RenderTexture.GetTemporary(Pixels, Pixels, 16, RenderTextureFormat.ARGB32);
        var layers = new int[parts.Count];
        var previous = RenderTexture.active;
        try {
            var camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = size / 2f;
            camera.aspect = 1f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = 1 << CaptureLayer;
            camera.targetTexture = rt;
            camera.transform.position = new Vector3(centre.x, centre.y, centre.z - 10f);
            camera.transform.rotation = Quaternion.identity;
            for (var i = 0; i < parts.Count; i++) {
                layers[i] = parts[i].gameObject.layer;
                parts[i].gameObject.layer = CaptureLayer;
            }

            camera.Render();
            for (var i = 0; i < parts.Count; i++) {
                parts[i].gameObject.layer = layers[i];
            }

            RenderTexture.active = rt;
            var texture = new Texture2D(Pixels, Pixels, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0f, 0f, Pixels, Pixels), 0, 0);
            var read = texture.GetPixels();
            var painted = new Painted {
                Texture = texture,
                Pixels = new Color32[read.Length],
                Place = new ushort[read.Length],
                Lit = new byte[read.Length],
                Cover = new byte[read.Length],
            };
            // The colour carries the sprite's own brightness, so the overlay multiplies the art
            // rather than hiding it. Gain is shared, never each icon's own brightest pixel.
            for (var y = 0; y < Pixels; y++) {
                for (var x = 0; x < Pixels; x++) {
                    var i = y * Pixels + x;
                    var glow = Mathf.Clamp01(Mathf.Max(read[i].r, Mathf.Max(read[i].g, read[i].b)) / dim);
                    var cover = Mathf.Clamp01(Mathf.Max(read[i].a / dim, glow));
                    var mask = Mathf.Clamp01((cover - Floor) / (1f - Floor) * Gain);
                    var dx = (x + 0.5f) / Pixels - 0.5f;
                    var dy = (y + 0.5f) / Pixels - 0.5f;
                    var wx = pattern.Frame.x + dx * pattern.FrameSize - pattern.Hub.x;
                    var wy = pattern.Frame.y + dy * pattern.FrameSize - pattern.Hub.y;
                    painted.Place[i] = (ushort)(Mathf.Repeat(Where(wx, wy, pattern.Span), 1f) * Steps);
                    painted.Lit[i] = (byte)(Mathf.Pow(glow, Tone) * 255f);
                    painted.Cover[i] = (byte)(mask * 255f);
                }
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            Repaint(painted, 0);
            return painted;
        } catch (System.Exception e) {
            Randomizer.log("enhanced wheel: capture: " + e);
            for (var i = 0; i < parts.Count; i++) {
                if (parts[i] != null && parts[i].gameObject.layer == CaptureLayer) {
                    parts[i].gameObject.layer = layers[i];
                }
            }

            return null;
        } finally {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(rig);
        }
    }

    // where a point about the hub falls on the rainbow, the spiral adding a turn with distance out
    private static float Where(float dx, float dy, float span) {
        var turn = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * Repeats;
        return span > 0.001f ? turn + Swirl * (Mathf.Sqrt(dx * dx + dy * dy) / span) : turn;
    }

    // The rainbow, violet closing back to red so the wheel has no seam. Green and indigo get two
    // stops each: a run between two of the same colour is the only stretch that cannot drift
    // toward a neighbour.
    private static readonly Color[] Stops = {
        new Color(1f, 0f, 0f),
        new Color(1f, 0.45f, 0f),
        new Color(1f, 0.95f, 0f),
        new Color(0.15f, 0.85f, 0.1f),
        new Color(0f, 0.75f, 0.45f),
        new Color(0f, 0.35f, 1f),
        new Color(0.15f, 0f, 0.75f),
        new Color(0.4f, 0.05f, 0.9f),
        new Color(0.7f, 0.1f, 1f),
    };

    // Each stop's share of the turn: equal shares would spend as long crossing the 21 degrees from
    // indigo to violet as the 57 from red to yellow.
    private static readonly float[] Shares = { 2.1f, 1.15f, 0.7f, 1.6f, 0.95f, 0.6f, 1.1f, 0.45f, 0.45f };

    private static readonly float Turn = Shares.Sum();

    // A turn of the wheel as a colour, at a constant rate between the stops it falls between: a
    // rate that changes leaves a kink the eye reads as an edge.
    private static Color Palette(float turn) {
        turn -= Mathf.Floor(turn);
        var at = turn * Turn;
        var stop = 0;
        while (stop < Shares.Length - 1 && at >= Shares[stop]) {
            at -= Shares[stop];
            stop++;
        }

        var tone = Color.Lerp(Stops[stop], Stops[(stop + 1) % Stops.Length], Mathf.Clamp01(at / Shares[stop]));
        return Color.Lerp(tone, Color.white, Pale);
    }

    // Drawn after everything the icon draws: its glow sits at a far later queue than its glyph.
    private static void Last(Renderer overlay, Transform root) {
        var queue = overlay.sharedMaterial.renderQueue;
        var order = overlay.sortingOrder;
        foreach (var part in root.GetComponentsInChildren<Renderer>(true)) {
            if (part == overlay || part.sharedMaterial == null) {
                continue;
            }

            queue = Mathf.Max(queue, part.sharedMaterial.renderQueue);
            order = Mathf.Max(order, part.sortingOrder);
        }

        overlay.sharedMaterial.renderQueue = queue + 1;
        overlay.sortingOrder = order + 1;
    }

    // The mesh sprites under an object; particles and our own overlays left out. The avatar drops
    // its filled disc and glow, which on an icon are the border and the bright core.
    private static List<Renderer> Sprites(Transform root, bool all) {
        var found = new List<Renderer>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
            var name = renderer.gameObject.name;
            if (renderer is ParticleSystemRenderer || renderer.GetComponent<MeshFilter>() == null
                    || name.StartsWith("randomizerEnhanced") || (!all && name.Contains("Background"))) {
                continue;
            }

            found.Add(renderer);
        }

        return found;
    }

    // how far the icons sit from the hub, which is the wheel's own radius
    private static float Span(InventoryAbilityItem[] items, Vector3 hub) {
        if (items.Length == 0) {
            return 0f;
        }

        var span = 0f;
        foreach (var item in items) {
            span += (item.transform.position - hub).magnitude;
        }

        return span / items.Length;
    }

    // an icon's drawn width in its own local units, its ring included
    private static float IconSize(InventoryAbilityItem item) {
        var ring = item.transform.FindChild("inventoryAbilitiesRing");
        var local = ring != null
            ? Extent(ring) * Mathf.Max(ring.localScale.x, ring.localScale.y)
            : Extent(item.transform);
        return local * Margin;
    }

    // the wheel an icon belongs to: where its hub sits and how far the icons are from it
    private static void Wheel(InventoryAbilityItem item, out Vector3 hub, out float span) {
        var screen = item.GetComponentInParent<InventoryManager>();
        var items = screen == null ? new InventoryAbilityItem[0] : screen.GetComponentsInChildren<InventoryAbilityItem>(true);
        hub = Centre(items);
        span = Span(items, hub);
    }

    private static Vector3 Centre(InventoryAbilityItem[] items) {
        if (items.Length == 0) {
            return Vector3.zero;
        }

        var centre = Vector3.zero;
        foreach (var item in items) {
            centre += item.transform.position;
        }

        return centre / items.Length;
    }

    // the larger side of a mesh's bounds, in its own local units
    private static float Extent(Transform obj) {
        var filter = obj.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) {
            return 0f;
        }

        var bounds = filter.sharedMesh.bounds.size;
        return Mathf.Max(bounds.x, bounds.y);
    }

    private static int Byte(float channel) {
        return Mathf.RoundToInt(Mathf.Clamp01(channel) * 255f);
    }

    // "Enhanced <name>" for an Enhanced skill, the skill's own provider otherwise
    public static MessageProvider NameFor(InventoryAbilityItem item, MessageProvider name) {
        if (!IsEnhanced(item) || name == null) {
            return name;
        }

        var provider = Provider(names, item.Ability);
        provider.SetMessage(RandomizerText.Rainbowify("Enhanced " + name, Every));
        return provider;
    }

    // the pop-up's text with the enhancement under it
    public static MessageProvider HelpFor(InventoryAbilityItem item, MessageProvider help) {
        string effect;
        if (!IsEnhanced(item) || help == null || !Effects.TryGetValue(item.Ability, out effect)) {
            return help;
        }

        var provider = Provider(helps, item.Ability);
        var off = RandomizerBonusSkill.IsActive(115) ? "\n(Enhanced effects are switched off)" : "";
        provider.SetMessage(help + "\n" + RandomizerText.Rainbowify("Enhanced:", Every) + "\n" + effect + off);
        return provider;
    }

    // one provider per skill, remade if the screen it belonged to took it with it
    private static RandomizerMessageProvider Provider(Dictionary<AbilityType, RandomizerMessageProvider> cache, AbilityType ability) {
        RandomizerMessageProvider provider;
        if (!cache.TryGetValue(ability, out provider) || provider == null) {
            provider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
            cache[ability] = provider;
        }

        return provider;
    }

    private static readonly Dictionary<AbilityType, RandomizerMessageProvider> names = new Dictionary<AbilityType, RandomizerMessageProvider>();

    private static readonly Dictionary<AbilityType, RandomizerMessageProvider> helps = new Dictionary<AbilityType, RandomizerMessageProvider>();
}

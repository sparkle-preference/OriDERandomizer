using System;
using System.Collections.Generic;
using Sein.World;
using UnityEngine;
using Object = UnityEngine.Object;

// Water that hurts Ori (all of it until Clean Water) looks diseased: each pool's tint quads are
// recolored in place, and a pool without a vanilla purity switch also gets a donor's surface dressing.
public static class RandomizerDirtyWater {
    // the whole treatment: fog, light rays, falls and rainbows as well as the pools
    public static readonly HashSet<string> Scenes = new HashSet<string> {
        "thornfeltSwampActTwoStart",
        "thornfeltSwampE",
        "thornfeltSwampStompAbility",
        "southMangroveFallsBackgroundB",
        "southMangroveFallsGrenadeEscalationBR",
    };

    // left as drawn: story-only water, Horu Fields' acid, backdrop water that never hurts, and
    // water the game already dresses for its dirty first act
    public static readonly HashSet<string> NeverScenes = new HashSet<string> {
        "horuFieldsB",
        "moonGrottoDoubleJump",
        "moonGrottoDoubleJumpIntroductionArt",
        "moonGrottoGumosHideoutB",
        "moonGrottoLaserPuzzle",
        "moonGrottoLaserPuzzleB",
        "moonGrottoRopeBridge",
        "sorrowPassEntranceA",
        "sunkenGladesSpiritCavernWalljumpB",
        "titleScreenSwallowsNest",
        "swallowsNestBridgeNight",
        "swallowsNestBridgeBuilding",
        "swallowsNestGettingBerries",
        "swallowsNestTheGift",
        "kuroMomentTree",
        "kuroMomentTreeDuplicate",
        "ginsoEntranceIntro",
        "outroRunaway",
    };

    // pools with nothing of their own to recolor that still get generated masks
    public static readonly HashSet<string> MaskScenes = new HashSet<string> {
        "catAndMouseMid",
    };

    // art over a pool but outside any water group, recolored by name
    public static readonly Dictionary<string, string[]> ArtScenes = new Dictionary<string, string[]> {
        { "catAndMouseMid", new[] { Mask, "glow", "sharedFogK" } },
    };

    public class Sunk {
        public float Below;
        public string[] Names;
        public string[] Branches = { "art" };
    }

    // underwater glows and tint quads outside any water group: the blue ones with these names, below the line
    public static readonly Dictionary<string, Sunk> SunkArt = new Dictionary<string, Sunk> {
        { "thornfeltSwampActTwoStart", new Sunk { Below = -137f, Names = new[] { "SpirittreeCutsceneFlowerGlowA", "sharedCircleGlowA" } } },
        { "thornfeltSwampE", new Sunk { Below = -137f, Names = new[] { "SpirittreeCutsceneFlowerGlowA", "sharedCircleGlowA" } } },
        { "thornfeltSwampMoonGrottoTransition", new Sunk { Below = -150f, Names = new[] { "glow", "radialGlow", "sharedFogK" }, Branches = new[] { "art", "*unsorted" } } },
        { "southMangroveFallsBackgroundB", new Sunk { Below = -540f, Names = new[] { "glow" } } },
        { "southMangroveFallsGrenadeEscalationB", new Sunk { Below = -540f, Names = new[] { "glow", "water" }, Branches = new[] { "art", "water" } } },
    };

    // scenes whose fog clouds go dark while the water is dirty (the main Swamp room, below it, its backdrop)
    public static readonly HashSet<string> MistScenes = new HashSet<string> {
        "thornfeltSwampActTwoStart",
        "thornfeltSwampE",
        "thornfeltSwampBackgroundA",
    };

    // a scene the camera crosses into partway down a neighbor's pool: dirty, it takes the neighbor's fog
    public static readonly Dictionary<string, string> FogLike = new Dictionary<string, string> {
        { "thornfeltSwampE", "thornfeltSwampActTwoStart" },
    };

    private static readonly Dictionary<string, Gradient> cleanFogs = new Dictionary<string, Gradient>();

    // bright scenes whose water mirrors a blue sky: the surface's reflection and refraction turn
    public static readonly HashSet<string> ReflScenes = new HashSet<string> {
        "southMangroveFallsBackgroundB",
        "southMangroveFallsGrenadeEscalationBR",
    };

    public static Color ReflTint = new Color(0.95f, 0.55f, 1f);
    public static Color RefrTint = new Color(0.14f, 0.02f, 0.20f);

    public static Color MistTint = new Color(0.16f, 0.03f, 0.20f);

    // animated water outside any pool group: the Swamp drain's falling surface
    public static readonly Dictionary<string, string[]> DrainScenes = new Dictionary<string, string[]> {
        { "thornfeltSwampMoonGrottoTransition", new[] { "*releaseWaterSequence/timelineSequence/topWater" } },
    };

    public static Color DrainTint = new Color(0.20f, 0.01f, 0.25f);

    // pool groups a sequence switches off mid-scene: what they showed fades out instead of popping
    public static readonly Dictionary<string, string[]> FadeOnDrain = new Dictionary<string, string[]> {
        { "thornfeltSwampMoonGrottoTransition", new[] { "water/waterGroupB" } },
    };

    public static float FadeSeconds = 2f;

    public class Seam {
        public string Group;
        public float X;
    }

    // a group's tint masks the scene cuts at x while its water runs on next door: mirrored across the cut
    public static readonly Dictionary<string, Seam[]> SeamMirrors = new Dictionary<string, Seam[]> {
        { "thornfeltSwampMoonGrottoTransition", new[] { new Seam { Group = "water/waterGroupC", X = 680.9f } } },
    };

    // the mask texture stops this share of its width short of the quad's edge
    public static float SeamOverlap = 0.003f;

    // dry corners of a listed scene: rays inside stay lit, and while Ori is inside the fog eases back to clean
    public static readonly Dictionary<string, Rect[]> ClearAreas = new Dictionary<string, Rect[]> {
        { "southMangroveFallsGrenadeEscalationBR", new[] { Rect.MinMaxRect(513f, -556f, 545f, -522f) } },
    };

    // how far outside a clear area the dirty fog is back in full
    public static float ClearPadding = 3f;

    public class Clear {
        public CameraSettingsZone Zone;
        public Rect Area;
    }

    // the setting: off, no scene is dressed and a look already placed goes back to clean
    public static bool Enabled {
        get { return RandomizerSettings.Customization.DirtyWater; }
    }

    private static bool wasEnabled = true;

    // scenes loaded while the setting was off get dressed once it is back on
    public static void Tick() {
        var on = Enabled;
        if (on && !wasEnabled) {
            DressLoaded();
        }

        wasEnabled = on;
    }

    // scenes whose every waterfall pours the dirty water
    public static readonly HashSet<string> FallScenes = new HashSet<string>();

    public static string DonorScene = "thornfeltSwampA";

    private const string Mask = "linearLightGradientMask";
    private const string FlameMask = "spiritFlameGradientMask";
    private const string Glow = "sharedCircleGlowB";
    private const string Duckweed = "sharedWaterDuckweedB";
    private const string Lightray = "sharedLightrayF";

    // pool-shaped kinds stretch with the pool; the rest keep their size
    private static readonly HashSet<string> Stretch = new HashSet<string> { Mask, Duckweed };

    // wall art (tentacles, veins, blowing sand) never travels
    private static readonly HashSet<string> Wanted = new HashSet<string> {
        Mask, Glow, "diseasedWaterTopSlimeA", "diseasedWaterTopSlimeB", "diseasedWaterTopSlimeC",
        "diseasedWaterBubbleA", "diseasedWaterBubbleB", Duckweed, "bubbles",
    };

    // Tuning, all harness-adjustable; Reapply() pushes palette changes to placed looks.
    public static Color Body = new Color(0.30f, 0.02f, 0.32f);
    public static Color Deep = new Color(0.14f, 0f, 0.16f);
    public static Color Bright = new Color(0.42f, 0.02f, 0.45f);
    public static Color GlowTint = new Color(0.75f, 0.1f, 0.65f);
    public static Color FlameTint = new Color(0.25f, 0f, 0.32f);
    public static bool HideRays = true;
    // the depth fog (the surface's mirror image too) is rebuilt as FogFloor + FogTint * brightness
    public static bool TintFog = true;
    public static Color FogTint = new Color(0.45f, 0.10f, 0.52f);
    public static Color FogFloor = new Color(0.05f, 0f, 0.06f);
    // a fall can't go (the sky behind it is the camera's clear color), so it is tinted; its rainbow goes
    public static bool HideWaterfalls = false;
    public static bool TintFalls = true;
    public static Color FallTint = new Color(0.22f, 0.08f, 0.28f);

    // generated masks only where a pool has no tint of its own; the surface slime and bubble
    // sprites come out black on a bright backdrop, so they stay home
    public static int MaskCopies = 1;
    public static int BasinMasks = 0;
    public static float MaskWidth = 1.6f;
    public static Color MaskColor = new Color(0.45f, 0.02f, 0.45f, 0.60f);

    // the donor's one small, intense glow reads as a lamp under the swimmer
    public static float MinGlowWidth = 10f;

    // a piece straddling the water line would veil an overhang (a tunnel's ceiling), so it hangs from the surface
    public static bool HangBelowSurface = true;

    // pieces keep the donor's depths, so a pool set back further than this would wear them in midair
    public static float BackdropZ = 1.5f;

    // a two-look pool's clean tint masks, copied purple where its diseased set has no tint
    public static bool CopyCleanTints = true;

    public static readonly Dictionary<string, int> MaskCopiesFor = new Dictionary<string, int>();

    // a scene whose purple should run darker than the rest
    public static readonly Dictionary<string, float> StrengthFor = new Dictionary<string, float>();

    private static float StrengthIn(string scene) {
        float strength;
        return StrengthFor.TryGetValue(scene, out strength) ? strength : 1f;
    }

    // a scene whose recolored quads go only part of the way from their clean color to the tint
    public static readonly Dictionary<string, float> MixFor = new Dictionary<string, float> {
        { "forlornRuinsKuroHideStreamlined", 0.5f },
    };

    private static float MixIn(string scene) {
        float mix;
        return MixFor.TryGetValue(scene, out mix) ? mix : 1f;
    }

    // depth-only occluders whose coarse outlines cut the purple in a stair-step; off while the look shows
    public static readonly Dictionary<string, string[]> Unoccluded = new Dictionary<string, string[]> {
        { "forlornRuinsKuroHideStreamlined", new[] { "earlyZParent/earyZMesh0" } },
    };

    public static readonly HashSet<string> Skip = new HashSet<string> {
        "diseasedWaterTopSlimeA", "diseasedWaterTopSlimeB", "diseasedWaterTopSlimeC", "diseasedWaterBubbleA", "diseasedWaterBubbleB", "bubbles",
    };

    private class Piece {
        public string Kind;
        public GameObject Template;
        public float Dx;
        public float Dy;
        public float Z;
    }

    private class Layout {
        public string Donor;
        public float Width;
        public float Depth;
        public readonly List<Piece> Pieces = new List<Piece>();
        public readonly HashSet<string> Kinds = new HashSet<string>();
    }

    private static Layout layout;
    private static Transform templateHolder;
    private static float donorAskedAt = -1f;
    private static readonly List<string> donorPins = new List<string>();

    public static bool Ready {
        get { return layout != null; }
    }

    public static void OnScene(SceneRoot root) {
        if (!Enabled) {
            return;
        }

        try {
            Dress(root);
        } catch (Exception e) {
            Randomizer.LogError("dirty water: " + root.name + ": " + e.Message);
        }
    }

    // The donor lends the dressing of its widest pool; pieces over its side pool stay.
    public static string Harvest() {
        if (layout != null) {
            return "layout from " + layout.Donor + " (" + layout.Pieces.Count + " pieces)";
        }

        foreach (var found in Resources.FindObjectsOfTypeAll(typeof(WaterPurityLogic))) {
            var purity = found as WaterPurityLogic;
            if (purity == null || purity.DiseasedGroup == null || purity.transform.root.name != DonorScene) {
                continue;
            }

            var group = purity.transform.parent ?? purity.transform;
            UberWaterControl control = null;
            foreach (var found2 in group.GetComponentsInChildren<UberWaterControl>(true)) {
                if (control == null || found2.Boundary.width > control.Boundary.width) {
                    control = found2;
                }
            }

            if (control == null) {
                continue;
            }

            var candidate = new Layout { Donor = DonorScene, Width = control.Boundary.width, Depth = control.transform.lossyScale.y };
            var x0 = control.Boundary.x;
            var xc = x0 + candidate.Width / 2f;
            var ys = control.transform.position.y;
            foreach (var renderer in purity.DiseasedGroup.GetComponentsInChildren<Renderer>(true)) {
                if (!Wanted.Contains(renderer.name)) {
                    continue;
                }

                var p = renderer.transform.position;
                var inside = p.x >= x0 - 2f && p.x <= x0 + candidate.Width + 2f;
                if (!inside && renderer.name != Mask) {
                    continue;
                }

                if (renderer.name == Glow && Mathf.Abs(renderer.transform.lossyScale.x) < MinGlowWidth) {
                    continue;
                }

                candidate.Pieces.Add(new Piece { Kind = renderer.name, Dx = (p.x - xc) / candidate.Width, Dy = p.y - ys, Z = p.z, Template = Clone(renderer.gameObject) });
                candidate.Kinds.Add(renderer.name);
            }

            layout = candidate;
            Randomizer.log("dirty water: layout from " + layout.Donor + ": " + layout.Pieces.Count + " pieces, " + layout.Kinds.Count + " kinds, pool "
                + layout.Width.ToString("0.0") + " x " + layout.Depth.ToString("0.0"));
            return "layout from " + layout.Donor + " (" + layout.Pieces.Count + " pieces)";
        }

        return "no donor in memory";
    }

    // A disabled preload is enough to harvest from; it is released again afterwards.
    private static void AskForDonor() {
        if (donorAskedAt >= 0f && Time.realtimeSinceStartup - donorAskedAt < 15f) {
            return;
        }

        donorAskedAt = Time.realtimeSinceStartup;
        var manager = Core.Scenes.Manager;
        if (manager == null || manager.GetSceneManagerScene(DonorScene) != null) {
            return;
        }

        var meta = manager.GetSceneInformation(DonorScene);
        if (meta == null) {
            Randomizer.LogError("dirty water: no scene called " + DonorScene);
            return;
        }

        var before = new List<SceneManagerScene>(manager.ActiveScenes);
        manager.PreloadScene(meta);
        foreach (var scene in manager.ActiveScenes) {
            if (!before.Contains(scene) && scene.PreventUnloading) {
                donorPins.Add(scene.MetaData.Scene);
            }
        }

        Randomizer.log("dirty water: preloading " + DonorScene + " (" + donorPins.Count + " scenes pinned)");
    }

    // only the pins the preload made: anyone else's (a warp's, the preloader's) stay
    private static void ReleaseDonor() {
        var manager = Core.Scenes.Manager;
        if (manager == null) {
            return;
        }

        foreach (var name in donorPins) {
            var scene = manager.GetSceneManagerScene(name);
            if (scene != null) {
                scene.PreventUnloading = false;
            }
        }

        donorPins.Clear();
    }

    public static string Describe() {
        if (layout == null) {
            return "no layout";
        }

        var lines = new List<string> { layout.Donor + " pool " + layout.Width.ToString("0.0") + " x " + layout.Depth.ToString("0.0") };
        foreach (var piece in layout.Pieces) {
            lines.Add(piece.Kind.PadRight(26) + " dx " + piece.Dx.ToString("+0.00;-0.00") + "  dy " + piece.Dy.ToString("+0.0;-0.0") + "  z " + piece.Z.ToString("0.0")
                + "  scale " + piece.Template.transform.localScale.ToString() + "  rot " + piece.Template.transform.eulerAngles.ToString("0"));
        }

        return string.Join("\n", lines.ToArray());
    }

    private static GameObject Clone(GameObject source) {
        if (templateHolder == null) {
            var holder = new GameObject("dirtyWaterTemplates");
            Object.DontDestroyOnLoad(holder);
            templateHolder = holder.transform;
        }

        var clone = Object.Instantiate(source) as GameObject;
        clone.name = source.name;
        clone.SetActive(false);
        clone.transform.SetParent(templateHolder, true);
        // the scene destroys its generated materials on unload, references or not
        foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true)) {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++) {
                if (materials[i] != null) {
                    materials[i] = new Material(materials[i]) { name = materials[i].name + " (dirty water)" };
                }
            }

            renderer.sharedMaterials = materials;
        }

        return clone;
    }

    // Listed scenes get the whole treatment; elsewhere switchless pools are dressed and the fog,
    // rays and falls stay the game's.
    public static void Dress(SceneRoot root) {
        if (root.transform.Find("dirtyWaterLook") != null || NeverScenes.Contains(root.name)) {
            return;
        }

        var started = Time.realtimeSinceStartup;
        var full = Scenes.Contains(root.name);
        var fallsOnly = FallScenes.Contains(root.name);
        var misty = MistScenes.Contains(root.name);
        Sunk sunk;
        SunkArt.TryGetValue(root.name, out sunk);
        var groups = LooseGroups(root);
        var all = WaterGroups(root);
        if (!full && !fallsOnly && !misty && sunk == null && all.Count == 0) {
            return;
        }

        var look = new GameObject("dirtyWaterLook");
        look.transform.SetParent(root.transform, false);
        var component = look.AddComponent<DirtyWaterLook>();
        component.Root = root;
        component.Groups = groups;
        component.AllGroups = all;
        // only loose pools take donor pieces; a scene without one never asks for the donor
        component.Placed = groups.Count == 0;
        component.Natives = FindNatives(all);
        string[] art;
        if (ArtScenes.TryGetValue(root.name, out art)) {
            AddNew(component.Natives, FindArtNatives(root, art));
        }

        if (misty) {
            AddNew(component.Natives, FindMist(root));
        }

        if (sunk != null) {
            AddNew(component.Natives, FindSunk(root, sunk));
        }

        if (ReflScenes.Contains(root.name)) {
            foreach (var top in root.GetComponentsInChildren<UberWaterTop>(true)) {
                var renderer = top.GetComponent<Renderer>();
                var control = top.transform.parent != null ? top.transform.parent.GetComponent<UberWaterControl>() : null;
                if (renderer != null && control != null && IsWater(control)) {
                    component.Tops.Add(renderer);
                }
            }
        }

        string[] drains;
        if (DrainScenes.TryGetValue(root.name, out drains)) {
            AddNew(component.Natives, FindDrain(root, drains));
        }

        string[] occluders;
        if (Unoccluded.TryGetValue(root.name, out occluders)) {
            foreach (var path in occluders) {
                var occluder = root.transform.Find(path);
                var renderer = occluder != null ? occluder.GetComponent<Renderer>() : null;
                if (renderer != null) {
                    component.Occluders.Add(renderer);
                }
            }
        }

        string[] fading;
        if (FadeOnDrain.TryGetValue(root.name, out fading)) {
            foreach (var path in fading) {
                var group = root.transform.Find(path);
                if (group != null) {
                    component.Fading.Add(group.gameObject);
                }
            }
        }

        if (CopyCleanTints) {
            component.CleanCopies = CleanCopies(all, StrengthIn(root.name), MixIn(root.name));
        }

        component.CleanCopies.AddRange(SeamCopies(root, component.Natives, StrengthIn(root.name)));

        if (full) {
            component.Rays = FindRays(root);
            component.Falls = FindWaterfalls(root.transform);
            component.Rainbows = FindRainbows(root);
            foreach (var fog in root.GetComponentsInChildren<FogGradientController>(true)) {
                var entry = new Fog { Controller = fog, Clean = fog.FogGradient, Scene = root.name };
                component.Fogs.Add(entry);
                if (!cleanFogs.ContainsKey(root.name)) {
                    cleanFogs[root.name] = fog.FogGradient;
                }

                // before the scene first builds its camera settings from it
                if (TintFog && (ForcedOn || !Events.WaterPurified)) {
                    fog.FogGradient = Purpled(entry.Source, StrengthIn(root.name));
                }
            }

            Rect[] clear;
            if (ClearAreas.TryGetValue(root.name, out clear)) {
                component.Rays.RemoveAll(ray => Array.Exists(clear, area => area.Contains(Footprint(ray.gameObject).center)));
                var settings = root.SceneSettings;
                var sceneFog = settings != null ? component.Fogs.Find(f => f.Controller == settings.SceneFogSettings) : null;
                if (sceneFog != null) {
                    foreach (var area in clear) {
                        component.Clears.Add(new Clear { Zone = ClearZone(look.transform, area, settings.CameraSettings, sceneFog), Area = area });
                    }
                }
            }
        } else if (fallsOnly) {
            component.Falls = FindWaterfalls(root.transform, true);
        } else {
            // a fall that shares a top-level branch with a switchless pool pours into it
            foreach (var group in groups) {
                var top = group;
                while (top.parent != null && top.parent != root.transform) {
                    top = top.parent;
                }

                foreach (var fall in FindWaterfalls(top)) {
                    if (!component.Falls.Contains(fall)) {
                        component.Falls.Add(fall);
                    }
                }
            }
        }

        component.FallMaterials = FindFallMaterials(component.Falls);

        Randomizer.log("dirty water: " + root.name + (full ? " (listed)" : "") + " has " + groups.Count + " of " + all.Count + " water groups loose, " + component.Natives.Count
            + " native quads, " + CopiesIn(component.CleanCopies) + " clean copies, " + component.Rays.Count + " light rays, " + component.Falls.Count + " waterfall objects, " + component.Fogs.Count + " fogs, "
            + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms");
    }

    // A pool's group is its parent, or the grandparent past an uberWaters holder or a bare gameObject
    // wrapper: where a scene keeps the pool's masks, glows, side quads and purity switch.
    private static List<Transform> WaterGroups(SceneRoot root) {
        var groups = new List<Transform>();
        foreach (var control in root.GetComponentsInChildren<UberWaterControl>(true)) {
            if (!IsWater(control)) {
                continue;
            }

            var t = GroupOf(control, root.transform);
            if (t != null && !groups.Contains(t)) {
                groups.Add(t);
            }
        }

        return groups;
    }

    // Mount Horu's lava runs on the same water system
    private static bool IsWater(UberWaterControl control) {
        return control.name.IndexOf("lava", StringComparison.OrdinalIgnoreCase) < 0
            && (control.TopMaterial == null || control.TopMaterial.name.StartsWith("seinWater"));
    }

    // a pool straight under the scene root is its own group
    private static Transform GroupOf(UberWaterControl control, Transform root) {
        var t = control.transform.parent;
        if (t != null && t != root && (t.name.EndsWith("berWaters") || t.name == "gameObject")) {
            t = t.parent;
        }

        return t == null || t == root ? control.transform : t;
    }

    private static bool Switched(Transform group) {
        return group.GetComponentInChildren<WaterPurityLogic>(true) != null;
    }

    // The groups without a purity switch: the ones whose surface dressing is ours to add.
    private static List<Transform> LooseGroups(SceneRoot root) {
        var loose = new List<Transform>();
        foreach (var group in WaterGroups(root)) {
            if (!Switched(group)) {
                loose.Add(group);
            }
        }

        return loose;
    }

    private static List<UberWaterControl> ControlsIn(List<Transform> groups) {
        var controls = new List<UberWaterControl>();
        foreach (var group in groups) {
            foreach (var control in group.GetComponentsInChildren<UberWaterControl>(true)) {
                if (IsWater(control)) {
                    controls.Add(control);
                }
            }
        }

        return controls;
    }

    private static Gradient Purpled(Gradient clean, float strength) {
        var keys = clean.colorKeys;
        for (var i = 0; i < keys.Length; i++) {
            var c = keys[i].color;
            var lum = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) * strength;
            keys[i].color = new Color(FogFloor.r + FogTint.r * lum, FogFloor.g + FogTint.g * lum, FogFloor.b + FogTint.b * lum, c.a);
        }

        var dirty = new Gradient();
        dirty.SetKeys(keys, clean.alphaKeys);
        return dirty;
    }

    // Every distinct material the fall objects draw with, and its clean color.
    private static List<Native> FindFallMaterials(List<GameObject> falls) {
        var found = new List<Native>();
        var seen = new HashSet<Material>();
        foreach (var fall in falls) {
            foreach (var renderer in fall.GetComponentsInChildren<Renderer>(true)) {
                var material = renderer.sharedMaterial;
                // a grab-pass distortion has a color too, but its alpha is the effect
                if (material == null || seen.Contains(material) || !material.HasProperty("_Color") || renderer.GetComponent("UberShaderBlockGrabPass") != null) {
                    continue;
                }

                seen.Add(material);
                found.Add(new Native { Renderer = renderer, Clean = material.color });
            }
        }

        return found;
    }

    private static List<GameObject> FindRainbows(SceneRoot root) {
        var rainbows = new List<GameObject>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
            if (renderer.name.StartsWith("rainbow") && renderer.gameObject.activeSelf) {
                rainbows.Add(renderer.gameObject);
            }
        }

        return rainbows;
    }

    // fall sprites with their splash, spray, foot actor and sound spots; spritesOnly where spray names mean other things
    private static List<GameObject> FindWaterfalls(Transform under, bool spritesOnly = false) {
        var falls = new List<GameObject>();
        foreach (var t in under.GetComponentsInChildren<Transform>(true)) {
            var n = t.name;
            if (spritesOnly ? n.StartsWith("sharedWaterfall") : n.IndexOf("aterfall", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("aterFall", StringComparison.Ordinal) >= 0
                || n == "waterSurfaceSplash" || n == "waterParticlesSplash" || n == "rainParticleSplash" || n == "particleWaterSplashSprites"
                || n == "sharedLavaFountainA" || n == "bubbles" || n == "bubblesBlurred") {
                if (t.gameObject.activeSelf) {
                    falls.Add(t.gameObject);
                }
            }
        }

        return falls;
    }

    // every colored quad in a switchless group is water dressing; a two-look group keeps the game's dirty look
    private static List<Native> FindNatives(List<Transform> groups) {
        var natives = new List<Native>();
        foreach (var group in groups) {
            if (Switched(group)) {
                continue;
            }

            foreach (var renderer in group.GetComponentsInChildren<Renderer>(true)) {
                var material = renderer.sharedMaterial;
                if (!renderer.enabled || material == null || !material.HasProperty("_Color") || renderer.name == Lightray) {
                    continue;
                }

                natives.Add(new Native { Renderer = renderer, Clean = material.color });
            }
        }

        return natives;
    }

    // a two-look pool's clean mask with no diseased tint over it gets a purple copy under the diseased set
    private static List<GameObject> CleanCopies(List<Transform> groups, float strength, float mix) {
        var holders = new List<GameObject>();
        foreach (var group in groups) {
            foreach (var purity in group.GetComponentsInChildren<WaterPurityLogic>(true)) {
                if (purity.CleanGroup == null || purity.DiseasedGroup == null) {
                    continue;
                }

                var covered = new List<Rect>();
                foreach (var renderer in purity.DiseasedGroup.GetComponentsInChildren<Renderer>(true)) {
                    if (renderer.name == Mask && Tints(renderer)) {
                        covered.Add(Footprint(renderer.gameObject));
                    }
                }

                Transform holder = null;
                foreach (var renderer in purity.CleanGroup.GetComponentsInChildren<Renderer>(true)) {
                    var center = Footprint(renderer.gameObject).center;
                    if (renderer.name != Mask || !renderer.enabled || !renderer.gameObject.activeSelf || !Tints(renderer) || covered.Exists(r => r.Contains(center))) {
                        continue;
                    }

                    if (holder == null) {
                        holder = new GameObject("dirtyWaterCleanCopies").transform;
                        holder.SetParent(purity.DiseasedGroup.transform, false);
                        holder.gameObject.SetActive(false);
                        holders.Add(holder.gameObject);
                    }

                    // a clone starts with its source's local values as its world ones
                    var copy = Object.Instantiate(renderer.gameObject) as GameObject;
                    copy.name = renderer.name;
                    var source = renderer.transform;
                    var outer = holder.lossyScale;
                    copy.transform.SetParent(holder, false);
                    copy.transform.position = source.position;
                    copy.transform.rotation = source.rotation;
                    copy.transform.localScale = new Vector3(source.lossyScale.x / outer.x, source.lossyScale.y / outer.y, source.lossyScale.z / outer.z);
                    var copyRenderer = copy.GetComponent<Renderer>();
                    var clean = renderer.sharedMaterial.color;
                    copyRenderer.material.color = TintFor(new Native { Renderer = copyRenderer, Clean = clean }, strength, clean.a, mix);
                    copy.SetActive(true);
                }
            }
        }

        return holders;
    }

    // a recolored quad with an edge on the cut, copied mirrored across it into a holder in its group
    private static List<GameObject> SeamCopies(SceneRoot root, List<Native> natives, float strength) {
        var holders = new List<GameObject>();
        Seam[] seams;
        if (!SeamMirrors.TryGetValue(root.name, out seams)) {
            return holders;
        }

        foreach (var seam in seams) {
            var group = root.transform.Find(seam.Group);
            if (group == null) {
                continue;
            }

            Transform holder = null;
            foreach (var renderer in group.GetComponentsInChildren<Renderer>(true)) {
                var native = natives.Find(n => n.Renderer == renderer);
                var foot = Footprint(renderer.gameObject);
                var right = Mathf.Abs(foot.xMax - seam.X) < 0.2f;
                if (native == null || !right && Mathf.Abs(foot.xMin - seam.X) >= 0.2f) {
                    continue;
                }

                if (holder == null) {
                    holder = new GameObject("dirtyWaterSeamCopies").transform;
                    holder.SetParent(group, false);
                    holder.gameObject.SetActive(false);
                    holders.Add(holder.gameObject);
                }

                var copy = Object.Instantiate(renderer.gameObject) as GameObject;
                copy.name = renderer.name;
                var source = renderer.transform;
                var outer = holder.lossyScale;
                var edge = right ? foot.xMax : foot.xMin;
                var back = (right ? -1f : 1f) * SeamOverlap * foot.width;
                var angles = source.eulerAngles;
                copy.transform.SetParent(holder, false);
                copy.transform.position = new Vector3(2f * edge - source.position.x + back, source.position.y, source.position.z);
                copy.transform.rotation = Quaternion.Euler(angles.x, -angles.y, -angles.z);
                copy.transform.localScale = new Vector3(-source.lossyScale.x / outer.x, source.lossyScale.y / outer.y, source.lossyScale.z / outer.z);
                var copyRenderer = copy.GetComponent<Renderer>();
                copyRenderer.material.color = TintFor(new Native { Renderer = copyRenderer, Clean = native.Clean, Mist = native.Mist, Drain = native.Drain }, strength, native.Clean.a, MixIn(root.name));
                copy.SetActive(true);
            }
        }

        return holders;
    }

    // The game's camera zone with the scene's settings and clean fog. A zone weighs itself by the camera,
    // which runs ahead of Ori, so its rect is grown past the camera's reach and the look drives it from Ori.
    private static CameraSettingsZone ClearZone(Transform parent, Rect area, CameraSettingsAsset settings, Fog fog) {
        var go = new GameObject("dirtyWaterClearZone");
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        // a zone reads its rect from its transform when it is enabled
        var outer = parent.lossyScale;
        var margin = 2f * (ClearPadding + 20f);
        go.transform.position = new Vector3(area.center.x, area.center.y, 0f);
        go.transform.localScale = new Vector3((area.width + margin) / outer.x, (area.height + margin) / outer.y, 1f);
        var zone = go.AddComponent<CameraSettingsZone>();
        zone.Settings = settings;
        zone.FogGradient = fog.Clean;
        zone.FogRange = fog.Controller.FogRange;
        zone.AnimatedStrength = 0f;
        return zone;
    }

    // full inside the area, nothing ClearPadding outside it
    private static float ClearAt(Rect area, Vector2 p) {
        return Mathf.Min(Mathf.Min(Mathf.InverseLerp(area.xMin - ClearPadding, area.xMin, p.x), Mathf.InverseLerp(area.xMax + ClearPadding, area.xMax, p.x)),
            Mathf.Min(Mathf.InverseLerp(area.yMin - ClearPadding, area.yMin, p.y), Mathf.InverseLerp(area.yMax + ClearPadding, area.yMax, p.y)));
    }

    private static bool Tints(Renderer renderer) {
        var material = renderer.sharedMaterial;
        return material != null && material.HasProperty("_Color") && material.color.a > 0.02f;
    }

    private static int CopiesIn(List<GameObject> holders) {
        var n = 0;
        foreach (var holder in holders) {
            n += holder.transform.childCount;
        }

        return n;
    }

    // from the mesh, not Renderer.bounds: an inactive set has none
    private static Rect Footprint(GameObject go) {
        var filter = go.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) {
            return new Rect();
        }

        var b = filter.sharedMesh.bounds;
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var i = 0; i < 8; i++) {
            var corner = go.transform.TransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static bool Under(Transform t, List<Transform> ancestors) {
        for (var p = t; p != null; p = p.parent) {
            if (ancestors.Contains(p)) {
                return true;
            }
        }

        return false;
    }

    private static List<Renderer> FindRays(SceneRoot root) {
        var rays = new List<Renderer>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
            if (renderer.name == Lightray && renderer.enabled) {
                rays.Add(renderer);
            }
        }

        return rays;
    }

    // hue only (the alpha stays the scene's or an animator's); mix is how far from clean toward the tint
    private static Color TintFor(Native native, float strength, float alpha, float mix) {
        var clean = native.Clean;
        Color tint;
        var name = native.Renderer.name;
        if (native.Mist) {
            tint = MistTint;
        } else if (native.Drain) {
            tint = DrainTint;
        } else if (name == FlameMask) {
            tint = FlameTint;
        } else if (name.EndsWith("plane") || name.Contains("Fog")) {
            tint = Body;
        } else if (name != Mask) {
            tint = GlowTint;
        } else if (clean.a >= 0.99f) {
            tint = Deep;
        } else if (Mathf.Max(clean.r, Mathf.Max(clean.g, clean.b)) > 0.9f) {
            tint = Bright;
        } else {
            tint = Body;
        }

        // never brighter than the color it replaces: a dim quad stays a dim quad
        var cleanLum = Mathf.Max(clean.r, Mathf.Max(clean.g, clean.b));
        var tintLum = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
        if (tintLum > cleanLum && tintLum > 0f) {
            strength *= cleanLum / tintLum;
        }

        return Color.Lerp(new Color(clean.r, clean.g, clean.b, alpha), new Color(tint.r * strength, tint.g * strength, tint.b * strength, alpha), mix);
    }

    // a pool whose group carries its own tint quads (not just an edge line) needs no generated masks
    private static bool HasTint(Transform group, List<Native> natives) {
        foreach (var native in natives) {
            if (native.Renderer != null && !native.Renderer.name.EndsWith("plane") && native.Clean.a > 0.02f && Under(native.Renderer.transform, new List<Transform> { group })) {
                return true;
            }
        }

        return false;
    }

    // the donor layout repeats per donor width; deeper pieces follow the pool's depth
    private static int Place(UberWaterControl control, Transform parent, bool nativeMasks) {
        var w = control.Boundary.width;
        var x0 = control.Boundary.x;
        var ys = control.transform.position.y;
        var depth = Mathf.Clamp(control.transform.lossyScale.y / layout.Depth, 0.4f, 2f);
        var mid = x0 + w / 2f;
        var n = nativeMasks ? 0 : Masks(control, parent, MaskCopiesIn(control.transform.root.name));
        if (w < 12f) {
            n += Spawn(FirstOf(Glow), parent, mid, ys - 2.5f, 0f, 0f);
            return n;
        }

        foreach (var piece in layout.Pieces) {
            if (piece.Kind != Mask && Stretch.Contains(piece.Kind) && !Skip.Contains(piece.Kind)) {
                n += Spawn(piece, parent, mid + piece.Dx * w, ys + piece.Dy, w / layout.Width * Mathf.Abs(piece.Template.transform.localScale.x), 0f);
            }
        }

        var tiles = Math.Max(1, Mathf.RoundToInt(w / layout.Width));
        var tileWidth = w / tiles;
        for (var t = 0; t < tiles; t++) {
            var center = x0 + tileWidth * (t + 0.5f);
            foreach (var piece in layout.Pieces) {
                if (Stretch.Contains(piece.Kind) || Skip.Contains(piece.Kind)) {
                    continue;
                }

                var y = piece.Dy > -5f ? piece.Dy : piece.Dy * depth;
                n += Spawn(piece, parent, center + piece.Dx * tileWidth, ys + y, 0f, 0f, HangBelowSurface && piece.Dy > -5f ? ys : float.NaN);
            }
        }

        return n;
    }

    // a generated mask spans the pool from its surface to its cross plane's foot, drawn just above the
    // cross plane and wider than the pool so the art in front of the water covers its hard sides
    private static int Masks(UberWaterControl control, Transform parent, int copies) {
        var w = control.Boundary.width;
        var h = control.transform.lossyScale.y;
        var cross = control.GetComponentInChildren<UberWaterCross>(true);
        var crossRenderer = cross != null ? cross.GetComponent<Renderer>() : null;
        var n = 0;
        for (var c = 0; c < copies; c++) {
            n += Spawn(FirstOf(Mask), parent, control.Boundary.x + w / 2f, control.transform.position.y - h / 2f, w * MaskWidth, h);
            var renderer = parent.GetChild(parent.childCount - 1).GetComponent<Renderer>();
            if (renderer == null) {
                continue;
            }

            renderer.sortingLayerName = crossRenderer != null ? crossRenderer.sortingLayerName : "center";
            renderer.sortingOrder = crossRenderer != null ? crossRenderer.sortingOrder + 1 : 4000;
            renderer.material.color = MaskColor;
            // in front of the water plane: the scene's depth-only meshes hide anything behind it
            if (crossRenderer != null) {
                var p = renderer.transform.position;
                renderer.transform.position = new Vector3(p.x, p.y, crossRenderer.transform.position.z - 0.05f);
            }
        }

        return n;
    }

    // A pool whose span sits inside a wider one is a basin of the same water.
    private static bool Contained(UberWaterControl control, UberWaterControl[] all) {
        foreach (var other in all) {
            if (other == control || other.Boundary.width <= control.Boundary.width) {
                continue;
            }

            var inside = control.Boundary.x >= other.Boundary.x - 1f && control.Boundary.x + control.Boundary.width <= other.Boundary.x + other.Boundary.width + 1f;
            if (inside && control.transform.position.y <= other.transform.position.y + 1f) {
                return true;
            }
        }

        return false;
    }

    private static int MaskCopiesIn(string scene) {
        int copies;
        return MaskCopiesFor.TryGetValue(scene, out copies) ? copies : MaskCopies;
    }

    private static Piece FirstOf(string kind) {
        foreach (var piece in layout.Pieces) {
            if (piece.Kind == kind) {
                return piece;
            }
        }

        return null;
    }

    // width/height 0 keep the template's size; a surface (not NaN) is a ceiling for its top edge
    private static int Spawn(Piece piece, Transform parent, float x, float y, float width, float height, float surface = float.NaN) {
        if (piece == null || piece.Template == null) {
            return 0;
        }

        var go = Object.Instantiate(piece.Template) as GameObject;
        go.name = piece.Kind;
        go.transform.SetParent(parent, true);
        go.transform.position = new Vector3(x, y, piece.Z);
        // width and height are world units; the holder sits in the scene's own hierarchy
        var scale = go.transform.localScale;
        var outer = parent.lossyScale;
        if (width != 0f) {
            scale.x = Mathf.Sign(scale.x) * width / Mathf.Max(0.001f, Mathf.Abs(outer.x));
        }

        if (height != 0f) {
            scale.y = Mathf.Sign(scale.y) * height / Mathf.Max(0.001f, Mathf.Abs(outer.y));
        }

        go.transform.localScale = scale;
        if (!float.IsNaN(surface)) {
            var top = TopOf(go);
            if (top > surface) {
                go.transform.position -= new Vector3(0f, top - surface, 0f);
            }
        }

        go.SetActive(true);
        return 1;
    }

    // from the mesh, not Renderer.bounds: a piece placed under a hidden pool has none yet
    private static float TopOf(GameObject go) {
        var filter = go.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) {
            return float.NegativeInfinity;
        }

        var b = filter.sharedMesh.bounds;
        var top = float.NegativeInfinity;
        for (var i = 0; i < 8; i++) {
            var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            top = Mathf.Max(top, go.transform.TransformPoint(corner).y);
        }

        return top;
    }

    // Harness entry points: dress whatever target scenes are loaded, or start over.
    public static string DressLoaded() {
        var done = new List<string>();
        foreach (var scene in Core.Scenes.Manager.ActiveScenes) {
            if (scene.SceneRoot != null) {
                Dress(scene.SceneRoot);
                if (scene.SceneRoot.transform.Find("dirtyWaterLook") != null) {
                    done.Add(scene.SceneRoot.name);
                }
            }
        }

        return "dressed " + string.Join(", ", done.ToArray());
    }

    public static string Reset() {
        var removed = 0;
        foreach (var found in Resources.FindObjectsOfTypeAll(typeof(DirtyWaterLook))) {
            var look = found as DirtyWaterLook;
            if (look != null) {
                Object.Destroy(look.gameObject);
                removed++;
            }
        }

        return removed + " looks removed; layout kept";
    }

    // Drops the layout too, so the next look preloads and harvests the donor afresh.
    public static string Forget() {
        Reset();
        layout = null;
        donorAskedAt = -1f;
        if (templateHolder != null) {
            Object.Destroy(templateHolder.gameObject);
            templateHolder = null;
        }

        return "forgot the donor";
    }

    // Harness probe: what one fixed tick of every active look costs, averaged over n ticks.
    public static string Bench(int n) {
        var looks = new List<DirtyWaterLook>();
        foreach (var found in Resources.FindObjectsOfTypeAll(typeof(DirtyWaterLook))) {
            var look = found as DirtyWaterLook;
            if (look != null && look.isActiveAndEnabled) {
                looks.Add(look);
            }
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < n; i++) {
            foreach (var look in looks) {
                look.FixedUpdate();
            }
        }

        watch.Stop();
        return looks.Count + " looks, " + (watch.Elapsed.TotalMilliseconds * 1000.0 / n).ToString("0.00") + " us per fixed tick for all of them";
    }

    public static string Reapply() {
        var n = 0;
        foreach (var found in Resources.FindObjectsOfTypeAll(typeof(DirtyWaterLook))) {
            var look = found as DirtyWaterLook;
            if (look != null && look.Applied) {
                look.Apply(false);
                look.Apply(true);
                n++;
            }
        }

        return n + " looks recolored";
    }

    // Harness probe: what each look holds, and whether every piece still draws.
    public static string Check() {
        var lines = new List<string>();
        foreach (var found in Resources.FindObjectsOfTypeAll(typeof(DirtyWaterLook))) {
            var look = found as DirtyWaterLook;
            if (look == null) {
                continue;
            }

            var dead = new Dictionary<string, int>();
            var total = 0;
            foreach (var holder in look.Holders) {
                if (holder == null) {
                    continue;
                }

                foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true)) {
                    total++;
                    var filter = renderer.GetComponent<MeshFilter>();
                    var material = renderer.sharedMaterial;
                    var what = (filter != null && filter.sharedMesh == null ? "mesh " : "")
                        + (material == null ? "material " : (material.shader == null ? "shader " : "") + (material.mainTexture == null ? "texture " : ""));
                    if (what.Length > 0) {
                        int n;
                        dead.TryGetValue(what, out n);
                        dead[what] = n + 1;
                    }
                }
            }

            var parts = new List<string>();
            foreach (var pair in dead) {
                parts.Add(pair.Value + " x [" + pair.Key.Trim() + "]");
            }

            lines.Add(look.transform.root.name + ": " + total + " pieces, dead " + (parts.Count == 0 ? "none" : string.Join(", ", parts.ToArray()))
                + "; natives " + look.Natives.Count + ", clean copies " + CopiesIn(look.CleanCopies) + ", rays " + look.Rays.Count + ", falls " + look.Falls.Count + ", fogs " + look.Fogs.Count
                + ", applied " + look.Applied + ", wet " + look.Wet + ", placed " + look.Placed);
        }

        var templates = 0;
        var templatesDead = 0;
        if (templateHolder != null) {
            foreach (var renderer in templateHolder.GetComponentsInChildren<Renderer>(true)) {
                templates++;
                var filter = renderer.GetComponent<MeshFilter>();
                if ((filter != null && filter.sharedMesh == null) || renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture == null) {
                    templatesDead++;
                }
            }
        }

        lines.Add("templates: " + templates + ", dead " + templatesDead);
        return string.Join("\n", lines.ToArray());
    }

    public static string Natives() {
        var lines = new List<string>();
        foreach (var found in Resources.FindObjectsOfTypeAll(typeof(DirtyWaterLook))) {
            var look = found as DirtyWaterLook;
            if (look == null) {
                continue;
            }

            lines.Add(look.transform.root.name);
            foreach (var native in look.Natives) {
                if (native.Renderer == null) {
                    continue;
                }

                lines.Add("  " + native.Renderer.name.PadRight(24) + " " + native.Renderer.sortingLayerName + "/" + native.Renderer.sortingOrder
                    + "  clean " + native.Clean + "  now " + native.Renderer.sharedMaterial.color + "  " + native.Renderer.bounds.size.ToString("0"));
            }
        }

        return string.Join("\n", lines.ToArray());
    }

    // Harness probe: dress the scene Ori stands in, target or not, and keep the pieces on.
    public static bool ForcedOn;

    public static string DressHere() {
        var current = Core.Scenes.Manager.CurrentScene;
        foreach (var scene in Core.Scenes.Manager.ActiveScenes) {
            if (scene.SceneRoot != null && current != null && scene.MetaData.Scene == current.Scene) {
                Dress(scene.SceneRoot);
                return "dressed " + scene.SceneRoot.name;
            }
        }

        return "no current scene root";
    }

    public class Native {
        public Renderer Renderer;
        public Color Clean;
        public bool Mist;
        public bool Drain;
    }

    private static void AddNew(List<Native> into, List<Native> more) {
        foreach (var native in more) {
            if (!into.Exists(n => n.Renderer == native.Renderer)) {
                into.Add(native);
            }
        }
    }

    // named art quads centered over a pool's span, from just above its surface to past its cross plane's foot
    private static List<Native> FindArtNatives(SceneRoot root, string[] names) {
        var found = new List<Native>();
        var art = root.transform.Find("art");
        if (art == null) {
            return found;
        }

        var pools = new List<Rect>();
        foreach (var control in root.GetComponentsInChildren<UberWaterControl>(true)) {
            if (IsWater(control)) {
                var b = control.Boundary;
                var top = control.transform.position.y;
                var depth = control.transform.lossyScale.y;
                pools.Add(Rect.MinMaxRect(b.xMin - 15f, top - depth - 15f, b.xMax + 15f, top + 8f));
            }
        }

        foreach (var renderer in art.GetComponentsInChildren<Renderer>(true)) {
            var material = renderer.sharedMaterial;
            if (!renderer.enabled || material == null || !material.HasProperty("_Color") || !Array.Exists(names, n => renderer.name.StartsWith(n))) {
                continue;
            }

            var center = renderer.bounds.center;
            if (pools.Exists(p => p.Contains(new Vector2(center.x, center.y)))) {
                found.Add(new Native { Renderer = renderer, Clean = material.color });
            }
        }

        return found;
    }

    private static List<Native> FindDrain(SceneRoot root, string[] paths) {
        var found = new List<Native>();
        foreach (var path in paths) {
            var branch = root.transform.Find(path);
            if (branch == null) {
                continue;
            }

            foreach (var renderer in branch.GetComponentsInChildren<Renderer>(true)) {
                var material = renderer.sharedMaterial;
                if (material != null && material.HasProperty("_Color")) {
                    found.Add(new Native { Renderer = renderer, Clean = material.color, Drain = true });
                }
            }
        }

        return found;
    }

    // art glows the scene keeps under its water: the blue-leaning ones below the line
    private static List<Native> FindSunk(SceneRoot root, Sunk sunk) {
        var found = new List<Native>();
        foreach (var path in sunk.Branches) {
            var branch = root.transform.Find(path);
            if (branch == null) {
                continue;
            }

            foreach (var renderer in branch.GetComponentsInChildren<Renderer>(true)) {
                var material = renderer.sharedMaterial;
                if (!renderer.enabled || material == null || !material.HasProperty("_Color") || Array.IndexOf(sunk.Names, renderer.name) < 0) {
                    continue;
                }

                var c = material.color;
                if (c.b > c.r + 0.05f && Footprint(renderer.gameObject).center.y <= sunk.Below) {
                    found.Add(new Native { Renderer = renderer, Clean = c });
                }
            }
        }

        return found;
    }

    private static List<Native> FindMist(SceneRoot root) {
        var found = new List<Native>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
            var material = renderer.sharedMaterial;
            if (renderer.name.StartsWith("sharedFog") && material != null && material.HasProperty("_Color")) {
                found.Add(new Native { Renderer = renderer, Clean = material.color, Mist = true });
            }
        }

        return found;
    }

    public class Fog {
        public FogGradientController Controller;
        public Gradient Clean;
        public string Scene;

        // what the dirty fog is made from: the neighbor's clean gradient once seen, else its own
        public Gradient Source {
            get {
                string like;
                Gradient neighbor;
                return FogLike.TryGetValue(Scene, out like) && cleanFogs.TryGetValue(like, out neighbor) ? neighbor : Clean;
            }
        }
    }

    // one per dressed scene: fetches the donor, places pieces once, follows Events.WaterPurified each tick
    public class DirtyWaterLook : MonoBehaviour {
        public SceneRoot Root;
        public bool Placed;
        public List<GameObject> Holders = new List<GameObject>();
        public List<UberWaterControl> HolderControls = new List<UberWaterControl>();
        public List<Transform> Groups = new List<Transform>();
        public List<Transform> AllGroups = new List<Transform>();
        public List<Native> Natives = new List<Native>();
        public List<GameObject> CleanCopies = new List<GameObject>();
        public List<GameObject> Fading = new List<GameObject>();
        public List<Clear> Clears = new List<Clear>();
        public List<Renderer> Occluders = new List<Renderer>();
        public List<Renderer> Rays = new List<Renderer>();
        public List<Renderer> Tops = new List<Renderer>();
        public List<GameObject> Falls = new List<GameObject>();
        public List<Native> FallMaterials = new List<Native>();
        public List<GameObject> Rainbows = new List<GameObject>();
        public List<Fog> Fogs = new List<Fog>();
        public List<UberWaterControl> Controls = new List<UberWaterControl>();
        public bool Applied;
        public bool Wet = true;
        private readonly List<GameObject> hidden = new List<GameObject>();
        private List<Renderer> previewed;
        private readonly List<bool> fadingWasOn = new List<bool>();
        private readonly List<Ghost> ghosts = new List<Ghost>();
        private TransparencyAnimator sheetFade;
        private GameObject sheetEdge;
        private float sheetTop;
        private float sheetPeak;
        private float hauntedAt;
        private int ticks;
        private int fixedTicks;
        private bool wasHere;

        private enum Drift {
            Stay,
            Ride,
            Drain,
        }

        private class Ghost {
            public Renderer Renderer;
            public Color Start;
            public Drift Drift;
            public Vector3 Home;
            public Vector3 HomeScale;
            public Rect Footprint;
        }

        // after the frame's sequences ran, so a group switched off this frame leaves its ghosts this frame
        public void LateUpdate() {
            for (var i = 0; i < Fading.Count; i++) {
                var on = Fading[i] != null && Fading[i].activeInHierarchy;
                if (i == fadingWasOn.Count) {
                    fadingWasOn.Add(on);
                    continue;
                }

                if (fadingWasOn[i] && !on && Applied && Fading[i] != null) {
                    Haunt(Fading[i].transform);
                }

                fadingWasOn[i] = on;
            }

            if (ghosts.Count > 0) {
                FollowSheet();
            }

            var sein = Game.Characters.Sein;
            foreach (var clear in Clears) {
                if (clear.Zone != null) {
                    var target = sein != null ? ClearAt(clear.Area, sein.Position) : 0f;
                    clear.Zone.AnimatedStrength = Mathf.MoveTowards(clear.Zone.AnimatedStrength, target, Time.deltaTime / clear.Zone.Duration);
                }
            }
        }

        // Ghosts go the way the drain's falling sheet does: surface things ride its top edge down, masks
        // shrink from the top, wall art stays, all fading with its opacity (in place without a sheet).
        private void FollowSheet() {
            float drop;
            float k;
            if (sheetFade != null) {
                if (sheetEdge == null || !sheetFade.gameObject.activeInHierarchy) {
                    ClearGhosts();
                    return;
                }

                drop = Mathf.Max(0f, sheetTop - Footprint(sheetEdge).yMax);
                sheetPeak = Mathf.Max(sheetPeak, sheetFade.FinalOpacity);
                k = sheetPeak > 0f ? sheetFade.FinalOpacity / sheetPeak : 0f;
            } else {
                var t = Mathf.Clamp01((Time.time - hauntedAt) / FadeSeconds);
                drop = 0f;
                k = 1f - t * t * (3f - 2f * t);
            }

            if (k <= 0.004f) {
                ClearGhosts();
                return;
            }

            foreach (var ghost in ghosts) {
                if (ghost.Renderer == null) {
                    continue;
                }

                var t = ghost.Renderer.transform;
                var left = 1f;
                if (ghost.Drift == Drift.Ride) {
                    t.position = ghost.Home - new Vector3(0f, drop, 0f);
                } else if (ghost.Drift == Drift.Drain) {
                    left = Mathf.Clamp01((ghost.Footprint.height - drop) / ghost.Footprint.height);
                    t.localScale = new Vector3(ghost.HomeScale.x, ghost.HomeScale.y * Mathf.Max(left, 0.001f), ghost.HomeScale.z);
                    t.position = ghost.Home;
                    t.position += new Vector3(0f, ghost.Footprint.yMin - Footprint(t.gameObject).yMin, 0f);
                }

                // every channel, so additive glows fade as well as blended ones
                ghost.Renderer.sharedMaterial.color = left > 0f ? ghost.Start * k : Color.clear;
            }
        }

        private void ClearGhosts() {
            foreach (var ghost in ghosts) {
                if (ghost.Renderer != null) {
                    Object.Destroy(ghost.Renderer.gameObject);
                }
            }

            ghosts.Clear();
        }

        // what a switched-off group showed, as bare mesh copies (no animator writes them) that follow the drain out
        private void Haunt(Transform group) {
            ClearGhosts();
            hauntedAt = Time.time;
            sheetFade = null;
            sheetEdge = null;
            string[] drains;
            if (DrainScenes.TryGetValue(Root.name, out drains)) {
                foreach (var path in drains) {
                    var branch = Root.transform.Find(path);
                    var fade = branch != null ? branch.GetComponentInChildren<TransparencyAnimator>(true) : null;
                    var edge = fade != null ? fade.GetComponentInChildren<MeshRenderer>(true) : null;
                    if (edge != null) {
                        sheetFade = fade;
                        sheetEdge = edge.gameObject;
                        sheetTop = Footprint(sheetEdge).yMax;
                        sheetPeak = fade.FinalOpacity;
                        break;
                    }
                }
            }

            foreach (var renderer in group.GetComponentsInChildren<Renderer>(true)) {
                var filter = renderer.GetComponent<MeshFilter>();
                var material = renderer.sharedMaterial;
                var line = renderer.name == "Edge plane";
                if (filter == null || filter.sharedMesh == null || !renderer.enabled || (renderer.name.EndsWith("plane") && !line) || material == null
                    || !material.HasProperty("_Color") || material.color.a < 0.01f || !ShownUnder(renderer.transform, group)) {
                    continue;
                }

                var ghost = new GameObject(renderer.name + " (fading)");
                ghost.transform.SetParent(transform, false);
                ghost.transform.position = renderer.transform.position;
                ghost.transform.rotation = renderer.transform.rotation;
                var outer = transform.lossyScale;
                var scale = renderer.transform.lossyScale;
                ghost.transform.localScale = new Vector3(scale.x / outer.x, scale.y / outer.y, scale.z / outer.z);
                // the surface line's mesh belongs to the water, which may rebuild or drop it
                ghost.AddComponent<MeshFilter>().sharedMesh = line ? Object.Instantiate(filter.sharedMesh) : filter.sharedMesh;
                var copy = ghost.AddComponent<MeshRenderer>();
                copy.sharedMaterial = new Material(material);
                copy.sortingLayerID = renderer.sortingLayerID;
                copy.sortingOrder = renderer.sortingOrder;
                var footprint = Footprint(ghost);
                ghosts.Add(new Ghost {
                    Renderer = copy,
                    Start = material.color,
                    Drift = DriftOf(renderer.name, footprint, sheetFade != null ? sheetTop : float.NaN),
                    Home = ghost.transform.position,
                    HomeScale = ghost.transform.localScale,
                    Footprint = footprint,
                });
            }
        }

        // surface things ride the level down, masks below it drain, the rest stays
        private static Drift DriftOf(string name, Rect footprint, float surface) {
            if (float.IsNaN(surface)) {
                return Drift.Stay;
            }

            if (name.Contains("GradientMask")) {
                return footprint.center.y <= surface && footprint.height > 0f ? Drift.Drain : Drift.Stay;
            }

            return name == "Edge plane" || name.StartsWith("sharedCircleGlow") || name.StartsWith("diseasedWaterTopSlime") || name.StartsWith("diseasedWaterBubble")
                || name.StartsWith(Duckweed) ? Drift.Ride : Drift.Stay;
        }

        private static bool ShownUnder(Transform t, Transform group) {
            for (var p = t; p != null && p != group; p = p.parent) {
                if (!p.gameObject.activeSelf) {
                    return false;
                }
            }

            return true;
        }

        public void FixedUpdate() {
            if (!Placed) {
                if (!Ready) {
                    if (ticks++ % 30 == 0) {
                        AskForDonor();
                        Harvest();
                    }
                } else {
                    PlaceAll();
                }
            }

            // a scene whose water is switched off (Enhanced Clean Water drains it) keeps its own fog
            var wanted = Enabled && (ForcedOn || !Events.WaterPurified);
            var wet = AnyWater();
            if (wanted != Applied || wet != Wet) {
                Wet = wet;
                Apply(wanted);
            }

            SyncHolders();
            // a pool customizes its surface material when it wakes, which can be after the flip
            if (Applied && topsClean.Count < Tops.Count && fixedTicks++ % 30 == 0) {
                foreach (var top in Tops) {
                    PaintTop(top, true);
                }
            }

            // the camera can hold on to settings it took before the scene changed under it
            var here = IsHere();
            if (here && !wasHere) {
                PushFog();
            }

            wasHere = here;
        }

        private bool IsHere() {
            var current = Core.Scenes.Manager.CurrentScene;
            return current != null && Root != null && current.Scene == Root.name;
        }

        private void PushFog() {
            if (Game.UI.Cameras.Current == null) {
                return;
            }

            foreach (var fog in Fogs) {
                if (fog.Controller != null) {
                    Game.UI.Cameras.Current.CameraPostProcessing.ForceFogIntoCurrentCameraSettings(fog.Controller);
                }
            }
        }

        private bool AnyWater() {
            if (Controls.Count == 0) {
                Controls.AddRange(ControlsIn(AllGroups));
            }

            foreach (var control in Controls) {
                if (control != null && control.gameObject.activeInHierarchy) {
                    return true;
                }
            }

            return Controls.Count == 0;
        }

        private void PlaceAll() {
            Placed = true;
            var controls = ControlsIn(Groups).ToArray();
            var count = 0;
            var backdrops = 0;
            var seen = new List<Rect>();
            foreach (var control in controls) {
                if (control.transform.position.z > BackdropZ) {
                    backdrops++;
                    continue;
                }

                // a pool drawn twice at different depths is still one pool
                if (seen.Contains(control.Boundary)) {
                    continue;
                }

                seen.Add(control.Boundary);
                var holder = HolderFor(control);
                var group = GroupOf(control, Root.transform);
                var tinted = group != null && HasTint(group, Natives);
                // generated masks only where asked for; elsewhere a pool keeps what it draws
                var masks = Scenes.Contains(Root.name) || MaskScenes.Contains(Root.name);
                count += Contained(control, controls) ? Masks(control, holder, BasinMasks) : Place(control, holder, tinted || !masks);
            }

            var strength = StrengthIn(Root.name);
            foreach (var holder in Holders) {
                if (strength < 1f) {
                    foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true)) {
                        // its own copy: the template's material is shared by every scene
                        var material = renderer.material;
                        if (material.HasProperty("_Color")) {
                            var c = material.color;
                            material.color = new Color(c.r * strength, c.g * strength, c.b * strength, c.a);
                        }
                    }
                }

            }

            SyncHolders();
            ReleaseDonor();
            Randomizer.log("dirty water: " + Root.name + " dressed with " + count + " pieces" + (backdrops > 0 ? ", " + backdrops + " backdrop pools left bare" : ""));
        }

        // beside the pool's water, but never under the control itself: its scale stretches pieces
        private Transform HolderFor(UberWaterControl control) {
            var holder = new GameObject("dirtyWaterPieces");
            var group = GroupOf(control, Root.transform);
            holder.transform.SetParent(group == control.transform ? transform : group, false);
            Holders.Add(holder);
            HolderControls.Add(control);
            return holder.transform;
        }

        // a holder shows while the look is applied and its pool's water is there
        private void SyncHolders() {
            for (var i = 0; i < Holders.Count; i++) {
                var holder = Holders[i];
                if (holder == null) {
                    continue;
                }

                var control = HolderControls[i];
                var wanted = Applied && (control == null || control.gameObject.activeInHierarchy);
                if (holder.activeSelf != wanted) {
                    holder.SetActive(wanted);
                }
            }
        }

        public void Apply(bool on) {
            Applied = on;
            SyncHolders();
            foreach (var holder in CleanCopies) {
                if (holder != null) {
                    holder.SetActive(on);
                }
            }

            foreach (var clear in Clears) {
                if (clear.Zone != null) {
                    clear.Zone.gameObject.SetActive(on);
                }
            }

            // hidden things go back to the state they were found in, never simply on
            foreach (var thing in hidden) {
                if (thing != null) {
                    thing.SetActive(true);
                }
            }

            hidden.Clear();
            if (on) {
                Hide(Rainbows);
                if (HideWaterfalls) {
                    Hide(Falls);
                }
            } else {
                ClearGhosts();
            }

            Paint(on);
        }

        // Harness: one render with the look on or off, by renderers and colors only; Preview(true) restores it exactly
        public void Preview(bool on) {
            if (!Applied) {
                return;
            }

            if (previewed == null) {
                previewed = new List<Renderer>();
                foreach (var holder in Holders) {
                    if (holder != null) {
                        previewed.AddRange(holder.GetComponentsInChildren<Renderer>(true));
                    }
                }

                foreach (var holder in CleanCopies) {
                    if (holder != null) {
                        previewed.AddRange(holder.GetComponentsInChildren<Renderer>(true));
                    }
                }
            }

            foreach (var renderer in previewed) {
                if (renderer != null) {
                    renderer.enabled = on;
                }
            }

            foreach (var thing in hidden) {
                if (thing != null) {
                    thing.SetActive(!on);
                }
            }

            foreach (var ghost in ghosts) {
                if (ghost.Renderer != null) {
                    ghost.Renderer.enabled = on;
                }
            }

            Paint(on);
            if (Fogs.Count > 0 && IsHere() && Game.UI.Cameras.Current != null) {
                Game.UI.Cameras.Current.CameraPostProcessing.Apply();
            }
        }

        // a top plane's own material (each pool customizes a copy), its clean colors kept per material
        private readonly Dictionary<Material, Color[]> topsClean = new Dictionary<Material, Color[]>();

        private void PaintTop(Renderer top, bool on) {
            var material = top != null ? top.sharedMaterial : null;
            if (material == null || !material.HasProperty("_ReflColor") || !material.name.Contains("(custom)")) {
                return;
            }

            Color[] clean;
            if (!topsClean.TryGetValue(material, out clean)) {
                clean = new[] { material.GetColor("_ReflColor"), material.GetColor("_RefrColor") };
                topsClean[material] = clean;
            }

            material.SetColor("_ReflColor", on ? Swap(clean[0], ReflTint) : clean[0]);
            material.SetColor("_RefrColor", on ? Swap(clean[1], RefrTint) : clean[1]);
        }

        // the tint's hue at the clean color's brightness, alpha kept
        private static Color Swap(Color clean, Color tint) {
            var k = Mathf.Max(clean.r, Mathf.Max(clean.g, clean.b)) / Mathf.Max(0.001f, Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)));
            return new Color(tint.r * k, tint.g * k, tint.b * k, clean.a);
        }

        private void Paint(bool on) {
            var strength = StrengthIn(Root.name);
            var mix = MixIn(Root.name);
            // the renderer, not the object: the client re-activates a deactivated one every frame
            foreach (var occluder in Occluders) {
                if (occluder != null) {
                    occluder.enabled = !on;
                }
            }

            foreach (var top in Tops) {
                PaintTop(top, on);
            }

            foreach (var native in Natives) {
                if (native.Renderer != null && native.Renderer.sharedMaterial != null) {
                    var material = native.Renderer.sharedMaterial;
                    var clean = native.Clean;
                    material.color = on ? TintFor(native, strength, material.color.a, mix) : new Color(clean.r, clean.g, clean.b, material.color.a);
                }
            }

            foreach (var ray in Rays) {
                if (ray != null) {
                    ray.enabled = !(on && HideRays);
                }
            }

            // hue only: a sequence may be animating the alpha
            foreach (var fall in FallMaterials) {
                if (fall.Renderer != null && fall.Renderer.sharedMaterial != null) {
                    var tint = on && TintFalls ? FallTint : fall.Clean;
                    fall.Renderer.sharedMaterial.color = new Color(tint.r, tint.g, tint.b, fall.Renderer.sharedMaterial.color.a);
                }
            }

            // the scene builds its camera settings from the gradient once and keeps them
            foreach (var fog in Fogs) {
                if (fog.Controller != null) {
                    fog.Controller.FogGradient = on && TintFog && Wet ? Purpled(fog.Source, strength) : fog.Clean;
                }
            }

            if (Fogs.Count > 0) {
                if (Root != null && Root.SceneSettings != null) {
                    Root.SceneSettings.ResetSettings();
                }

                if (IsHere()) {
                    PushFog();
                }
            }
        }

        private void Hide(List<GameObject> things) {
            foreach (var thing in things) {
                if (thing != null && thing.activeSelf) {
                    thing.SetActive(false);
                    hidden.Add(thing);
                }
            }
        }

        public void OnDestroy() {
            if (Applied) {
                Apply(false);
            }

            foreach (var holder in Holders) {
                if (holder != null) {
                    Object.Destroy(holder);
                }
            }

            foreach (var holder in CleanCopies) {
                if (holder != null) {
                    Object.Destroy(holder);
                }
            }
        }
    }
}

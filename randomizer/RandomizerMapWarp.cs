using System.Collections.Generic;
using System.Reflection;
using Core;
using Game;
using UnityEngine;

// Ori 2 style warping: hover a spirit well on the area map, hold the bind, a ring fills, you
// warp. The list walked is TeleporterController's own, so custom warps come along for free, and
// the warp itself is the one the teleporter screen already uses.
public static class RandomizerMapWarp {
    // How far from the cursor a well counts, in world units. Tighter than the map's own icons
    // reach, because a well takes the hover from every pickup sitting under it.
    private const float Reach = 6f;

    private const float PadReach = 25f;

    // Ring radius in map units: the icons are children of MapPivot, so sizing from its scale is
    // what keeps the ring proportional to them at every zoom.
    private const float RingSpan = 7.04f;

    // a warp icon, and its drop onto the ring's centre, as shares of the ring and of itself
    private const float PinShare = 0.75f;

    private const float PinDrop = 0.15f;

    // a locked well has no ring to clear, only its own icon
    private const float IconClearance = 0.5f;

    // Measured at 270 labels: no frame cost once they exist, so this is only a guard against a
    // seed with far more locations than any real one has.
    private const int MostLabels = 400;

    // below this the icon is gone rather than merely faint
    private const float Vanished = 0.02f;

    // How bright a well you have not lit is drawn. Absolute, not a share: these icons sit at
    // 0.5 already, and the map fade drives the same channel, so a ceiling is what sticks.
    private const float LockedAlpha = 0.125f;

    // How far into the borrowed animation the ring reads as full. The rest of its five seconds is
    // the flourish the soul link plays once ready, which a hold has no use for.
    private const float SoulFull = 1f;

    // roughly twelve frames either way
    private const float FadeRate = 5f;

    // TransparencyAnimator drives opacity through whichever of these its Mode selects.
    private static readonly string[] Alphas = {
        "_Color", "_TintColor", "_MaskDissolveColor", "_AdditiveLayerColor"
    };

    // True when a well has the cursor, so the map knows not to draw a pickup tooltip over it.
    public static bool Update(AreaMapUI map, AreaMapNavigation navigation, Vector2 cursor,
            Vector3 textScale, float offset) {
        try {
            return Inner(map, navigation, cursor, textScale, offset);
        } catch (System.Exception e) {
            if (!Complained) {
                Complained = true;
                Randomizer.log("map warp: threw, no warp prompt this session -- " + e);
            }

            return false;
        }
    }

    private static bool Inner(AreaMapUI map, AreaMapNavigation navigation, Vector2 cursor,
            Vector3 textScale, float offset) {
        var found = Nearest(Pointing() ? cursor : navigation.ScrollPosition);
        List(map, navigation, textScale, offset, found);
        if (found == null) {
            Clear();
            return false;
        }

        Say(map, navigation, found, textScale, offset);
        if (!Warpable(found)) {
            Held = null;
            Since = -1f;
            Charging(false);
            Hide();
            return true;
        }

        // a different well under the cursor is a new hold, not a continuation of the old one
        if (found != Held) {
            Held = found;
            Since = -1f;
        }

        var down = RandomizerRebinding.MapWarp.Held();
        if (!down) {
            Since = -1f;
        } else if (Since < 0f) {
            Since = Time.time;
        }

        var progress = Since < 0f ? 0f
            : Mathf.Clamp01((Time.time - Since) / RandomizerSettings.Customization.MapWarpHold.Value);
        Charging(progress > 0f && progress < 1f);
        Draw(map, navigation, found, progress);
        if (progress < 1f) {
            return true;
        }

        Clear();
        UI.Menu.HideMenuScreen();
        var sound = GameMapUI.Instance.Teleporters.SelectTeleporterSound;
        if (sound != null) {
            Sound.Play(sound.GetSound(null), Vector3.zero, null);
        }

        TeleporterController.BeginTeleportation(found);
        return true;
    }

    // Rides the legend bind: the gesture that explains the map's symbols, extended to the things
    // on it you can act on. Named only -- the second line is an instruction, and an instruction
    // repeated a dozen times over is noise.
    public static void Labels() {
        Listing = !Listing;
    }

    // Hides what the legend put up. The toggle itself is left alone, so reopening the map restores
    // the labels the same way it restores the legend.
    public static void Closed() {
        Clear();
        Blank(0);
    }

    private static void List(AreaMapUI map, AreaMapNavigation navigation, Vector3 textScale,
            float offset, GameMapTeleporter hovered) {
        if (!Listing) {
            Blank(0);
            return;
        }

        var shown = 0;
        var controller = TeleporterController.Instance;
        if (controller != null && controller.Teleporters != null) {
            foreach (var well in controller.Teleporters) {
                // the hovered well already wears Say's label
                if (well == null || well == hovered) {
                    continue;
                }

                // the one thing a full listing can say that hover already says: this one is shut
                shown = Put(map, navigation, textScale, offset, shown, well.WorldPosition,
                    well.Activated ? Title(well) : Title(well) + "\n(not activated)");
            }
        }

        if (GameWorld.Instance != null) {
            foreach (var area in GameWorld.Instance.RuntimeAreas) {
                if (area == null || area.Icons == null) {
                    continue;
                }

                foreach (var icon in area.Icons) {
                    if (icon == null || shown >= MostLabels ||
                            icon.Icon == WorldMapIconType.Invisible || !icon.IsVisible(map)) {
                        continue;
                    }

                    if (RandomizerLocationManager.LocationsByWorldMapGuid.TryGetValue(
                            icon.Guid, out var pickup)) {
                        shown = Put(map, navigation, textScale, offset, shown, icon.Position,
                            pickup.FriendlyName);
                    }
                }
            }
        }

        Blank(shown);
    }

    private static int Put(AreaMapUI map, AreaMapNavigation navigation, Vector3 textScale,
            float offset, int index, Vector2 world, string text) {
        var box = Label(index, map);
        if (box == null) {
            return index;
        }

        Vector3 at = navigation.WorldToMapPosition(world);
        at.y -= offset * 0.6f + Scaled(navigation) * IconClearance;
        box.transform.position = at;
        box.transform.localScale = textScale;
        box.OverrideText = text;
        box.gameObject.SetActive(true);
        return index + 1;
    }

    private static void Blank(int keep) {
        for (var i = keep; i < Labelled.Count; i++) {
            if (Labelled[i] != null && Labelled[i].gameObject.activeSelf) {
                Labelled[i].gameObject.SetActive(false);
            }
        }
    }

    // Cloned from the same legend entry the map's other randomizer labels come from.
    private static MessageBox Label(int index, AreaMapUI map) {
        while (Labelled.Count <= index) {
            var legend = map.transform.FindChild("legend");
            var source = legend == null ? null : legend.FindChild("player");
            if (source == null) {
                return null;
            }

            var made = (GameObject)Object.Instantiate(source.gameObject);
            made.transform.parent = legend;
            var box = made.GetComponent<MessageBox>();
            box.MessageProvider = null;
            box.OverrideText = "";
            box.gameObject.SetActive(false);
            Labelled.Add(box);
        }

        return Labelled[index];
    }

    // A pad has no cursor to point with, so what it is pointing at is whatever the map is
    // centred on. The scheme is the game's own answer to this, and the one Bash aiming uses.
    public static bool Pointing() {
        return GameSettings.Instance == null ||
            GameSettings.Instance.CurrentControlScheme == ControlScheme.KeyboardAndMouse;
    }

    // Activated wells only, and never the one Ori is standing on -- BeginTeleportation refuses a
    // target within ten units and would swallow the hold without saying why.
    private static bool Warpable(GameMapTeleporter well) {
        return well.Activated && Characters.Sein != null &&
            Vector3.Distance(well.WorldPosition, Characters.Sein.Position) >= 10f;
    }

    private static GameMapTeleporter Nearest(Vector2 cursor) {
        var controller = TeleporterController.Instance;
        if (controller == null || controller.Teleporters == null) {
            return null;
        }

        // the scroll centre is a blunter pointer than a mouse, so it reaches further
        var nearest = Pointing() ? Reach : PadReach;
        GameMapTeleporter found = null;
        foreach (var well in controller.Teleporters) {
            if (well == null) {
                continue;
            }

            var distance = Vector2.Distance(well.WorldPosition, cursor);
            if (distance > nearest) {
                continue;
            }

            nearest = distance;
            found = well;
        }

        return found;
    }

    private static void Say(AreaMapUI map, AreaMapNavigation navigation, GameMapTeleporter well,
            Vector3 textScale, float offset) {
        var box = map.WarpPrompt;
        if (box == null) {
            return;
        }

        Vector3 at = navigation.WorldToMapPosition(well.WorldPosition);
        at.y -= offset * 0.6f + Scaled(navigation) * (Warpable(well) ? 1f : IconClearance);
        box.transform.position = at;
        box.transform.localScale = textScale;
        // FirstBindName renders as the button's own glyph, and follows a rebind for free
        box.OverrideText = Title(well) + "\n" + (!well.Activated
            ? "(not activated)"
            : Warpable(well)
                ? "(Hold " + RandomizerRebinding.MapWarp.FirstBindName() + " to warp!)"
                : "(you are here)");
        box.gameObject.SetActive(true);
    }

    // Teleporter identifiers that are not zone keys, or whose zone name is not what the well is
    // called. Everything else reads out of the stats page's own table.
    private static readonly Dictionary<string, string> WellNames = new Dictionary<string, string> {
        { "swamp", "Swamp" },
        { "forlorn", "Forlorn" },
        { "mangroveFalls", "Blackroot" },
        { "mangroveB", "Lost Grove" },
        { "horuFields", "Horu Fields" },
        { "spiritTree", "Grove" },
        { "grove", "Grove" },
        { "grotto", "Grotto" },
        { "ginso", "Ginso" },
        { "horu", "Horu" },
        { "valley", "Valley" },
        { "sorrow", "Sorrow" },
        { "glades", "Glades" },
        { "blackroot", "Blackroot" }
    };

    // The short name the map's tooltip uses, for callers holding only an identifier.
    public static string ShortName(string id) {
        string name;
        if (WellNames.TryGetValue(id ?? "", out name)) {
            return name;
        }

        var zones = RandomizerStatsManager.ZonePrettyNames;
        return zones != null && zones.TryGetValue(id ?? "", out name) ? name.Trim() : id;
    }

    private static string Name(GameMapTeleporter well) {
        var id = well.Identifier ?? "";
        string name;
        if (WellNames.TryGetValue(id, out name)) {
            return name;
        }

        var zones = RandomizerStatsManager.ZonePrettyNames;
        // the stats page pads some of its names out to a column width
        if (zones != null && zones.TryGetValue(id, out name)) {
            return name.Trim();
        }

        if (Unnamed.Add(id)) {
            Randomizer.log("map warp: no short name for teleporter " + id);
        }

        var area = well.Area == null || well.Area.Area == null ? null : well.Area.Area.AreaNameString;
        return string.IsNullOrEmpty(area) ? id : area;
    }

    // A custom warp is one the randomizer added: the game's own teleporters carry the game's own
    // message provider, and only ours carry the randomizer's.
    private static bool Custom(GameMapTeleporter well) {
        return well.Name != null && well.Name.GetType() == typeof(RandomizerMessageProvider);
    }

    private static string Title(GameMapTeleporter well) {
        if (!Custom(well)) {
            return Name(well) + " Teleporter";
        }

        // the seed names these "Warp to X", which reads badly with a second line under it
        var name = well.Identifier ?? "";
        return (name.StartsWith("Warp to ") ? name.Substring(8) : name) + " Warp";
    }

    // Custom warps are not in the game's map data, so nothing on the area map draws them. Show is
    // the game's own recipe -- the spirit well prefab, the warp tint, both maps -- and Update
    // keeps it where the map has scrolled to, so neither is worth reimplementing.
    public static void Icons(AreaMapUI map) {
        try {
            var controller = TeleporterController.Instance;
            if (map == null || map.Navigation == null || AreaMapUI.Instance == null ||
                    controller == null || controller.Teleporters == null) {
                return;
            }

            var any = false;
            foreach (var well in controller.Teleporters) {
                if (well != null && Custom(well)) {
                    well.Show();
                    well.Update();
                    any = true;
                }
            }

            if (any) {
                Fit(map);
            }

            Shade();
        } catch (System.Exception e) {
            if (!Pinned) {
                Pinned = true;
                Randomizer.log("map warp: could not draw custom warps -- " + e);
            }
        }
    }

    // A well you have not lit looks exactly like one you have. The icon object belongs to
    // RuntimeWorldMapIcon and is private, so it is read the way the rest of this file reads the
    // game: by asking rather than by rebuilding.
    private static readonly FieldInfo IconObject = typeof(RuntimeWorldMapIcon).GetField(
        "m_iconGameObject", BindingFlags.NonPublic | BindingFlags.Instance);

    private static void Shade() {
        if (IconObject == null || GameWorld.Instance == null) {
            return;
        }

        foreach (var area in GameWorld.Instance.RuntimeAreas) {
            if (area == null || area.Icons == null) {
                continue;
            }

            foreach (var icon in area.Icons) {
                if (icon == null) {
                    continue;
                }

                var alpha = 1f;
                if (icon.Icon == WorldMapIconType.SavePedestal) {
                    alpha = Open(icon.Position) ? 1f : LockedAlpha;
                } else if (RandomizerLocationManager.LocationsByWorldMapGuid.TryGetValue(
                        icon.Guid, out var loc) && Spent(loc)) {
                    alpha = TouchedAlpha();
                }

                if (alpha >= 0.99f) {
                    continue;
                }

                var lit = IconObject.GetValue(icon) as GameObject;
                if (lit != null) {
                    Paint(lit, alpha);
                }
            }
        }
    }

    // Touched but not collected: the slot is already granted, so going back for it gives nothing.
    public static bool Spent(RandomizerLocationManager.Location loc) {
        return loc != null && loc.Touched && !loc.Collected;
    }

    // The slider's word, except that the Uncollected filter never loses a touched icon
    // entirely: a slider left at zero is more often a mistake than a wish.
    private static float TouchedAlpha() {
        var alpha = RandomizerSettings.Customization.TouchedVisibility.Value;
        return RandomizerSettings.CurrentFilter == RandomizerSettings.MapFilterMode.Uncollected
            ? Mathf.Max(alpha, FailSafeAlpha) : alpha;
    }

    private const float FailSafeAlpha = 0.1f;

    // At the bottom of the slider it is not drawn at all, since an invisible icon is still a
    // thing the cursor can catch on.
    public static bool Hidden(RandomizerLocationManager.Location loc) {
        return Spent(loc) && TouchedAlpha() <= Vanished;
    }

    // Wells sit on their own scenery, so a loose match is enough to tell which one an icon is.
    private static bool Open(Vector2 at) {
        var controller = TeleporterController.Instance;
        if (controller == null || controller.Teleporters == null) {
            return true;
        }

        foreach (var well in controller.Teleporters) {
            if (well != null && Vector2.Distance(well.WorldPosition, at) < Reach) {
                return well.Activated;
            }
        }

        return true;
    }

    // Written every frame rather than on a change: the map's own fade drives these same colours
    // while it opens, and a value set once would be painted over.
    private static void Paint(GameObject icon, float alpha) {
        List<Material> paints;
        List<string> keys;
        if (!Painted.TryGetValue(icon, out paints)) {
            paints = new List<Material>();
            keys = new List<string>();
            foreach (var renderer in icon.GetComponentsInChildren<Renderer>(true)) {
                var material = renderer == null ? null : renderer.material;
                if (material == null) {
                    continue;
                }

                foreach (var name in Alphas) {
                    if (material.HasProperty(name)) {
                        paints.Add(material);
                        keys.Add(name);
                    }
                }
            }

            Painted[icon] = paints;
            Keyed[icon] = keys;
        }

        keys = Keyed[icon];
        for (var i = 0; i < paints.Count; i++) {
            if (paints[i] == null) {
                continue;
            }

            var colour = paints[i].GetColor(keys[i]);
            if (colour.a > alpha + 0.01f) {
                paints[i].SetColor(keys[i], new Color(colour.r, colour.g, colour.b, alpha));
            }
        }
    }

    // Show parents its icon to the fade group rather than to the map, so it keeps one size while
    // the map scales underneath -- which reads as enormous zoomed out.
    private static void Fit(AreaMapUI map) {
        var pivot = map.Navigation == null || map.Navigation.MapPivot == null
            ? 0f : map.Navigation.MapPivot.lossyScale.x;
        if (pivot <= 0f || map.TeleportPrefab == null) {
            return;
        }

        var want = 2f * RingSpan * PinShare * pivot;
        foreach (Transform child in map.FadeOutGroup) {
            if (child == null || !child.name.StartsWith(map.TeleportPrefab.name)) {
                continue;
            }

            if (PinNatural <= 0f) {
                child.localScale = map.TeleportPrefab.transform.localScale;
                var drawn = child.GetComponentInChildren<Renderer>(true);
                PinNatural = drawn == null ? 0f : drawn.bounds.size.x;
                PinTall = drawn == null ? 0f : drawn.bounds.size.y;
                if (PinNatural <= 0f) {
                    return;
                }
            }

            var fit = want / PinNatural;
            child.localScale = map.TeleportPrefab.transform.localScale * fit;
            // Show hangs the icon above the well so its base sits on the spot; the ring is drawn
            // on the spot itself, so one of them has to move for the two to agree.
            child.position -= Vector3.up * (PinTall * fit * PinDrop);
        }
    }

    // Ring radius in world units, from the scale the map's own contents are drawn at.
    private static float Scaled(AreaMapNavigation navigation) {
        var pivot = navigation == null || navigation.MapPivot == null
            ? 0f : navigation.MapPivot.lossyScale.x;
        return Mathf.Clamp(RingSpan * pivot, 0.05f, 1.5f);
    }

    private static void Draw(AreaMapUI map, AreaMapNavigation navigation, GameMapTeleporter well,
            float progress) {
        var soul = Soul(map);
        if (soul == null) {
            return;
        }

        SoulFade = Mathf.MoveTowards(SoulFade, progress > 0f ? 1f : 0f,
            Time.unscaledDeltaTime * FadeRate);
        if (SoulFade <= 0.001f) {
            soul.SetActive(false);
            return;
        }

        soul.SetActive(true);
        Tint(SoulFade);
        soul.transform.position = navigation.WorldToMapPosition(well.WorldPosition);
        Measure(soul);
        soul.transform.localScale = SoulBase * (2f * Scaled(navigation) / SoulSpan);
        if (SoulTimeline != null && SoulTimeline.AnimatorDriver != null) {
            var driver = SoulTimeline.AnimatorDriver;
            driver.CurrentTime = progress * SoulFull;
            driver.Sample();
        }
    }

    // Taken while the menu's prefabs are certainly still loaded. The clone holds a reference to
    // the material and its texture, which is what keeps UnloadUnusedAssets from taking them.
    public static void Preload(AreaMapUI map) {
        try {
            var soul = Soul(map);
            if (soul != null) {
                soul.SetActive(false);
            }
        } catch (System.Exception e) {
            SoulMissing = true;
            Randomizer.log("map warp: could not take the ring art -- " + e);
        }
    }

    // The soul link's own charge ring. Its fill is a TimelineSequence that a
    // FloatProviderAnimatorDriver walks from the real cooldown -- drop that driver and the
    // timeline is ours to scrub, which is the authored animation at any point we like.
    private static GameObject Soul(AreaMapUI map) {
        if (SoulObject != null || SoulMissing) {
            return SoulObject;
        }

        var source = UI.SeinUI == null ? null : UI.SeinUI.SoulFlameUI;
        if (source == null) {
            SoulMissing = true;
            Randomizer.log("map warp: no soul link ring to borrow; holds will have no ring");
            return null;
        }

        SoulObject = (GameObject)Object.Instantiate(source);
        SoulObject.name = "randomizerWarpSoulRing";
        SoulObject.transform.parent = map.FadeOutGroup;
        foreach (var driver in SoulObject.GetComponentsInChildren<FloatProviderAnimatorDriver>(true)) {
            Object.DestroyImmediate(driver);
        }

        RandomizerGhost.Quiet(SoulObject);
        // Enabled and made opaque, but not repainted: the widget is many colours and flattening it
        // to one tint throws away the thing worth having. Its fader is gone with Quiet, and what
        // the fader left behind is invisible, so only the alpha is overruled.
        Opaque(SoulObject);
        var sorted = Sorting(map);
        var layer = MapLayer(map);
        foreach (var renderer in SoulObject.GetComponentsInChildren<Renderer>(true)) {
            renderer.enabled = true;
            renderer.gameObject.layer = layer;
            if (sorted != null) {
                renderer.sortingLayerID = sorted.sortingLayerID;
                renderer.sortingOrder = sorted.sortingOrder + 1;
            }
        }

        foreach (var animator in SoulObject.GetComponentsInChildren<BaseAnimator>(true)) {
            if (animator.GetType().Name == "TimelineSequence" && animator.AnimatorDriver != null) {
                SoulTimeline = animator;
                animator.AnimatorDriver.Stop();
                break;
            }
        }

        SoulBase = SoulObject.transform.localScale;
        return SoulObject;
    }

    // The widget is hidden while playing, and an inactive renderer has no bounds to read, so its
    // size cannot be known until the frame it is first shown. The ring halves are the ring;
    // everything else is glow and background reaching well past it.
    private static void Measure(GameObject soul) {
        if (SoulSpan > 0.0001f) {
            return;
        }

        soul.transform.localScale = SoulBase;
        var widest = 0f;
        var ring = 0f;
        foreach (var renderer in soul.GetComponentsInChildren<Renderer>(true)) {
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
        SoulSpan = ring > 0.0001f ? ring : SoulBase.x;
    }

    private static void Opaque(GameObject target) {
        SoulPaints.Clear();
        SoulKeys.Clear();
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true)) {
            var material = renderer == null ? null : renderer.material;
            if (material == null) {
                continue;
            }

            foreach (var name in Alphas) {
                if (material.HasProperty(name)) {
                    // kept so the fade multiplies the widget's colours rather than flattening them
                    SoulPaints.Add(material);
                    SoulKeys.Add(name);
                }
            }
        }
    }

    private static void Tint(float alpha) {
        for (var i = 0; i < SoulPaints.Count; i++) {
            var colour = SoulPaints[i].GetColor(SoulKeys[i]);
            SoulPaints[i].SetColor(SoulKeys[i], new Color(colour.r, colour.g, colour.b, alpha));
        }
    }

    // The layer things on this map are drawn on, which is not the one the group holding them
    // sits on.
    private static int MapLayer(AreaMapUI map) {
        foreach (var renderer in map.FadeOutGroup.GetComponentsInChildren<Renderer>(true)) {
            if (renderer != null) {
                return renderer.gameObject.layer;
            }
        }

        return map.FadeOutGroup.gameObject.layer;
    }

    // The player's own marker is the thing on this map guaranteed to draw over the terrain, so
    // its sorting is the sorting to have.
    private static Renderer Sorting(AreaMapUI map) {
        var marker = map.PlayerPositionMarker == null
            ? null : map.PlayerPositionMarker.GetComponentInChildren<Renderer>(true);
        if (marker != null) {
            return marker;
        }

        foreach (var renderer in map.FadeOutGroup.GetComponentsInChildren<Renderer>(true)) {
            if (renderer != null) {
                return renderer;
            }
        }

        return null;
    }

    // The soul link's own charge loop. It belongs to the live ability rather than to the cloned
    // widget, so it is started and stopped, never copied.
    private static void Charging(bool on) {
        var sein = Characters.Sein;
        var flame = sein == null ? null : sein.SoulFlame;
        var sound = flame == null ? null : flame.ChargingSound;
        if (on != Sounding) {
            Sounding = on;
            if (sound != null) {
                if (on) {
                    sound.Play();
                } else {
                    sound.StopAndFadeOut(0.1f);
                }
            }

            return;
        }

        // the clip is cut for the soul link's own charge and runs out under a longer hold
        if (on && sound != null && !sound.IsPlaying) {
            sound.Play();
        }
    }

    private static void Hide() {
        SoulFade = 0f;
        if (SoulObject != null) {
            SoulObject.SetActive(false);
        }
    }

    // Every way this gesture can end comes through here, the map closing included -- a charge
    // loop with nothing left to stop it would play forever.
    public static void Clear() {
        Charging(false);
        Held = null;
        Since = -1f;
        Hide();
        if (AreaMapUI.Instance != null && AreaMapUI.Instance.WarpPrompt != null) {
            AreaMapUI.Instance.WarpPrompt.gameObject.SetActive(false);
        }
    }

    private static GameMapTeleporter Held;

    private static float Since = -1f;

    private static GameObject SoulObject;

    private static BaseAnimator SoulTimeline;

    private static float SoulSpan;

    private static Vector3 SoulBase = Vector3.one;

    private static bool SoulMissing;

    private static float SoulFade;

    private static readonly List<Material> SoulPaints = new List<Material>();

    private static readonly List<string> SoulKeys = new List<string>();

    private static float PinNatural;

    private static float PinTall;

    private static bool Pinned;

    private static readonly Dictionary<GameObject, List<Material>> Painted =
        new Dictionary<GameObject, List<Material>>();

    private static readonly Dictionary<GameObject, List<string>> Keyed =
        new Dictionary<GameObject, List<string>>();

    private static bool Listing;

    private static readonly List<MessageBox> Labelled = new List<MessageBox>();

    private static bool Sounding;

    private static bool Complained;

    private static readonly HashSet<string> Unnamed = new HashSet<string>();
}

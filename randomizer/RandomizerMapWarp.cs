using System.Collections.Generic;
using System.Reflection;
using Core;
using Game;
using UnityEngine;

// Hover a spirit well on the area map and hold the bind to warp there, via the teleporter screen's
// own BeginTeleportation; TeleporterController's list includes custom warps.
public static class RandomizerMapWarp {
    // world units from the cursor; tight, since a well takes the hover from every pickup under it
    private const float Reach = 6f;

    private const float PadReach = 25f;

    // ring radius in map units, scaled by MapPivot like the icons so it tracks every zoom
    private const float RingSpan = 7.04f;

    // a warp icon, and its drop onto the ring's center, as shares of the ring and of itself
    private const float PinShare = 0.75f;

    private const float PinDrop = 0.15f;

    // a locked well has no ring to clear, only its own icon
    private const float IconClearance = 0.5f;

    // a guard against seeds with far more locations than any real one
    private const int MostLabels = 400;

    // below this the icon is gone rather than merely faint
    private const float Vanished = 0.02f;

    // alpha ceiling for an unlit well; absolute, since the map fade drives the same channel
    private const float LockedAlpha = 0.125f;

    // animation time at which the borrowed ring reads full; the rest of its 5 s is the ready flourish
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
                Randomizer.log("map warp: threw (logged once, retried every frame) -- " + e);
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

    // Toggled with the legend; labels carry names only, never the hold instruction.
    public static void Labels() {
        Listing = !Listing;
    }

    // Hides the labels but keeps the toggle, so reopening the map restores them like the legend.
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

                // of hover's second lines, only "(not activated)" belongs in a listing
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
        for (var i = keep; i < Labeled.Count; i++) {
            if (Labeled[i] != null && Labeled[i].gameObject.activeSelf) {
                Labeled[i].gameObject.SetActive(false);
            }
        }
    }

    // Cloned from the same legend entry the map's other randomizer labels come from.
    private static MessageBox Label(int index, AreaMapUI map) {
        // the clones die with the legend they were parented to
        if (index < Labeled.Count && Labeled[index] == null) {
            Labeled.RemoveRange(index, Labeled.Count - index);
        }

        while (Labeled.Count <= index) {
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
            Labeled.Add(box);
        }

        return Labeled[index];
    }

    // A mouse points with the cursor, a pad at the map's centre; the same scheme test Bash aiming uses.
    public static bool Pointing() {
        return GameSettings.Instance == null ||
            GameSettings.Instance.CurrentControlScheme == ControlScheme.KeyboardAndMouse;
    }

    // BeginTeleportation silently refuses a target within 10 units, so those never charge.
    private static bool Warpable(GameMapTeleporter well) {
        return well.Activated && Characters.Sein != null &&
            Vector3.Distance(well.WorldPosition, Characters.Sein.Position) >= 10f;
    }

    private static GameMapTeleporter Nearest(Vector2 cursor) {
        var controller = TeleporterController.Instance;
        if (controller == null || controller.Teleporters == null) {
            return null;
        }

        // the scroll center is a blunter pointer than a mouse, so it reaches further
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

    // Ids that are not zone keys, or whose zone name is not the well's; the rest use ZonePrettyNames.
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
        // the game's own well ids are crossed: Sorrow's well is valleyOfTheWind, as in TeleportTable
        { "valleyOfTheWind", "Sorrow" },
        { "sorrowPass", "Valley" },
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

    // Only warps the randomizer added carry a RandomizerMessageProvider (RemoveCustomTeleporters' test).
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

    // Nothing in the map data draws custom warps; GameMapTeleporter.Show and Update do, on both maps.
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

    // An unlit well looks lit unless faded here; the icon object is private to RuntimeWorldMapIcon.
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

    // Already granted: an icon still on the map for it (the world forgot on death, or it repeats) gives nothing.
    public static bool Spent(RandomizerLocationManager.Location loc) {
        return loc != null && loc.Touched;
    }

    // The slider's alpha, floored under the Uncollected filter so a touched icon never vanishes there.
    private static float TouchedAlpha() {
        var alpha = RandomizerSettings.Customization.TouchedVisibility.Value;
        return RandomizerSettings.CurrentFilter == RandomizerSettings.MapFilterMode.Uncollected
            ? Mathf.Max(alpha, FailSafeAlpha) : alpha;
    }

    private const float FailSafeAlpha = 0.1f;

    // Not drawn at all near zero: an invisible icon would still catch the cursor.
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

    // Every frame, not on change: the map's opening fade drives these colours and would paint over it.
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

            var color = paints[i].GetColor(keys[i]);
            if (color.a > alpha + 0.01f) {
                paints[i].SetColor(keys[i], new Color(color.r, color.g, color.b, alpha));
            }
        }
    }

    // Show parents icons to the fade group, not the map, so they are rescaled with MapPivot here.
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
            // Show hangs the icon above the well; the ring is centred on the well itself
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
            soul.Show(false);
            return;
        }

        soul.Show(true);
        soul.Fade(SoulFade);
        soul.Place(navigation.WorldToMapPosition(well.WorldPosition));
        soul.Widen(2f * Scaled(navigation));
        soul.Progress(progress);
    }

    // Cloned at Awake; the clone's references are what keep UnloadUnusedAssets off its art.
    public static void Preload(AreaMapUI map) {
        try {
            var soul = Soul(map);
            if (soul != null) {
                soul.Show(false);
            }
        } catch (System.Exception e) {
            SoulMissing = true;
            Randomizer.log("map warp: could not take the ring art -- " + e);
        }
    }

    // The soul link's own charge ring, borrowed and scrubbed.
    private static RandomizerHoldRing Soul(AreaMapUI map) {
        // the holder is a plain object and never goes null with the scene its clone was in
        if (SoulRing != null && SoulRing.Object != null) {
            return SoulRing;
        }

        if (SoulMissing) {
            return null;
        }

        var source = UI.SeinUI == null ? null : UI.SeinUI.SoulFlameUI;
        if (source == null) {
            SoulMissing = true;
            Randomizer.log("map warp: no soul link ring to borrow; holds will have no ring");
            return null;
        }

        var ring = new RandomizerHoldRing();
        if (!ring.Adopt(source, map.FadeOutGroup, "randomizerWarpSoulRing")) {
            SoulMissing = true;
            return null;
        }

        ring.Match(Sorting(map), MapLayer(map));
        ring.Full = SoulFull;
        SoulRing = ring;
        return SoulRing;
    }

    // The map's renderers' layer; the group object holding them sits on another.
    private static int MapLayer(AreaMapUI map) {
        foreach (var renderer in map.FadeOutGroup.GetComponentsInChildren<Renderer>(true)) {
            if (renderer != null) {
                return renderer.gameObject.layer;
            }
        }

        return map.FadeOutGroup.gameObject.layer;
    }

    // The player marker always draws over the terrain, so its sorting is the one to copy.
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

    // The live soul link's charge loop, started and stopped, never cloned.
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
        if (SoulRing != null) {
            SoulRing.Show(false);
        }
    }

    // Every exit, map close included, must come through here or the charge loop keeps playing.
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

    private static RandomizerHoldRing SoulRing;

    private static bool SoulMissing;

    private static float SoulFade;

    private static float PinNatural;

    private static float PinTall;

    private static bool Pinned;

    private static readonly Dictionary<GameObject, List<Material>> Painted =
        new Dictionary<GameObject, List<Material>>();

    private static readonly Dictionary<GameObject, List<string>> Keyed =
        new Dictionary<GameObject, List<string>>();

    private static bool Listing;

    private static readonly List<MessageBox> Labeled = new List<MessageBox>();

    private static bool Sounding;

    private static bool Complained;

    private static readonly HashSet<string> Unnamed = new HashSet<string>();
}

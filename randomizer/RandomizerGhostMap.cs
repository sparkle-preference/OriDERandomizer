using System.Collections.Generic;
using Game;
using UnityEngine;

// The other players, on the area map. Not distance-culled the way the ghosts are: the map is
// laid out from the start, so there is nothing to spoil and nothing to walk towards.
public static class RandomizerGhostMap {
    // The player's own marker is generous; a screen of them at that size would be a mess.
    private const float Relative = 0.5f;

    // The reach the map's own icons hover from, so a player is no fussier to point at.
    private const float HoverRadius = 12f;

    public static void Update(AreaMapUI map) {
        if (map == null || map.Navigation == null || map.PlayerPositionMarkerPrefab == null) {
            Retire(Icons, 0);
            Retire(LinkIcons, 0);
            return;
        }

        RandomizerGhost.Markers(Wanted);
        for (var i = 0; i < Wanted.Count; i++) {
            var marker = Wanted[i];
            var icon = At(i, map, map.PlayerPositionMarkerPrefab, Icons, Painted, "randomizerPeerMarker", Relative);
            if (icon == null) {
                continue;
            }

            if (Painted.Count > i && Painted[i] != marker.Shade) {
                Painted[i] = marker.Shade;
                RandomizerGhost.Paint(icon, marker.Shade);
            }

            // the same transform the player's own marker goes through, so the two agree
            icon.transform.localPosition = map.Navigation.WorldToMapPosition(
                marker.Position + map.PlayerPositionOffset + Vector3.up);
        }

        Retire(Icons, Wanted.Count);

        // their soul links, only where saving at them is on: otherwise they are clutter
        var links = 0;
        if (RandomizerBonus.AllyLinkSaves && map.SoulFlamePositionMarkerPrefab != null) {
            for (var i = 0; i < Wanted.Count; i++) {
                var marker = Wanted[i];
                if (float.IsNaN(marker.SoulLink.x)) {
                    continue;
                }

                // one per player, so it keeps the size your own gets
                var icon = At(links, map, map.SoulFlamePositionMarkerPrefab, LinkIcons, LinkPainted, "randomizerPeerLink", 1f);
                if (icon == null) {
                    continue;
                }

                if (LinkPainted.Count > links && LinkPainted[links] != marker.Shade) {
                    LinkPainted[links] = marker.Shade;
                    RandomizerGhost.Paint(icon, marker.Shade);
                }

                icon.transform.localPosition = map.Navigation.WorldToMapPosition(
                    new Vector3(marker.SoulLink.x, marker.SoulLink.y, 0f) + map.PlayerPositionOffset + Vector3.up);
                links++;
            }
        }

        Retire(LinkIcons, links);
    }

    // The peer nearest the cursor, if one beats everything else in the running. Shares the map's
    // one tooltip rather than stacking a second label on the same spot.
    public static bool Hover(Vector2 cursor, ref float nearest, out Vector3 position, out string name) {
        position = Vector3.zero;
        name = null;
        var found = false;
        RandomizerGhost.Markers(Wanted);
        for (var i = 0; i < Wanted.Count; i++) {
            var distance = Vector2.Distance(Wanted[i].Position, cursor);
            if (distance > HoverRadius || distance > nearest) {
                continue;
            }

            nearest = distance;
            position = Wanted[i].Position;
            name = RandomizerMW.PlayerName(Wanted[i].PlayerId);
            found = true;
        }

        return found;
    }

    private static GameObject At(int index, AreaMapUI map, GameObject prefab, List<GameObject> icons, List<Color> painted, string name, float scale) {
        while (icons.Count <= index) {
            // an icon made before the map is all the way up is lost with the fade-in
            if (map.FadeOutAnimator == null || map.FadeOutAnimator.FinalOpacity < 1f) {
                return null;
            }

            var made = Object.Instantiate(prefab);
            made.name = name;
            made.transform.parent = map.FadeOutGroup;
            made.transform.localScale = prefab.transform.localScale * scale;
            // markers fade with the map rather than on their own schedule
            TransparencyAnimator.Register(made.transform);
            icons.Add(made);
            // an unset color, so the first Paint always happens
            painted.Add(new Color(-1f, -1f, -1f, -1f));
        }

        return icons[index];
    }

    // Peers come and go; keep the objects rather than the churn, and hide the spares.
    private static void Retire(List<GameObject> icons, int keep) {
        for (var i = 0; i < icons.Count; i++) {
            if (icons[i] == null) {
                continue;
            }

            var wanted = i < keep;
            if (icons[i].activeSelf != wanted) {
                icons[i].SetActive(wanted);
            }
        }
    }

    private static readonly List<RandomizerGhost.Marker> Wanted = new List<RandomizerGhost.Marker>();

    private static readonly List<GameObject> Icons = new List<GameObject>();

    private static readonly List<Color> Painted = new List<Color>();

    private static readonly List<GameObject> LinkIcons = new List<GameObject>();

    private static readonly List<Color> LinkPainted = new List<Color>();
}

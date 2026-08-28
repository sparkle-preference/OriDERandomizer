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
            Retire(0);
            return;
        }

        RandomizerGhost.Markers(Wanted);
        for (var i = 0; i < Wanted.Count; i++) {
            var marker = Wanted[i];
            var icon = At(i, map);
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

        Retire(Wanted.Count);
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

    private static GameObject At(int index, AreaMapUI map) {
        while (Icons.Count <= index) {
            var made = Object.Instantiate(map.PlayerPositionMarkerPrefab);
            made.name = "randomizerPeerMarker";
            made.transform.parent = map.FadeOutGroup;
            made.transform.localScale = map.PlayerPositionMarkerPrefab.transform.localScale * Relative;
            // markers fade with the map rather than on their own schedule
            TransparencyAnimator.Register(made.transform);
            Icons.Add(made);
            // an unset colour, so the first Paint always happens
            Painted.Add(new Color(-1f, -1f, -1f, -1f));
        }

        return Icons[index];
    }

    // Peers come and go; keep the objects rather than the churn, and hide the spares.
    private static void Retire(int keep) {
        for (var i = 0; i < Icons.Count; i++) {
            if (Icons[i] == null) {
                continue;
            }

            var wanted = i < keep;
            if (Icons[i].activeSelf != wanted) {
                Icons[i].SetActive(wanted);
            }
        }
    }

    private static readonly List<RandomizerGhost.Marker> Wanted = new List<RandomizerGhost.Marker>();

    private static readonly List<GameObject> Icons = new List<GameObject>();

    private static readonly List<Color> Painted = new List<Color>();
}

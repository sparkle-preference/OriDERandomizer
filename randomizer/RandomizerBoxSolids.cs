using System.Collections.Generic;
using UnityEngine;

// Solid boxes as colliders in the world. Ori's movement finds floors and walls by
// physics contacts and unmasked raycasts, so a plain box collider on the level's
// layer is ground to stand on and a wall to slide down, grab and jump from. Rebuilt
// whenever the set of boxes in force changes.
public static class RandomizerBoxSolids {
    private static readonly List<GameObject> made = new List<GameObject>();

    private static int built = -1;

    // the level's colliders run through Ori's plane, and so do these
    private const float Depth = 10f;

    public static void Tick() {
        if (GameController.Instance == null || GameController.Instance.GameInTitleScreen) {
            Clear();
            return;
        }

        var sein = Game.Characters.Sein;
        if (built == RandomizerBoxes.Version || sein == null) {
            return;
        }

        // The layer of the ground Ori stands on, remembered once seen: the first frame after
        // a load has no contact yet. Not "platform", the drop-through layer.
        var ground = sein.PlatformBehaviour.PlatformMovementListOfColliders.GroundCollider;
        if (IsTerrain(ground)) {
            groundLayer = ground.gameObject.layer;
        } else if (groundLayer < 0) {
            groundLayer = LayerBelow(sein.Position);
        }

        if (groundLayer < 0) {
            return;
        }

        Clear();
        built = RandomizerBoxes.Version;
        var layer = groundLayer;
        foreach (var box in RandomizerBoxes.Active) {
            if (box.Type != RandomizerBox.Kind.Solid) {
                continue;
            }

            var obj = new GameObject("randomizerSolid");
            obj.layer = layer;
            var area = box.Area;
            obj.transform.position = new Vector3(area.center.x, area.center.y, 0f);
            var collider = obj.AddComponent<BoxCollider>();
            collider.size = new Vector3(area.width, area.height, Depth);
            made.Add(obj);
        }
    }

    private static int groundLayer = -1;

    // The level's ground is mesh colliders; a one-way platform, a mushroom cap or a
    // trigger volume is not the template for a solid block.
    private static bool IsTerrain(Collider collider) {
        return collider != null && collider is MeshCollider && !collider.isTrigger
            && collider.gameObject.layer != LayerMask.NameToLayer("platform");
    }

    // the nearest terrain under a point, for when Ori is in the air or on something else
    private static int LayerBelow(Vector3 at) {
        var nearest = float.MaxValue;
        var layer = -1;
        foreach (var hit in Physics.RaycastAll(at + Vector3.up * 0.5f, Vector3.down, 60f)) {
            if (IsTerrain(hit.collider) && hit.distance < nearest) {
                nearest = hit.distance;
                layer = hit.collider.gameObject.layer;
            }
        }

        return layer;
    }

    public static void Clear() {
        foreach (var obj in made) {
            if (obj != null) {
                Object.Destroy(obj);
            }
        }

        made.Clear();
        built = -1;
    }
}

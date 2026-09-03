using System;
using System.Collections.Generic;
using UnityEngine;

// Replacement art for the ability tree nodes the randomizer repurposed. The vanilla icon
// still says what the vanilla ability did.
public static class RandomizerSkillIcons {
    private static readonly Dictionary<AbilityType, string> Art = new Dictionary<AbilityType, string> {
        { AbilityType.MapMarkers, "skill_drop_efficiency.png" },
        { AbilityType.HealthMarkers, "skill_health_recovery.png" },
        { AbilityType.EnergyMarkers, "skill_energy_recovery.png" },
        { AbilityType.AbilityMarkers, "skill_spirit_efficiency.png" },
        { AbilityType.SoulEfficiency, "skill_spirit_potency.png" },
        { AbilityType.Sense, "skill_sense.png" }
    };

    public static void Apply(SkillTreeManager tree) {
        try {
            foreach (var lane in new[] { tree.EnergyLane, tree.UtilityLane, tree.CombatLane }) {
                if (lane == null || lane.Skills == null) {
                    continue;
                }

                foreach (var skill in lane.Skills) {
                    string resource;
                    if (skill == null || skill.Icon == null || skill.Icon.sharedMaterial == null
                        || !Art.TryGetValue(skill.Ability, out resource)) {
                        continue;
                    }

                    var texture = Load(resource);
                    var vanilla = skill.Icon.sharedMaterial.mainTexture;
                    if (texture != null && vanilla != null && texture != vanilla) {
                        Replace(tree, vanilla, texture);
                    }
                }
            }
        } catch (Exception e) {
            Randomizer.LogError("skill icons: " + e);
        }
    }

    private static Texture2D Load(string resource) {
        Texture2D texture;
        if (loaded.TryGetValue(resource, out texture) && texture != null) {
            return texture;
        }

        var bytes = RandomizerResources.ReadResource(resource);
        if (bytes == null) {
            return null;
        }

        texture = new Texture2D(2, 2, TextureFormat.ARGB32, true);
        texture.LoadImage(bytes);
        texture.name = resource;
        texture.wrapMode = TextureWrapMode.Clamp;
        loaded[resource] = texture;
        return texture;
    }

    // A node's art is drawn by several renderers with their own materials: the node itself,
    // the large hover art, and LearntSkillGlow once the node is earned. They share the
    // texture, so matching on it is what reaches all three.
    private static void Replace(SkillTreeManager tree, Texture vanilla, Texture replacement) {
        foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true)) {
            Retexture(renderer, vanilla, replacement);
        }

        if (tree.LargeIcon != null) {
            foreach (var renderer in tree.LargeIcon.GetComponentsInChildren<Renderer>(true)) {
                Retexture(renderer, vanilla, replacement);
            }
        }
    }

    private static void Retexture(Renderer renderer, Texture vanilla, Texture replacement) {
        if (renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture != vanilla) {
            return;
        }

        renderer.sharedMaterial.mainTexture = replacement;
        Quadify(renderer.GetComponent<MeshFilter>());
    }

    // Each icon is drawn on a mesh traced to the shape of its own art -- 400-500 verts, not a
    // quad -- so replacement art would be clipped to the vanilla silhouette. A quad over the
    // same bounds lets the texture's own alpha be the shape.
    private static void Quadify(MeshFilter filter) {
        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.name == QuadName) {
            return;
        }

        var bounds = filter.sharedMesh.bounds;
        var mesh = new Mesh { name = QuadName };
        mesh.vertices = new[] {
            new Vector3(bounds.min.x, bounds.min.y, 0f),
            new Vector3(bounds.max.x, bounds.min.y, 0f),
            new Vector3(bounds.max.x, bounds.max.y, 0f),
            new Vector3(bounds.min.x, bounds.max.y, 0f)
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        // the shader multiplies by vertex colour, and an unset array is not white
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
    }

    private const string QuadName = "RandomizerIconQuad";

    private static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();
}

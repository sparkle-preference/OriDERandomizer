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

    // Sense's icon is a cave with something sensed inside it. The inside is drawn from one
    // of these, picked fresh every time the tree opens.
    private const string SenseCave = "skill_sense_cave.png";

    private static readonly string[] SenseFinds = {
        "sense_warmth.png", "sense_watervein.png", "sense_gumonseal.png",
        "sense_sunstone.png", "sense_cleanwater.png", "sense_windrestored.png"
    };

    // The tree is rebuilt with vanilla meshes on every open; the materials are shared assets
    // that keep whatever art was last set. Each pass has to recognise both.
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

                    var texture = skill.Ability == AbilityType.Sense ? ComposeSense() : Load(resource);
                    if (texture == null) {
                        continue;
                    }

                    Texture earlier;
                    shown.TryGetValue(skill.Ability, out earlier);
                    Replace(tree, texture, skill.Icon.sharedMaterial.mainTexture, earlier);
                    shown[skill.Ability] = texture;
                }
            }
        } catch (Exception e) {
            Randomizer.LogError("skill icons: " + e);
        }
    }

    private static readonly Dictionary<AbilityType, Texture> shown = new Dictionary<AbilityType, Texture>();

    // Cave plus one find, composited on the CPU: both layers are full-canvas PNGs already in
    // position, and LoadImage leaves them readable. The cave draws OVER the find, so a symbol
    // that overruns the opening is trimmed by the cave rather than pasted on top of it.
    // One composite per find, kept: a material may still be showing the last one.
    private static Texture2D ComposeSense() {
        var name = SenseFinds[UnityEngine.Random.Range(0, SenseFinds.Length)];
        Texture2D made;
        if (composites.TryGetValue(name, out made) && made != null) {
            return made;
        }

        var cave = Load(SenseCave);
        var find = Load(name);
        if (cave == null || find == null
            || cave.width != find.width || cave.height != find.height) {
            return Load("skill_sense.png");
        }

        var top = cave.GetPixels32();
        var under = find.GetPixels32();
        for (var i = 0; i < under.Length; i++) {
            under[i] = Over(top[i], under[i]);
        }

        made = new Texture2D(cave.width, cave.height, TextureFormat.ARGB32, true);
        made.SetPixels32(under);
        made.Apply();
        made.wrapMode = TextureWrapMode.Clamp;
        made.name = "sense " + name;
        composites[name] = made;
        return made;
    }

    private static readonly Dictionary<string, Texture2D> composites = new Dictionary<string, Texture2D>();

    private static Color32 Over(Color32 src, Color32 dst) {
        if (src.a == 255 || dst.a == 0) {
            return src;
        }

        if (src.a == 0) {
            return dst;
        }

        var sa = src.a / 255f;
        var da = dst.a / 255f * (1f - sa);
        var a = sa + da;
        return new Color32(
            (byte)((src.r * sa + dst.r * da) / a),
            (byte)((src.g * sa + dst.g * da) / a),
            (byte)((src.b * sa + dst.b * da) / a),
            (byte)(a * 255f));
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
    private static void Replace(SkillTreeManager tree, Texture replacement, params Texture[] matches) {
        foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true)) {
            Retexture(renderer, replacement, matches);
        }

        if (tree.LargeIcon != null) {
            foreach (var renderer in tree.LargeIcon.GetComponentsInChildren<Renderer>(true)) {
                Retexture(renderer, replacement, matches);
            }
        }
    }

    private static void Retexture(Renderer renderer, Texture replacement, Texture[] matches) {
        var material = renderer.sharedMaterial;
        if (material == null) {
            return;
        }

        var current = material.mainTexture;
        var ours = current == replacement;
        foreach (var match in matches) {
            if (match != null && current == match) {
                ours = true;
            }
        }

        if (!ours) {
            return;
        }

        material.mainTexture = replacement;
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

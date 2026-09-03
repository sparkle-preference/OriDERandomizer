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
                    var vanilla = skill.Icon.sharedMaterial.mainTexture;
                    if (texture != null && vanilla != null && texture != vanilla) {
                        Replace(tree, vanilla, texture);
                        if (skill.Ability == AbilityType.Sense) {
                            RetireSenseComposite(vanilla);
                            senseComposite = texture;
                        }
                    }
                }
            }
        } catch (Exception e) {
            Randomizer.LogError("skill icons: " + e);
        }
    }

    // Cave plus one find, composited on the CPU: both layers are full-canvas PNGs already in
    // position, and LoadImage leaves them readable. The cave draws OVER the find, so a symbol
    // that overruns the opening is trimmed by the cave rather than pasted on top of it.
    private static Texture2D ComposeSense() {
        var cave = Load(SenseCave);
        var find = Load(SenseFinds[UnityEngine.Random.Range(0, SenseFinds.Length)]);
        if (cave == null || find == null
            || cave.width != find.width || cave.height != find.height) {
            return Load("skill_sense.png");
        }

        var top = cave.GetPixels32();
        var under = find.GetPixels32();
        for (var i = 0; i < under.Length; i++) {
            under[i] = Over(top[i], under[i]);
        }

        var made = new Texture2D(cave.width, cave.height, TextureFormat.ARGB32, true);
        made.SetPixels32(under);
        made.Apply();
        made.wrapMode = TextureWrapMode.Clamp;
        made.name = "sense " + find.name;
        return made;
    }

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

    // The composites are made per menu open, so the one being replaced has to go with it.
    private static void RetireSenseComposite(Texture replaced) {
        if (senseComposite != null && senseComposite == replaced) {
            UnityEngine.Object.Destroy(senseComposite);
        }

        senseComposite = null;
    }

    private static Texture2D senseComposite;

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

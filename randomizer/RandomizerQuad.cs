using System.Collections.Generic;
using UnityEngine;

// Draws an embedded PNG as a flat quad. Game sprites cannot be re-textured -- their
// UberShader variants have the atlas rect baked in -- but the hint background's variant
// maps a whole texture, so its material plus 0..1 UVs renders a standalone image.
// Stock shaders are stripped from this build, so there is no other way in.
public static class RandomizerQuad {
    public static GameObject Build(string name, string resource, Renderer order) {
        return BuildTextured(name, Texture(resource), order);
    }

    // For a texture drawn at runtime rather than loaded from a resource.
    public static GameObject BuildTextured(string name, Texture2D texture, Renderer order) {
        var material = WholeTextureMaterial();
        if (material == null || texture == null) {
            return null;
        }

        material.SetTexture("_MainTex", texture);
        material.SetColor("_Color", Color.white);

        var obj = new GameObject(name);
        obj.AddComponent<MeshFilter>().mesh = UnitQuad();
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.castShadows = false;
        renderer.receiveShadows = false;
        if (order != null) {
            obj.layer = order.gameObject.layer;
            renderer.sortingLayerID = order.sortingLayerID;
            renderer.sortingOrder = order.sortingOrder;
        }

        return obj;
    }

    public static Material WholeTextureMaterial() {
        var controller = Game.UI.MessageController;
        var hintMessage = controller == null ? null : controller.HintMessage;
        if (hintMessage == null) {
            return null;
        }

        var background = hintMessage.transform.FindChild("background/hintMessageBackground");
        if (background == null) {
            return null;
        }

        var renderer = background.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null) {
            return null;
        }

        return new Material(renderer.sharedMaterial);
    }

    // 1x1 quad centred on its origin, UVs spanning the whole texture
    public static Mesh UnitQuad() {
        var mesh = new Mesh();
        mesh.vertices = new Vector3[4] {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.uv = new Vector2[4] {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.colors = new Color[4] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new int[6] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Texture2D Texture(string resource) {
        Texture2D cached;
        if (textures.TryGetValue(resource, out cached) && cached != null) {
            return cached;
        }

        var bytes = RandomizerResources.ReadResource(resource);
        if (bytes == null) {
            return null;
        }

        cached = new Texture2D(0, 0);
        cached.LoadImage(bytes);
        textures[resource] = cached;
        return cached;
    }

    private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
}

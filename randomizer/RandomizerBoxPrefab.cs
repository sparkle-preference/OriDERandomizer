#nullable enable

using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class RandomizerBoxPrefab : MonoBehaviour {
    private void OnDestroy() {
        if (meshFilter.sharedMesh != null) {
            Destroy(meshFilter.sharedMesh);
        }
    }

    public static RandomizerBoxPrefab Create(RandomizerBox box) {
        var go = Instantiate(Instance.gameObject);
        var rb = go.GetComponent<RandomizerBoxPrefab>();
        rb.Init(box);
        rb.UpdateActive();
        return rb;
    }

    public void UpdateActive() {
        gameObject.SetActive(!Box.IsOff && RandomizerBoxes.Enabled);
    }

    private void Init(RandomizerBox box) {
        Box = box;
        var rect = box.Rect;

        transform.position = rect.center;

        collider.isTrigger = !box.Solid;
        collider.size = new Vector3(rect.width, rect.height, 1f);

        if (box.Invisible) {
            renderer.enabled = false;
        } else {
            var fillColor = (Color)box.Color;
            var edgeColor = fillColor;
            edgeColor.a = Math.Min(edgeColor.a + 0.45f, 1f);
            meshFilter.sharedMesh = CreateMesh(fillColor, edgeColor, box.ParallaxDepth);
            renderer.sharedMaterial = GetMaterial(box.RenderDepth);
            UberShaderRenderQueue.SetRenderQueueExplicit(gameObject, box.RenderDepth);
        }

        if (box.DamageAmount > 0f) {
            GameObject ddGo;
            if (box.ExtendedHitboxes) {
                gameObject.layer = RandomizerLayers.RandomizerSolidDamage;
                ddGo = gameObject;
            } else {
                ddGo = new GameObject("Damage Dealer") {
                    layer = RandomizerLayers.KillEverything,
                };
                ddGo.transform.SetParent(transform, false);

                var ddCollider = ddGo.AddComponent<BoxCollider>();
                ddCollider.isTrigger = true;
                ddCollider.size = new Vector3(rect.width, rect.height, 1f);
            }

            var dd = ddGo.AddComponent<DamageDealer>();
            dd.Damage = box.DamageAmount;
            dd.DamageType = box.DamageType;
            dd.PlayerOnly = box.DamageTarget == RandomizerBox.BoxDamageTarget.Player;
            dd.UseExtendedHitboxes = box.ExtendedHitboxes;
        }

        if (box.Unsafe) {
            var zoneGo = new GameObject("Unsafe Zone");
            var zoneTf = zoneGo.transform;
            zoneTf.SetParent(transform, false);
            zoneTf.localScale = rect.size;

            zoneGo.AddComponent<NoSoulFlameZone>();
        }
    }

    private void OnTriggerStay(Collider other) {
        if (other.GetComponent<SeinCharacter>() == null) {
            return;
        }

        if (!Characters.Sein || Characters.Sein.IsSuspended) {
            return;
        }

        if (Box.BoxNumber < 0) {
            return;
        }

        RandomizerBoxes.MarkActive(Box.BoxNumber);
    }

    private void OnCollisionStay(Collision other) {
        OnTriggerStay(other.collider);
    }

    private Mesh CreateMesh(Color color, Color edgeColor, float zPos) {
        var mesh = new Mesh();
        var size = Box.Rect.size;
        var xMin = -size.x * 0.5f;
        var xMax = size.x * 0.5f;
        var yMin = -size.y * 0.5f;
        var yMax = size.y * 0.5f;
        mesh.SetVertices(
            [
                new Vector3(xMin, yMin, zPos),
                new Vector3(xMin, yMax, zPos),
                new Vector3(xMax, yMax, zPos),
                new Vector3(xMax, yMin, zPos),
                new Vector3(xMin, yMin, zPos),
                new Vector3(xMin, yMax, zPos),
                new Vector3(xMax, yMax, zPos),
                new Vector3(xMax, yMin, zPos),
                new Vector3(xMin + EdgeWidth, yMin + EdgeWidth, zPos),
                new Vector3(xMin + EdgeWidth, yMax - EdgeWidth, zPos),
                new Vector3(xMax - EdgeWidth, yMax - EdgeWidth, zPos),
                new Vector3(xMax - EdgeWidth, yMin + EdgeWidth, zPos),
            ]
        );

        var colors = new List<Color>(12);
        for (var i = 0; i < 4; ++i) {
            colors.Add(color);
        }

        for (var i = 4; i < 12; ++i) {
            colors.Add(edgeColor);
        }

        mesh.SetColors(colors);

        if (size.x <= 2f * EdgeWidth || size.y <= 2f * EdgeWidth) {
            mesh.triangles = [4, 6, 5, 4, 7, 6];
        } else {
            mesh.triangles = [0, 2, 1, 0, 3, 2, 4, 8, 5, 8, 9, 5, 5, 9, 6, 9, 10, 6, 6, 10, 7, 10, 11, 7, 7, 11, 4, 11, 8, 4];
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    private static RandomizerBoxPrefab CreatePrefab() {
        var go = new GameObject("RandomizerBoxPrefab") {
            layer = RandomizerLayers.Solids,
        };
        DontDestroyOnLoad(go);

        go.SetActive(false);

        var rb = go.AddComponent<RandomizerBoxPrefab>();
        rb.meshFilter = go.AddComponent<MeshFilter>();
        rb.renderer = go.AddComponent<MeshRenderer>();
        rb.collider = go.AddComponent<BoxCollider>();
        rb.collider.isTrigger = true;

        return rb;
    }

    private static Material GetMaterial(float renderDepth) {
        if (MaterialsByDepth.TryGetValue(renderDepth, out var material)) {
            return material;
        }

        material = Instantiate(Material);
        MaterialsByDepth[renderDepth] = material;
        return material;
    }

    private static Material CreateMaterial() {
        var material = new Material(Shader.Find("Hidden/Internal-Colored")) {
            name = "Randomizer Box Material",
        };
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        material.SetInt("_ZWrite", 0);
        material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        return material;
    }

    private const float EdgeWidth = 0.25f;

    private static RandomizerBoxPrefab? _instance;

    public static RandomizerBoxPrefab Instance {
        get {
            if (_instance == null) {
                _instance = CreatePrefab();
            }

            return _instance;
        }
    }

    private static Material? _material;

    private static Material Material {
        get {
            if (_material == null) {
                _material = CreateMaterial();
            }

            return _material;
        }
    }

    private static readonly Dictionary<float, Material> MaterialsByDepth = new();

    public RandomizerBox Box = null!;

    public MeshFilter meshFilter = null!;

    public MeshRenderer renderer = null!;

    public BoxCollider collider = null!;
}

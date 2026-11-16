#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using Game;
using UnityEngine;
using static RandomizerBox;

public class RandomizerBoxPrefab : MonoBehaviour {
    private void OnDestroy() {
        if (meshFilter.sharedMesh != null) {
            Destroy(meshFilter.sharedMesh);
        }

        if (meshCollider != null) {
            Destroy(meshCollider.sharedMesh);
        }
    }

    public static RandomizerBoxPrefab Create(RandomizerBox box) {
        var go = Instantiate(PrefabInstance.gameObject);
        go.transform.SetParent(BoxParent, false);
        var rb = go.GetComponent<RandomizerBoxPrefab>();
        rb.Init(box);
        rb.UpdateActive();
        return rb;
    }

    public void UpdateActive() {
        gameObject.SetActive(!Box.IsOff && RandomizerBoxes.Enabled);
    }

    // everything the box owns hangs off this transform, so moving it moves them all
    public void Place() {
        transform.position = (RandomizerBoxes.ShowingLines ? Box.Rect : Box.CurrentRect).center;
    }

    private void Init(RandomizerBox box) {
        Box = box;
        var rect = box.Rect;

        Place();

        var fillColor = (Color)box.Color;
        var edgeColor = fillColor;
        edgeColor.a = Math.Min(edgeColor.a + 0.45f, 1f);

        Mesh? rendererMesh = null;
        Mesh? colliderMesh = null;
        if (box.Points != null) {
            var points = !box.Convex ? box.Points : [QuickHull.Compute(box.Points[0])];

            CreateMeshes(box.ShapeType, points, rect.center, box.ParallaxDepth, fillColor, edgeColor, out rendererMesh, out colliderMesh, out var simplifiedShape);
            if (simplifiedShape != null) {
                box.SimplifiedShape = simplifiedShape;
            }
        }

        if (box.Invisible) {
            renderer.enabled = false;
            if (rendererMesh != null) {
                Destroy(rendererMesh);
            }
        } else {
            meshFilter.sharedMesh = rendererMesh ?? CreateRectangleMesh(fillColor, edgeColor, box.ParallaxDepth);
            renderer.sharedMaterial = GetMaterial(box.RenderDepth);
            UberShaderRenderQueue.SetRenderQueueExplicit(gameObject, box.RenderDepth);
        }

        if (colliderMesh == null) {
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = !box.Solid;
            collider.size = new Vector3(rect.width, rect.height, ColliderDepth);
        } else if (box.Solid || box.Convex) {
            meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.convex = box.Convex;
            meshCollider.sharedMesh = colliderMesh;
            meshCollider.isTrigger = !box.Solid;
        } else {
            Destroy(colliderMesh);
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

                if (colliderMesh == null) {
                    var ddCollider = ddGo.AddComponent<BoxCollider>();
                    ddCollider.isTrigger = !box.Solid;
                    ddCollider.size = new Vector3(rect.width, rect.height, ColliderDepth);
                } else {
                    var ddCollider = ddGo.AddComponent<MeshCollider>();
                    ddCollider.convex = box.Convex;
                    ddCollider.sharedMesh = colliderMesh;
                    ddCollider.isTrigger = !box.Solid;
                }
            }

            var dd = ddGo.AddComponent<DamageDealer>();
            dd.Damage = box.DamageAmount;
            dd.DamageType = box.DamageType;
            dd.PlayerOnly = box.DamageTarget == BoxDamageTarget.Player;
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

    private void CreateMeshes(BoxShapeType shapeType, List<List<Vector2>> points, Vector2 offset, float z, Color fillColor, Color edgeColor,
        out Mesh? visualMesh, out Mesh? colliderMesh, out List<object>? simplifiedShape) {

        if (points.Count == 0) {
            visualMesh = new Mesh();
            colliderMesh = new Mesh();
            simplifiedShape = null;
            return;
        }

        const float scale = 1024f;
        const float scaleDown = 1f / scale;

        var isPreview = shapeType == BoxShapeType.PreviewPolygon;

        Paths64 previewPaths = [];
        Paths64 outerPaths = [];
        for (var i = 0; i < points.Count; i++) {
            var loop = points[i];
            Path64 loopPath = [.. loop.Select(p => new Point64((p.x - offset.x) * scale, (p.y - offset.y) * scale))];
            if (loopPath.Count < 3) {
                if (isPreview && loopPath.Count > 0) {
                    previewPaths.Add(loopPath);
                }
            } else {
                outerPaths.AddRange(Clipper.Union([loopPath], FillRule.EvenOdd));
                if (isPreview && i == points.Count - 1) {
                    previewPaths.Add([loopPath[0]]);
                }
            }
        }

        outerPaths = Clipper.Union(outerPaths, FillRule.NonZero);
        var innerPaths = Clipper.InflatePaths(outerPaths, -EdgeWidth * scale, JoinType.Miter, EndType.Polygon);
        var edge = Clipper.Difference(outerPaths, innerPaths, FillRule.Positive);

        var edgeResult = Clipper.Triangulate(edge, out var edgeTriangulation, false);
        Paths64 innerTriangulation;
        if (edgeResult is not (TriangulateResult.success or TriangulateResult.noPolygons)) {
            Randomizer.log($"Edge triangulation of failed, box will have no edge");
            if (Clipper.Triangulate(outerPaths, out innerTriangulation, false) != TriangulateResult.success) {
                Randomizer.log("Outer path also failed to triangulate, falling back to rectangle");
                visualMesh = colliderMesh = null;
                simplifiedShape = null;
                return;
            }
        } else {
            Clipper.Triangulate(innerPaths, out innerTriangulation, false);
        }

        if (isPreview) {
            previewPaths = Clipper.InflatePaths(previewPaths, 0.1 * scale, JoinType.Round, EndType.Round);
            Clipper.Triangulate(previewPaths, out var previewTriangulation, false);
            edgeTriangulation.AddRange(previewTriangulation);
        }

        visualMesh = CreateVisualMesh(edgeTriangulation, innerTriangulation, scaleDown, z, fillColor, edgeColor);
        colliderMesh = CreateColliderMesh(outerPaths, scaleDown);
        simplifiedShape = ToRawShape(shapeType, outerPaths, scaleDown, offset);
    }

    private Mesh CreateVisualMesh(Paths64 edgeTriangulation, Paths64 innerTriangulation, float scaleDown, float z, Color fillColor, Color edgeColor) {
        var edgeCount = edgeTriangulation.Count * 3;
        var innerCount = innerTriangulation.Count * 3;
        var vertices = new Vector3[edgeCount + innerCount];

        for (var i = 0; i < edgeTriangulation.Count; i++) {
            var tri = edgeTriangulation[i];
            vertices[i * 3] = new Vector3(tri[0].X * scaleDown, tri[0].Y * scaleDown, z);
            vertices[i * 3 + 1] = new Vector3(tri[1].X * scaleDown, tri[1].Y * scaleDown, z);
            vertices[i * 3 + 2] = new Vector3(tri[2].X * scaleDown, tri[2].Y * scaleDown, z);
        }

        for (var i = 0; i < innerTriangulation.Count; i++) {
            var tri = innerTriangulation[i];
            vertices[edgeCount + i * 3] = new Vector3(tri[0].X * scaleDown, tri[0].Y * scaleDown, z);
            vertices[edgeCount + i * 3 + 1] = new Vector3(tri[1].X * scaleDown, tri[1].Y * scaleDown, z);
            vertices[edgeCount + i * 3 + 2] = new Vector3(tri[2].X * scaleDown, tri[2].Y * scaleDown, z);
        }

        var triangles = new int[vertices.Length];
        for (var i = 0; i < triangles.Length; i++) {
            triangles[i] = i;
        }

        var colors = new Color[vertices.Length];
        for (var i = 0; i < edgeCount; i++) {
            colors[i] = edgeColor;
        }

        for (var i = 0; i < innerCount; i++) {
            colors[edgeCount + i] = fillColor;
        }

        var mesh = new Mesh {
            name = "Box Mesh",
            vertices = vertices,
            triangles = triangles,
            colors = colors,
        };
        mesh.RecalculateBounds();
        mesh.Optimize();
        return mesh;
    }

    private Mesh CreateColliderMesh(Paths64 paths, float scaleDown) {
        var z = ColliderDepth * 0.5f;

        var vertices = new List<Vector3>();
        var tris = new List<int>();

        foreach (var path in paths) {
            var firstIdx = vertices.Count;

            foreach (var p in path) {
                var x = p.X * scaleDown;
                var y = p.Y * scaleDown;
                vertices.Add(new Vector3(x, y, z));
                vertices.Add(new Vector3(x, y, -z));
            }

            for (var i = firstIdx; i < vertices.Count - 2; i += 2) {
                tris.Add(i);
                tris.Add(i + 1);
                tris.Add(i + 2);
                tris.Add(i + 1);
                tris.Add(i + 3);
                tris.Add(i + 2);
            }

            tris.Add(vertices.Count - 2);
            tris.Add(vertices.Count - 1);
            tris.Add(firstIdx);
            tris.Add(vertices.Count - 1);
            tris.Add(firstIdx + 1);
            tris.Add(firstIdx);
        }

        var mesh = new Mesh {
            name = "Box Collider Mesh",
            vertices = vertices.ToArray(),
            triangles = tris.ToArray(),
        };
        mesh.RecalculateBounds();
        mesh.Optimize();
        return mesh;
    }

    private List<object> ToRawShape(BoxShapeType shapeType, Paths64 paths, float scaleDown, Vector2 offset) {
        List<object> rawShape = [shapeType switch {
            BoxShapeType.ConvexHull => "convex",
            BoxShapeType.Polygon => "loop",
            BoxShapeType.PreviewPolygon => "previewLoop",
            _ => throw new ArgumentOutOfRangeException(nameof(shapeType), shapeType, null),
        }];

        for (var i = 0; i < paths.Count; i++) {
            if (i != 0) {
                rawShape.Add("loop");
            }

            foreach (var point in paths[i]) {
                rawShape.Add(new Vector2(point.X * scaleDown + offset.x, point.Y * scaleDown + offset.y));
            }
        }

        return rawShape;
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

    private Mesh CreateRectangleMesh(Color color, Color edgeColor, float zPos) {
        var size = Box.Rect.size;
        float hx = size.x * 0.5f, hy = size.y * 0.5f;
        // every hard boundary fades across a pixel centered on it, so a tilted camera's slanted edges don't stair-step
        var h = Mathf.Min(FeatherWidth * 0.5f, Mathf.Min(hx, hy));
        var clearFill = new Color(color.r, color.g, color.b, 0f);
        var clearEdge = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0f);
        var verts = new List<Vector3>();
        var colors = new List<Color>();
        var tris = new List<int>();

        void Corners(float inset, Color c) {
            verts.Add(new Vector3(-hx + inset, -hy + inset, zPos));
            verts.Add(new Vector3(-hx + inset, hy - inset, zPos));
            verts.Add(new Vector3(hx - inset, hy - inset, zPos));
            verts.Add(new Vector3(hx - inset, -hy + inset, zPos));
            colors.AddRange(new[] { c, c, c, c });
        }

        void Solid(float inset, Color c) {
            var at = verts.Count;
            Corners(inset, c);
            tris.AddRange(new[] { at, at + 2, at + 1, at, at + 3, at + 2 });
        }

        void Ring(float outer, Color outerColor, float inner, Color innerColor) {
            var at = verts.Count;
            Corners(outer, outerColor);
            Corners(inner, innerColor);
            for (var k = 0; k < 4; k++) {
                int o = at + k, next = at + (k + 1) % 4;
                tris.AddRange(new[] { o, o + 4, next, o + 4, next + 4, next });
            }
        }

        if (hx <= EdgeWidth || hy <= EdgeWidth) {
            Solid(h, edgeColor);
            Ring(-h, clearEdge, h, edgeColor);
        } else {
            Solid(h, color);
            Ring(-h, clearFill, h, color);
            Ring(-h, clearEdge, h, edgeColor);
            var seam = Mathf.Min(h, Mathf.Min(hx, hy) - EdgeWidth);
            Ring(h, edgeColor, EdgeWidth - seam, edgeColor);
            Ring(EdgeWidth - seam, edgeColor, EdgeWidth + seam, clearEdge);
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static RandomizerBoxPrefab CreatePrefab() {
        var go = new GameObject("RandomizerBoxPrefab") {
            layer = RandomizerLayers.Solids,
        };
        DontDestroyOnLoad(go);

        go.SetActive(false);
        go.transform.SetParent(BoxParent, false);

        var rb = go.AddComponent<RandomizerBoxPrefab>();
        rb.meshFilter = go.AddComponent<MeshFilter>();
        rb.renderer = go.AddComponent<MeshRenderer>();

        return rb;
    }

    public static Material GetMaterial(float renderDepth) {
        if (MaterialsByDepth.TryGetValue(renderDepth, out var material)) {
            return material;
        }

        material = CreateMaterial();
        MaterialsByDepth[renderDepth] = material;
        return material;
    }

    // the game reads each frame's alpha back a frame late, so a box that wrote alpha would trail a ghost;
    // built fresh each time, since a copy drops the properties UI/Default doesn't declare
    private static Material CreateMaterial() {
        var ui = Shader.Find("UI/Default");
        if (ui != null) {
            var colorOnly = new Material(ui) {
                name = "Randomizer Box Material",
            };
            colorOnly.SetFloat("_ColorMask", (float)(UnityEngine.Rendering.ColorWriteMask.Red | UnityEngine.Rendering.ColorWriteMask.Green | UnityEngine.Rendering.ColorWriteMask.Blue));
            colorOnly.SetVector("_ClipRect", new Vector4(-1e7f, -1e7f, 1e7f, 1e7f));
            colorOnly.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
            return colorOnly;
        }

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

    public const float EdgeWidth = 0.25f;

    public const float ColliderDepth = 100f;

    // a pixel of world at the usual zoom, which shows about 36 units of height
    private static float FeatherWidth => 36f / Math.Max(Screen.height, 1);

    private static Transform BoxParent => field ??= new GameObject("RandomizerBoxes").transform;

    private static RandomizerBoxPrefab PrefabInstance => field ??= CreatePrefab();

    private static readonly Dictionary<float, Material> MaterialsByDepth = new();

    public RandomizerBox Box = null!;

    public MeshFilter meshFilter = null!;

    public MeshRenderer renderer = null!;

    public MeshCollider? meshCollider;
}

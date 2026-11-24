#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using UnityEngine;
using static RandomizerBox;

// A shaped box's geometry as plain data: nothing here may touch the engine, so it runs outside the game.
public static class RandomizerBoxGeometry {
    // Clipper units: world units times Scale, relative to the offset Compute was given
    public class Shape {
        public Paths64 Outline = [];

        public Paths64 EdgeTriangles = [];

        public Paths64 FillTriangles = [];
    }

    // null for a rectangle; a convex box's points become their hull
    public static List<List<Vector2>>? Loops(RandomizerBox box) {
        if (box.Points == null) {
            return null;
        }

        return !box.Convex ? box.Points : [QuickHull.Compute(box.Points[0])];
    }

    // null when the shape can't be triangulated
    public static Shape? Compute(BoxShapeType shapeType, List<List<Vector2>> points, Vector2 offset) {
        var isPreview = shapeType == BoxShapeType.PreviewPolygon;

        Paths64 previewPaths = [];
        Paths64 outerPaths = [];
        for (var i = 0; i < points.Count; i++) {
            var loop = points[i];
            Path64 loopPath = [.. loop.Select(p => new Point64((p.x - offset.x) * Scale, (p.y - offset.y) * Scale))];
            if (loopPath.Count < 3) {
                if (isPreview && loopPath.Count > 0) {
                    previewPaths.Add(loopPath);
                }
            } else if (shapeType == BoxShapeType.OrientedPolygon) {
                outerPaths.Add(loopPath);
            } else {
                outerPaths.AddRange(Clipper.Union([loopPath], FillRule.EvenOdd));
                if (isPreview && i == points.Count - 1) {
                    previewPaths.Add([loopPath[0]]);
                }
            }
        }

        outerPaths = Clipper.Union(outerPaths, FillRule.NonZero);
        var innerPaths = Clipper.InflatePaths(outerPaths, -RandomizerBoxPrefab.EdgeWidth * Scale, JoinType.Miter, EndType.Polygon);
        var edge = Clipper.Difference(outerPaths, innerPaths, FillRule.Positive);

        var edgeResult = Clipper.Triangulate(edge, out var edgeTriangulation, false);
        Paths64 innerTriangulation;
        if (edgeResult is not (TriangulateResult.success or TriangulateResult.noPolygons)) {
            Randomizer.log($"Edge triangulation of failed, box will have no edge");
            if (Clipper.Triangulate(outerPaths, out innerTriangulation, false) != TriangulateResult.success) {
                Randomizer.log("Outer path also failed to triangulate, falling back to rectangle");
                return null;
            }
        } else {
            Clipper.Triangulate(innerPaths, out innerTriangulation, false);
        }

        if (isPreview) {
            previewPaths = Clipper.InflatePaths(previewPaths, 0.1 * Scale, JoinType.Round, EndType.Round);
            Clipper.Triangulate(previewPaths, out var previewTriangulation, false);
            edgeTriangulation.AddRange(previewTriangulation);
        }

        return new Shape {
            Outline = outerPaths,
            EdgeTriangles = edgeTriangulation,
            FillTriangles = innerTriangulation,
        };
    }

    public static void VisualVertices(Shape shape, float z, Color fillColor, Color edgeColor,
        out Vector3[] vertices, out int[] triangles, out Color[] colors) {

        var edgeTriangulation = shape.EdgeTriangles;
        var innerTriangulation = shape.FillTriangles;
        var edgeCount = edgeTriangulation.Count * 3;
        var innerCount = innerTriangulation.Count * 3;
        vertices = new Vector3[edgeCount + innerCount];

        for (var i = 0; i < edgeTriangulation.Count; i++) {
            var tri = edgeTriangulation[i];
            vertices[i * 3] = new Vector3(tri[0].X * ScaleDown, tri[0].Y * ScaleDown, z);
            vertices[i * 3 + 1] = new Vector3(tri[1].X * ScaleDown, tri[1].Y * ScaleDown, z);
            vertices[i * 3 + 2] = new Vector3(tri[2].X * ScaleDown, tri[2].Y * ScaleDown, z);
        }

        for (var i = 0; i < innerTriangulation.Count; i++) {
            var tri = innerTriangulation[i];
            vertices[edgeCount + i * 3] = new Vector3(tri[0].X * ScaleDown, tri[0].Y * ScaleDown, z);
            vertices[edgeCount + i * 3 + 1] = new Vector3(tri[1].X * ScaleDown, tri[1].Y * ScaleDown, z);
            vertices[edgeCount + i * 3 + 2] = new Vector3(tri[2].X * ScaleDown, tri[2].Y * ScaleDown, z);
        }

        triangles = new int[vertices.Length];
        for (var i = 0; i < triangles.Length; i++) {
            triangles[i] = i;
        }

        colors = new Color[vertices.Length];
        for (var i = 0; i < edgeCount; i++) {
            colors[i] = edgeColor;
        }

        for (var i = 0; i < innerCount; i++) {
            colors[edgeCount + i] = fillColor;
        }
    }

    public static void ColliderVertices(Paths64 paths, out List<Vector3> vertices, out List<int> tris) {
        var z = RandomizerBoxPrefab.ColliderDepth * 0.5f;

        vertices = new List<Vector3>();
        tris = new List<int>();

        foreach (var path in paths) {
            var firstIdx = vertices.Count;

            foreach (var p in path) {
                var x = p.X * ScaleDown;
                var y = p.Y * ScaleDown;
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
    }

    // the outline's area centroid in world units, relative to Compute's offset; holes wind the other way and subtract
    public static Vector2 Centroid(Paths64 outline) {
        double twiceArea = 0, x = 0, y = 0;
        foreach (var path in outline) {
            for (var i = 0; i < path.Count; i++) {
                var p = path[i];
                var q = path[(i + 1) % path.Count];
                var cross = (double)p.X * q.Y - (double)q.X * p.Y;
                twiceArea += cross;
                x += (p.X + (double)q.X) * cross;
                y += (p.Y + (double)q.Y) * cross;
            }
        }

        if (Math.Abs(twiceArea) < 1) {
            return Vector2.zero;
        }

        return new Vector2((float)(x / (3 * twiceArea) * ScaleDown), (float)(y / (3 * twiceArea) * ScaleDown));
    }

    public static List<object>? ToRawShape(BoxShapeType shapeType, Paths64 paths, Vector2 offset) {
        if (shapeType == BoxShapeType.PreviewPolygon) {
            return null;
        }

        List<object> rawShape = [shapeType switch {
            BoxShapeType.ConvexHull => "convex",
            BoxShapeType.Polygon or BoxShapeType.OrientedPolygon => "orientedLoop",
            _ => throw new ArgumentOutOfRangeException(nameof(shapeType), shapeType, null),
        }];

        for (var i = 0; i < paths.Count; i++) {
            if (i != 0) {
                rawShape.Add("loop");
            }

            foreach (var point in paths[i]) {
                rawShape.Add(new Vector2(point.X * ScaleDown + offset.x, point.Y * ScaleDown + offset.y));
            }
        }

        return rawShape;
    }

    public const float Scale = 1024f;

    public const float ScaleDown = 1f / Scale;
}

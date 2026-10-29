using System.Collections.Generic;
using UnityEngine;

public static class QuickHull {
    /// <summary>
    /// Computes the convex hull of the given point cloud
    /// </summary>
    /// <param name="points">Point cloud to compute convex hull of.</param>
    /// <returns>The convex hull of the point cloud, in CCW order.</returns>
    public static List<Vector2> Compute(List<Vector2> points) {
        if (points.Count == 0) {
            return new List<Vector2>();
        }

        var minPoint = points[0];
        var maxPoint = points[0];
        foreach (var point in points) {
            if (point.x < minPoint.x) {
                minPoint = point;
            }
            else if (point.x > maxPoint.x) {
                maxPoint = point;
            }
        }

        var ray = new Ray(minPoint, maxPoint);

        var pointsA = new List<Vector3>();
        var pointsB = new List<Vector3>();
        foreach (var point in points) {
            if (point == minPoint || point == maxPoint) {
                continue;
            }

            var dist = ray.Dist(point);
            if (dist >= 0f) {
                pointsA.Add(new Vector3(point.x, point.y, dist));
            }
            else {
                pointsB.Add(new Vector3(point.x, point.y, -dist));
            }
        }

        var result = new List<Vector2>();
        result.Add(minPoint);
        AddPoints(result, pointsA, minPoint, maxPoint, 0, pointsA.Count);
        result.Add(maxPoint);
        AddPoints(result, pointsB, maxPoint, minPoint, 0, pointsB.Count);

        return result;
    }

    private static void AddPoints(List<Vector2> result, List<Vector3> allPoints, Vector2 startPoint, Vector2 endPoint, int startIdx, int endIdx) {
        if (startIdx == endIdx) {
            return;
        }

        var maxIdx = startIdx;
        for (var i = startIdx + 1; i < endIdx; i++) {
            if (allPoints[i].z > allPoints[maxIdx].z) {
                maxIdx = i;
            }
        }

        var midPoint = allPoints[maxIdx];

        allPoints[maxIdx] = allPoints[startIdx++];

        var rayA = new Ray(startPoint, midPoint);
        var rayB = new Ray(midPoint, endPoint);

        var aEnd = startIdx;
        var bStart = endIdx;
        for (var i = startIdx; i < bStart; i++) {
            var point = allPoints[i];
            if (rayA.Dist(point) is var distA and > 0f) {
                allPoints[aEnd++] = new Vector3(point.x, point.y, distA);
            }
            else if (rayB.Dist(point) is var distB and > 0f) {
                var temp = allPoints[--bStart];
                allPoints[bStart] = new Vector3(point.x, point.y, distB);
                allPoints[i--] = temp;
            }
        }

        AddPoints(result, allPoints, startPoint, midPoint, startIdx, aEnd);
        result.Add(midPoint);
        AddPoints(result, allPoints, midPoint, endPoint, bStart, endIdx);
    }

    private struct Ray {
        public Ray(Vector2 start, Vector2 end) {
            var dir = (end - start).normalized;
            _dirX = dir.y;
            _dirY = -dir.x;
            _offset = start.x * _dirX + start.y * _dirY;
        }

        public float Dist(Vector2 point) => point.x * _dirX + point.y * _dirY - _offset;

        public float Dist(Vector3 point) => point.x * _dirX + point.y * _dirY - _offset;

        private float _dirX;

        private float _dirY;

        private float _offset;

    }
}

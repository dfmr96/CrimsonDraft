#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    /// <summary>2D polygon maths for camera zones. Points are (x, z) pairs of the XZ floor plane.</summary>
    public static class CameraZonePolygon
    {
        /// <summary>Even-odd point-in-polygon test; works for concave shapes and either winding.</summary>
        public static bool Contains(IReadOnlyList<Vector2> points, Vector2 point)
        {
            if (points.Count < 3) return false;

            bool inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                var a = points[i];
                var b = points[j];
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Ear-clipping triangulation. Returns triangle corner indices into <paramref name="points"/>
        /// (three per triangle). Falls back to a fan when the outline is degenerate or self-intersecting.</summary>
        public static List<int> Triangulate(IReadOnlyList<Vector2> points)
        {
            var triangles = new List<int>();
            int count = points.Count;
            if (count < 3) return triangles;

            var remaining = new List<int>(count);
            for (int i = 0; i < count; i++) remaining.Add(i);
            float orientation = SignedArea(points) >= 0f ? 1f : -1f;

            int guard = count * count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int prev = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int curr = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];

                    if (Cross(points[prev], points[curr], points[next]) * orientation <= 0f) continue;
                    if (ContainsAnyOther(points, remaining, prev, curr, next)) continue;

                    triangles.Add(prev);
                    triangles.Add(curr);
                    triangles.Add(next);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }

            if (remaining.Count == 3)
            {
                triangles.AddRange(remaining);
            }
            else
            {
                for (int i = 1; i < remaining.Count - 1; i++)
                {
                    triangles.Add(remaining[0]);
                    triangles.Add(remaining[i]);
                    triangles.Add(remaining[i + 1]);
                }
            }
            return triangles;
        }

        private static float SignedArea(IReadOnlyList<Vector2> points)
        {
            float area = 0f;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                area += (points[j].x * points[i].y) - (points[i].x * points[j].y);
            return area * 0.5f;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
            (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);

        /// <summary>Which side of edge a→b the point lies on (sign of (b-a) x (p-a)).</summary>
        private static float Side(Vector2 a, Vector2 b, Vector2 p) =>
            (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        private static bool ContainsAnyOther(IReadOnlyList<Vector2> points, List<int> remaining, int a, int b, int c)
        {
            foreach (int index in remaining)
            {
                if (index == a || index == b || index == c) continue;
                if (InTriangle(points[index], points[a], points[b], points[c])) return true;
            }
            return false;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Side(a, b, p), d2 = Side(b, c, p), d3 = Side(c, a, p);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }
    }
}

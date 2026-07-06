#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Ear-clipping triangulation for simple (non-self-intersecting) polygons.
    /// Accepts either winding; output indices reference the input array.</summary>
    public static class PolygonTriangulator
    {
        public static int[] Triangulate(Vector2[] polygon)
        {
            int n = polygon.Length;
            if (n < 3)
                return System.Array.Empty<int>();

            var indices = new List<int>(n);
            if (SignedArea(polygon) > 0f)
            {
                for (int i = 0; i < n; i++)
                    indices.Add(i);
            }
            else
            {
                for (int i = n - 1; i >= 0; i--)
                    indices.Add(i);
            }

            var result = new List<int>((n - 2) * 3);
            int guard = 0;

            while (indices.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;

                for (int i = 0; i < indices.Count; i++)
                {
                    int i0 = indices[(i - 1 + indices.Count) % indices.Count];
                    int i1 = indices[i];
                    int i2 = indices[(i + 1) % indices.Count];

                    if (!IsEar(polygon, indices, i0, i1, i2))
                        continue;

                    result.Add(i0);
                    result.Add(i1);
                    result.Add(i2);
                    indices.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                    break;
            }

            if (indices.Count == 3)
            {
                result.Add(indices[0]);
                result.Add(indices[1]);
                result.Add(indices[2]);
            }

            return result.ToArray();
        }

        /// <summary>Standard shoelace sum — positive for CCW polygons (y-up).</summary>
        private static float SignedArea(Vector2[] p)
        {
            float area = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                Vector2 a = p[i];
                Vector2 b = p[(i + 1) % p.Length];
                area += a.x * b.y - b.x * a.y;
            }

            return area;
        }

        private static bool IsEar(Vector2[] p, List<int> indices, int i0, int i1, int i2)
        {
            Vector2 a = p[i0];
            Vector2 b = p[i1];
            Vector2 c = p[i2];

            if (Cross(b - a, c - b) <= 0f)
                return false;

            foreach (int idx in indices)
            {
                if (idx == i0 || idx == i1 || idx == i2)
                    continue;

                if (PointInTriangle(p[idx], a, b, c))
                    return false;
            }

            return true;
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        private static bool PointInTriangle(Vector2 pt, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, pt - a);
            float d2 = Cross(c - b, pt - b);
            float d3 = Cross(a - c, pt - c);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }
    }
}

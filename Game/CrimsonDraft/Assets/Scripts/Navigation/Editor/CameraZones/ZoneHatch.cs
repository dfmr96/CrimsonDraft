#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>
    /// 45° stripes clipped to a zone polygon (its own x/z frame), so the Camera Zone Viewer can tell a
    /// priority zone (completion gate still open) apart at a glance. Scanline clipping: each stripe is
    /// cut where it crosses the outline and the inside runs are kept, which also handles concave zones.
    /// </summary>
    public static class ZoneHatch
    {
        public const float DefaultSpacing = 0.35f;

        public static List<(Vector2 a, Vector2 b)> Segments(IReadOnlyList<Vector2> polygon, float spacing = DefaultSpacing)
        {
            var segments = new List<(Vector2, Vector2)>();
            if (polygon == null || polygon.Count < 3 || spacing <= 0f) return segments;

            // Stripes run along (1, 1); each one is the line x - z = c.
            float cMin = float.MaxValue, cMax = float.MinValue;
            foreach (var p in polygon)
            {
                cMin = Mathf.Min(cMin, p.x - p.y);
                cMax = Mathf.Max(cMax, p.x - p.y);
            }

            var crossings = new List<float>(); // positions along the stripe, as z
            for (float c = Mathf.Ceil(cMin / spacing) * spacing; c <= cMax; c += spacing)
            {
                crossings.Clear();
                for (int i = 0; i < polygon.Count; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    float da = a.x - a.y - c, db = b.x - b.y - c;
                    // Half-open on the end vertex so a stripe through a vertex is counted once.
                    if ((da > 0f) == (db > 0f) || da == db) continue;
                    float t = da / (da - db);
                    crossings.Add(Mathf.Lerp(a.y, b.y, t));
                }

                crossings.Sort();
                for (int i = 0; i + 1 < crossings.Count; i += 2)
                {
                    float z0 = crossings[i], z1 = crossings[i + 1];
                    if (z1 - z0 < 1e-4f) continue;
                    segments.Add((new Vector2(c + z0, z0), new Vector2(c + z1, z1)));
                }
            }
            return segments;
        }
    }
}

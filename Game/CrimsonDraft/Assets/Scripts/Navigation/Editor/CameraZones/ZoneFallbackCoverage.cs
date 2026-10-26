#nullable enable

using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>
    /// What the Camera Zone Viewer's "Fallback" view paints red. A zone with a completion gate takes the
    /// camera while its gate is open and stops firing once it completes; where no ordinary (ungated) zone
    /// covers the same floor, nothing would switch back to a normal camera there. Sampled on a grid in each
    /// gated zone's own floor plane; other gated zones never count as cover, as they can switch off too.
    /// </summary>
    public static class ZoneFallbackCoverage
    {
        public const float DefaultCellSize = 0.25f;

        public readonly struct Hole
        {
            /// <summary>Cell centre in the gated zone's frame (x, z).</summary>
            public readonly Vector2   Center;
            /// <summary>The cell's four corners in world space, on the gated zone's floor.</summary>
            public readonly Vector3[] Corners;

            public Hole(Vector2 center, Vector3[] corners)
            {
                this.Center  = center;
                this.Corners = corners;
            }
        }

        public static List<Hole> Holes(IReadOnlyList<FixedCameraZoneTrigger> zones, float cellSize = DefaultCellSize)
        {
            var holes = new List<Hole>();
            foreach (var gated in zones)
            {
                if (gated == null || !gated.HasCompletionGate) continue;
                var polygon = gated.Polygon;
                if (polygon == null || polygon.Count < 3) continue;

                var min = polygon[0];
                var max = polygon[0];
                foreach (var p in polygon) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }

                // Test at the middle of the zone's height range: where a player standing in it would be.
                float sampleHeight = (gated.MinHeight + gated.MaxHeight) * 0.5f;
                float half = cellSize * 0.5f;
                for (float x = min.x + half; x < max.x; x += cellSize)
                for (float z = min.y + half; z < max.y; z += cellSize)
                {
                    var center = new Vector2(x, z);
                    if (!CameraZonePolygon.Contains(polygon, center)) continue;
                    if (CoveredByOrdinaryZone(zones, gated.ToWorld(center, sampleHeight))) continue;

                    holes.Add(new Hole(center, new[]
                    {
                        gated.ToWorld(new Vector2(x - half, z - half), gated.MinHeight),
                        gated.ToWorld(new Vector2(x - half, z + half), gated.MinHeight),
                        gated.ToWorld(new Vector2(x + half, z + half), gated.MinHeight),
                        gated.ToWorld(new Vector2(x + half, z - half), gated.MinHeight),
                    }));
                }
            }
            return holes;
        }

        private static bool CoveredByOrdinaryZone(IReadOnlyList<FixedCameraZoneTrigger> zones, Vector3 world)
        {
            foreach (var zone in zones)
                if (zone != null && !zone.HasCompletionGate && zone.Contains(world)) return true;
            return false;
        }
    }
}

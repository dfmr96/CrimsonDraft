#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Editor;

namespace CrimsonDraft.Tests
{
    /// <summary>Diagonal stripes that mark a priority zone in the Camera Zone Viewer: clipped to the
    /// zone's polygon, concave ones included.</summary>
    public sealed class ZoneHatchTests
    {
        private static readonly Vector2[] Square = { new(0, 0), new(0, 2), new(2, 2), new(2, 0) };

        // L shape: the top-right 1x1 quarter is cut out.
        private static readonly Vector2[] LShape =
            { new(0, 0), new(0, 2), new(1, 2), new(1, 1), new(2, 1), new(2, 0) };

        [Test]
        public void Square_isCoveredByStripesInsideIt()
        {
            var segments = ZoneHatch.Segments(Square, 0.25f);

            Assert.Greater(segments.Count, 5);
            foreach (var (a, b) in segments)
            {
                Assert.IsTrue(InsideOrOnEdge(Square, a) && InsideOrOnEdge(Square, b), $"{a} - {b} leaves the square");
                Assert.IsTrue(CameraZonePolygon.Contains(Square, (a + b) * 0.5f));
            }
        }

        [Test]
        public void ConcaveZone_stripesNeverCrossTheNotch()
        {
            foreach (var (a, b) in ZoneHatch.Segments(LShape, 0.2f))
            {
                var mid = (a + b) * 0.5f;
                Assert.IsTrue(CameraZonePolygon.Contains(LShape, mid), $"{a} - {b} crosses the cut-out corner");
                Assert.IsFalse(mid.x > 1.01f && mid.y > 1.01f);
            }
        }

        [Test]
        public void StripesAreDiagonal()
        {
            foreach (var (a, b) in ZoneHatch.Segments(Square, 0.5f))
            {
                var d = (b - a).normalized;
                Assert.AreEqual(Mathf.Abs(d.x), Mathf.Abs(d.y), 1e-3f);
            }
        }

        [Test]
        public void DegeneratePolygon_hasNoStripes()
        {
            Assert.AreEqual(0, ZoneHatch.Segments(new[] { new Vector2(0, 0), new Vector2(1, 1) }, 0.25f).Count);
        }

        private static bool InsideOrOnEdge(Vector2[] polygon, Vector2 p)
        {
            if (CameraZonePolygon.Contains(polygon, p)) return true;
            for (int i = 0; i < polygon.Length; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Length];
                float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                if (Vector2.Distance(a + (b - a) * t, p) < 1e-3f) return true;
            }
            return false;
        }
    }
}

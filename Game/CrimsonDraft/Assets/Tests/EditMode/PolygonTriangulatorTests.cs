#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class PolygonTriangulatorTests
    {
        [Test]
        public void Triangulate_quad_returnsTwoTriangles()
        {
            var quad = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1),
            };

            var tris = PolygonTriangulator.Triangulate(quad);

            Assert.AreEqual(6, tris.Length);
        }

        [Test]
        public void Triangulate_triangle_returnsItself()
        {
            var tri = new[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
            };

            var tris = PolygonTriangulator.Triangulate(tri);

            Assert.AreEqual(3, tris.Length);
        }

        [Test]
        public void Triangulate_lShape_coversFullArea()
        {
            var l = new[]
            {
                new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 1),
                new Vector2(1, 1), new Vector2(1, 2), new Vector2(0, 2),
            };

            var tris = PolygonTriangulator.Triangulate(l);

            Assert.AreEqual((l.Length - 2) * 3, tris.Length);

            float area = 0f;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector2 a = l[tris[i]];
                Vector2 b = l[tris[i + 1]];
                Vector2 c = l[tris[i + 2]];
                area += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            }

            Assert.AreEqual(3f, area, 0.001f);
        }

        [Test]
        public void Triangulate_degenerateInput_returnsEmpty()
        {
            Assert.IsEmpty(PolygonTriangulator.Triangulate(new[] { new Vector2(0, 0), new Vector2(1, 1) }));
            Assert.IsEmpty(PolygonTriangulator.Triangulate(System.Array.Empty<Vector2>()));
        }
    }
}

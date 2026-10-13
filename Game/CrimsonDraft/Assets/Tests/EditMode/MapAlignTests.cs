#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapAlignTests
    {
        [Test]
        public void SnapOffset_closesGap_toNeighbourEdge()
        {
            var moving = new RectInt(13, 0, 10, 10);
            var other  = new RectInt(0, 0, 10, 10);
            Assert.AreEqual(new Vector2Int(-3, 0), MapAlign.SnapOffset(moving, new[] { other }, 4));
        }

        [Test]
        public void SnapOffset_alignsSameSideEdges()
        {
            var moving = new RectInt(2, 30, 10, 10);
            var other  = new RectInt(0, 0, 10, 10);
            Assert.AreEqual(new Vector2Int(-2, 0), MapAlign.SnapOffset(moving, new[] { other }, 4));
        }

        [Test]
        public void SnapOffset_ignoresEdgesBeyondTolerance()
        {
            var moving = new RectInt(15, 30, 10, 10);
            var other  = new RectInt(0, 0, 10, 10);
            Assert.AreEqual(Vector2Int.zero, MapAlign.SnapOffset(moving, new[] { other }, 4));
        }

        [Test]
        public void SnapOffset_picksNearestEdge_perAxisIndependently()
        {
            var moving = new RectInt(11, 9, 10, 10);
            var a      = new RectInt(0, 0, 10, 10);
            var b      = new RectInt(30, 21, 10, 10);
            Assert.AreEqual(new Vector2Int(-1, 1), MapAlign.SnapOffset(moving, new[] { a, b }, 4));
        }

        [Test]
        public void SnapOffset_alreadyFlush_staysPut()
        {
            Assert.AreEqual(Vector2Int.zero, MapAlign.SnapOffset(new RectInt(10, 0, 5, 5), new[] { new RectInt(0, 0, 10, 10) }, 4));
        }
    }
}

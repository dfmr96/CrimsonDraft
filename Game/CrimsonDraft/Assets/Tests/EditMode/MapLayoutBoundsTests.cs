#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapLayoutBoundsTests
    {
        private static MapRoomVisual Visual(int w, int h, int x, int y, int turns = 0)
            => new("r", MapTestData.Sprite(w, h), new Vector2Int(x, y), turns, false);

        [Test]
        public void Empty_isZeroRect()
        {
            Assert.AreEqual(Rect.zero, MapLayoutBounds.Compute(new MapRoomVisual[0]));
        }

        [Test]
        public void SingleRoom_isCentredOnPosition()
        {
            var bounds = MapLayoutBounds.Compute(new[] { Visual(10, 20, 5, 5) });
            Assert.AreEqual(new Vector2(5, 5), bounds.center);
            Assert.AreEqual(new Vector2(10, 20), bounds.size);
        }

        [Test]
        public void TwoRooms_centreBetweenThem()
        {
            var bounds = MapLayoutBounds.Compute(new[] { Visual(10, 10, 0, 0), Visual(10, 10, 20, 0) });
            Assert.AreEqual(new Vector2(10, 0), bounds.center);
            Assert.AreEqual(new Vector2(30, 10), bounds.size);
        }

        [Test]
        public void OddQuarterTurns_swapWidthAndHeight()
        {
            Assert.AreEqual(new Vector2(20, 10), MapLayoutBounds.Compute(new[] { Visual(10, 20, 0, 0, turns: 1) }).size);
            Assert.AreEqual(new Vector2(10, 20), MapLayoutBounds.Compute(new[] { Visual(10, 20, 0, 0, turns: 2) }).size);
        }
    }
}

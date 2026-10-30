#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Pushables;

namespace CrimsonDraft.Tests
{
    // PushDetector: RE2 push activation (touch, same floor, square to a face, moving into it).
    // Box: 1 x 1 x 1 m resting on the floor at the origin. Player: radius 0.5 m, pivot 1 m above
    // its feet, standing just outside the south (-Z) face unless stated otherwise.
    public sealed class PushableObjectTests
    {
        private const float Radius       = 0.5f;
        private const float Skin         = 0.05f;
        private const float CornerMargin = PushDetector.DefaultCornerMargin;

        private static readonly Bounds Box = new(new Vector3(0f, 0.5f, 0f), Vector3.one);

        private const float MinAlignment = PushDetector.DefaultMinAlignment;

        private static Vector3 SouthOfBox(float x = 0f) => new(x, 1f, -1.04f);

        private static bool Resolve(Vector3 player, Vector3 move, out Vector3 axis) =>
            PushDetector.TryResolveAxis(player, move, Box, CornerMargin, MinAlignment, out axis);

        [Test]
        public void IsTouching_playerAgainstFace_true()
        {
            Assert.IsTrue(PushDetector.IsTouching(SouthOfBox(), Radius, Box, Skin));
        }

        [Test]
        public void IsTouching_playerAFewCentimetresBeyondSkin_false()
        {
            Assert.IsFalse(PushDetector.IsTouching(new Vector3(0f, 1f, -1.2f), Radius, Box, Skin));
        }

        [Test]
        public void IsSameFloor_feetAtBoxBase_true_feetOneFloorUp_false()
        {
            Assert.IsTrue(PushDetector.IsSameFloor(0.02f, Box, 0.5f));
            Assert.IsFalse(PushDetector.IsSameFloor(1.8f, Box, 0.5f));
        }

        [Test]
        public void TryResolveAxis_southOfBoxMovingNorth_pushesNorth()
        {
            Assert.IsTrue(Resolve(SouthOfBox(), Vector3.forward, out var axis));
            Assert.AreEqual(Vector3.forward, axis);
        }

        [Test]
        public void TryResolveAxis_eastOfBoxMovingWest_pushesWest()
        {
            Assert.IsTrue(Resolve(new Vector3(1.04f, 1f, 0f), Vector3.left, out var axis));
            Assert.AreEqual(Vector3.left, axis);
        }

        [Test]
        public void TryResolveAxis_movingAlongTheFace_doesNotPush()
        {
            Assert.IsFalse(Resolve(SouthOfBox(), Vector3.right, out _));
        }

        [Test]
        public void TryResolveAxis_movingAway_doesNotPush()
        {
            Assert.IsFalse(Resolve(SouthOfBox(), Vector3.back, out _));
        }

        [Test]
        public void TryResolveAxis_atCorner_doesNotPush()
        {
            // 0.48 m off-centre on a 0.5 m half-width face is inside the 0.05 m corner margin.
            Assert.IsFalse(Resolve(SouthOfBox(x: 0.48f), Vector3.forward, out _));
        }

        [Test]
        public void TryResolveAxis_offCentreButSquareToFace_pushes()
        {
            Assert.IsTrue(Resolve(SouthOfBox(x: 0.3f), Vector3.forward, out var axis));
            Assert.AreEqual(Vector3.forward, axis);
        }

        [Test]
        public void TryResolveAxis_movingInAt30Degrees_pushesAlongFaceAxis_notDiagonal()
        {
            var move = new Vector3(Mathf.Sin(30f * Mathf.Deg2Rad), 0f, Mathf.Cos(30f * Mathf.Deg2Rad));

            Assert.IsTrue(Resolve(SouthOfBox(), move, out var axis));
            Assert.AreEqual(Vector3.forward, axis);
        }

        [Test]
        public void TryResolveAxis_movingInAt60Degrees_doesNotPush()
        {
            var move = new Vector3(Mathf.Sin(60f * Mathf.Deg2Rad), 0f, Mathf.Cos(60f * Mathf.Deg2Rad));

            Assert.IsFalse(Resolve(SouthOfBox(), move, out _));
        }

        [Test]
        public void TryResolveAxis_noInput_doesNotPush()
        {
            Assert.IsFalse(Resolve(SouthOfBox(), Vector3.zero, out _));
        }

        [Test]
        public void TryResolveAxis_tallNarrowShelf_usesItsOwnBounds()
        {
            // 0.4 m deep shelf: a player square to its long (-X) face pushes it east.
            var shelf = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(0.4f, 2f, 1f));

            Assert.IsTrue(PushDetector.TryResolveAxis(new Vector3(-0.74f, 1f, 0.1f), Vector3.right, shelf, CornerMargin, MinAlignment, out var axis));
            Assert.AreEqual(Vector3.right, axis);
        }

        [Test]
        public void TryResolveAxis_movingInAt45Degrees_pushes_regardlessOfGapToBox()
        {
            // Regression: the old probe depended on the player-box gap, so ~45 degree input flickered.
            var move = new Vector3(Mathf.Sin(45f * Mathf.Deg2Rad), 0f, Mathf.Cos(45f * Mathf.Deg2Rad));

            Assert.IsTrue(Resolve(new Vector3(0f, 1f, -1.00f), move, out _));
            Assert.IsTrue(Resolve(new Vector3(0f, 1f, -1.04f), move, out _));
        }
    }
}

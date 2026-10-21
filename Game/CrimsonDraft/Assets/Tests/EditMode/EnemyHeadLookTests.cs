#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyHeadLookTests
    {
        [Test]
        public void TargetYaw_playerAhead_isZero()
            => Assert.AreEqual(0f, EnemyHeadLook.TargetYaw(Vector3.forward, new Vector3(0f, 0f, 5f), 70f), 1e-4f);

        [Test]
        public void TargetYaw_playerToTheRight_isPositive()
            => Assert.AreEqual(45f, EnemyHeadLook.TargetYaw(Vector3.forward, new Vector3(1f, 0f, 1f), 70f), 1e-3f);

        [Test]
        public void TargetYaw_playerBehind_isClampedToTheLimit()
            => Assert.AreEqual(70f, Mathf.Abs(EnemyHeadLook.TargetYaw(Vector3.forward, new Vector3(0.1f, 0f, -5f), 70f)), 1e-4f);

        [Test]
        public void TurnSign_swingsAFloorFacingFaceTowardTheRight()
        {
            // Body lying along +Z, neck axis pointing forward, face down; right is +X.
            var sign = EnemyHeadLook.TurnSign(Vector3.forward, Vector3.down, Vector3.right);
            var turned = Quaternion.AngleAxis(60f * sign, Vector3.forward) * Vector3.down;
            Assert.Greater(turned.x, 0.5f);
        }

        [Test]
        public void TargetYaw_ignoresHeight()
            => Assert.AreEqual(0f, EnemyHeadLook.TargetYaw(Vector3.forward, new Vector3(0f, 10f, 2f), 70f), 1e-4f);
    }
}

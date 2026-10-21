#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyTrapGrabZoneTests
    {
        private const float Range = 1.2f;
        private const float Cone  = 70f;

        [Test]
        public void InFrontAndClose_grabs()
            => Assert.IsTrue(EnemyNavAgent.IsInTrapGrabZone(Vector3.forward, new Vector3(0.2f, 0f, 1f), Range, Cone));

        [Test]
        public void Behind_neverGrabs()
            => Assert.IsFalse(EnemyNavAgent.IsInTrapGrabZone(Vector3.forward, new Vector3(0f, 0f, -0.5f), Range, Cone));

        [Test]
        public void BesideOutsideTheCone_doesNotGrab()
            => Assert.IsFalse(EnemyNavAgent.IsInTrapGrabZone(Vector3.forward, new Vector3(1f, 0f, 0.3f), Range, Cone));

        [Test]
        public void InFrontButOutOfRange_doesNotGrab()
            => Assert.IsFalse(EnemyNavAgent.IsInTrapGrabZone(Vector3.forward, new Vector3(0f, 0f, 2f), Range, Cone));

        [Test]
        public void IgnoresHeight()
            => Assert.IsTrue(EnemyNavAgent.IsInTrapGrabZone(Vector3.forward, new Vector3(0f, 3f, 1f), Range, Cone));
    }
}

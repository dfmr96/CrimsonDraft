#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class NavigationDebugKeysTests
    {
        [Test]
        public void PickKillTarget_lastAliveOperator()
        {
            var roster = new FakeRoster(Alive(0), Alive(1), Alive(2));
            Assert.AreEqual(2, NavigationDebugKeys.PickKillTarget(roster));
        }

        [Test]
        public void PickKillTarget_skipsDeadOperators()
        {
            var roster = new FakeRoster(Alive(0), Alive(1), Dead(2));
            Assert.AreEqual(1, NavigationDebugKeys.PickKillTarget(roster));
        }

        [Test]
        public void PickKillTarget_neverTheLastSurvivor()
        {
            var roster = new FakeRoster(Alive(0), Dead(1), Dead(2));
            Assert.IsNull(NavigationDebugKeys.PickKillTarget(roster));
        }

        [Test]
        public void PickNearest_closestCandidate()
        {
            var candidates = new[] { new Vector3(5f, 0f, 0f), new Vector3(1f, 0f, 1f), new Vector3(-3f, 0f, 0f) };
            Assert.AreEqual(1, NavigationDebugKeys.PickNearest(Vector3.zero, candidates));
        }

        [Test]
        public void PickNearest_noCandidates_isNull()
            => Assert.IsNull(NavigationDebugKeys.PickNearest(Vector3.zero, new Vector3[0]));
    }
}

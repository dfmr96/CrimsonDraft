#nullable enable

using NUnit.Framework;
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
    }
}

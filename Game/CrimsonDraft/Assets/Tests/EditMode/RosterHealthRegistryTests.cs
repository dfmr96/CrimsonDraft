#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class RosterHealthRegistryTests
    {
        [Test]
        public void HasSavedState_initially_isFalse()
        {
            var registry = new RosterHealthRegistry();
            Assert.IsFalse(registry.HasSavedState);
        }

        [Test]
        public void Load_initially_returnsNull()
        {
            var registry = new RosterHealthRegistry();
            Assert.IsNull(registry.Load());
        }

        [Test]
        public void Save_setsHasSavedState_toTrue()
        {
            var registry = new RosterHealthRegistry();
            registry.Save(new[] { 100, 80, 0 });

            Assert.IsTrue(registry.HasSavedState);
        }

        [Test]
        public void Load_afterSave_returnsSameArrayReference()
        {
            var registry = new RosterHealthRegistry();
            var hp       = new[] { 100, 80, 0 };
            registry.Save(hp);

            Assert.AreSame(hp, registry.Load());
        }

        [Test]
        public void Save_calledTwice_overwritesPreviousSnapshot()
        {
            var registry = new RosterHealthRegistry();
            registry.Save(new[] { 100, 100 });
            var second = new[] { 50, 25 };
            registry.Save(second);

            Assert.AreSame(second, registry.Load());
        }
    }
}

#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class RosterHealthRegistryTests
    {
        [Test]
        public void Save_thenLoad_returnsSavedArray()
        {
            var registry = new RosterHealthRegistry();
            registry.Save(new[] { 50, 80 });

            CollectionAssert.AreEqual(new[] { 50, 80 }, registry.Load());
        }

        [Test]
        public void ClearAll_removesSavedState()
        {
            var registry = new RosterHealthRegistry();
            registry.Save(new[] { 50 });
            registry.ClearAll();

            Assert.IsFalse(registry.HasSavedState);
            Assert.IsNull(registry.Load());
        }
    }
}

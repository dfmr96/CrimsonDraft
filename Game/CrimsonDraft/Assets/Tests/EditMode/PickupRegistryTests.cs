#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class PickupRegistryTests
    {
        [Test]
        public void LoadState_marksGivenIdsAsCollected()
        {
            var registry = new PickupRegistry();
            registry.LoadState(new List<string> { "a", "b" });

            Assert.IsTrue(registry.IsCollected("a"));
            Assert.IsTrue(registry.IsCollected("b"));
            Assert.IsFalse(registry.IsCollected("c"));
        }

        [Test]
        public void LoadState_replacesPreviousState()
        {
            var registry = new PickupRegistry();
            registry.SetCollected("old");
            registry.LoadState(new List<string> { "new" });

            Assert.IsFalse(registry.IsCollected("old"));
            Assert.IsTrue(registry.IsCollected("new"));
        }

        [Test]
        public void ClearAll_removesAllCollectedIds()
        {
            var registry = new PickupRegistry();
            registry.SetCollected("a");
            registry.ClearAll();

            Assert.IsFalse(registry.IsCollected("a"));
            Assert.AreEqual(0, registry.CollectedIds.Count);
        }
    }
}

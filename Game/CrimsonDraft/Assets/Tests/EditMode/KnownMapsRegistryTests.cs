#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class KnownMapsRegistryTests
    {
        [Test]
        public void IsKnown_whenNeverSet_returnsFalse()
        {
            var registry = new KnownMapsRegistry();
            Assert.IsFalse(registry.IsKnown("deck-a"));
        }

        [Test]
        public void MarkKnown_setsKnown()
        {
            var registry = new KnownMapsRegistry();
            registry.MarkKnown("deck-a");
            Assert.IsTrue(registry.IsKnown("deck-a"));
        }

        [Test]
        public void LoadState_restoresGivenState()
        {
            var registry = new KnownMapsRegistry();
            registry.LoadState(new HashSet<string> { "deck-a" });
            Assert.IsTrue(registry.IsKnown("deck-a"));
        }

        [Test]
        public void GetState_reflectsMarkedMaps()
        {
            var registry = new KnownMapsRegistry();
            registry.MarkKnown("deck-a");
            Assert.IsTrue(registry.GetState().Contains("deck-a"));
        }

        [Test]
        public void ClearAll_removesAllKnownMaps()
        {
            var registry = new KnownMapsRegistry();
            registry.MarkKnown("map-a");
            registry.ClearAll();

            Assert.IsFalse(registry.IsKnown("map-a"));
        }
    }
}

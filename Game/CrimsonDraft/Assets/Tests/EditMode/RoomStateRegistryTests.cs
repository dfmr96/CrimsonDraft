#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class RoomStateRegistryTests
    {
        [Test]
        public void GetState_whenNeverSet_returnsUnknown()
        {
            var registry = new RoomStateRegistry();
            Assert.AreEqual(RoomMapState.Unknown, registry.GetState("room-a"));
        }

        [Test]
        public void MarkVisited_fromUnknown_setsVisited()
        {
            var registry = new RoomStateRegistry();
            registry.MarkVisited("room-a");
            Assert.AreEqual(RoomMapState.Visited, registry.GetState("room-a"));
        }

        [Test]
        public void MarkVisited_isMonotonic()
        {
            var registry = new RoomStateRegistry();
            registry.MarkVisited("room-a");
            registry.MarkVisited("room-a");
            Assert.AreEqual(RoomMapState.Visited, registry.GetState("room-a"));
        }

        [Test]
        public void LoadState_restoresGivenState()
        {
            var registry = new RoomStateRegistry();
            registry.LoadState(new Dictionary<string, RoomMapState>
            {
                ["room-a"] = RoomMapState.Visited,
            });

            Assert.AreEqual(RoomMapState.Visited, registry.GetState("room-a"));
        }
    }
}

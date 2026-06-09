#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class DoorStateRegistryTests
    {
        [Test]
        public void IsUnlocked_whenNeverSet_returnsFalse()
        {
            var registry = new DoorStateRegistry();
            Assert.IsFalse(registry.IsUnlocked("any-door"));
        }

        [Test]
        public void SetUnlocked_thenIsUnlocked_returnsTrue()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            Assert.IsTrue(registry.IsUnlocked("door-a"));
        }

        [Test]
        public void SetUnlocked_doesNotAffectOtherDoors()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            Assert.IsFalse(registry.IsUnlocked("door-b"));
        }

        [Test]
        public void LoadState_restoresGivenState()
        {
            var registry = new DoorStateRegistry();
            registry.LoadState(new Dictionary<string, bool> { ["door-x"] = true });
            Assert.IsTrue(registry.IsUnlocked("door-x"));
            Assert.IsFalse(registry.IsUnlocked("door-y"));
        }

        [Test]
        public void GetState_reflectsSetUnlockedCalls()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            Assert.IsTrue(registry.GetState().ContainsKey("door-a"));
        }
    }
}

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
        public void GetMapState_whenNeverSet_returnsUnknown()
        {
            var registry = new DoorStateRegistry();
            Assert.AreEqual(DoorMapState.Unknown, registry.GetMapState("door-a"));
        }

        [Test]
        public void MarkLocked_fromUnknown_setsLocked()
        {
            var registry = new DoorStateRegistry();
            registry.MarkLocked("door-a");
            Assert.AreEqual(DoorMapState.Locked, registry.GetMapState("door-a"));
            Assert.IsFalse(registry.IsUnlocked("door-a"));
        }

        [Test]
        public void MarkLocked_afterUnlocked_doesNotDowngrade()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            registry.MarkLocked("door-a");
            Assert.AreEqual(DoorMapState.Unlocked, registry.GetMapState("door-a"));
            Assert.IsTrue(registry.IsUnlocked("door-a"));
        }

        [Test]
        public void SetUnlocked_afterLocked_upgradesToUnlocked()
        {
            var registry = new DoorStateRegistry();
            registry.MarkLocked("door-a");
            registry.SetUnlocked("door-a");
            Assert.AreEqual(DoorMapState.Unlocked, registry.GetMapState("door-a"));
        }

        [Test]
        public void LoadState_restoresGivenState()
        {
            var registry = new DoorStateRegistry();
            registry.LoadState(new Dictionary<string, DoorMapState>
            {
                ["door-x"] = DoorMapState.Unlocked,
                ["door-y"] = DoorMapState.Locked,
            });

            Assert.IsTrue(registry.IsUnlocked("door-x"));
            Assert.IsFalse(registry.IsUnlocked("door-y"));
            Assert.AreEqual(DoorMapState.Locked, registry.GetMapState("door-y"));
        }

        [Test]
        public void GetState_reflectsSetUnlockedCalls()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            Assert.AreEqual(DoorMapState.Unlocked, registry.GetState()["door-a"]);
        }

        [Test]
        public void ClearAll_removesAllDoorState()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-a");
            registry.ClearAll();

            Assert.AreEqual(DoorMapState.Unknown, registry.GetMapState("door-a"));
        }
    }
}

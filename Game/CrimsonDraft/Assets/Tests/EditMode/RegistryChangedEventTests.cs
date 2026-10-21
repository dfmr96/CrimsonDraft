#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class RegistryChangedEventTests
    {
        [Test]
        public void Pickup_SetCollected_raisesOnce()
        {
            var registry = new PickupRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.SetCollected("p1");
            registry.SetCollected("p1");

            Assert.AreEqual(1, raised);
        }

        [Test]
        public void Note_SetCollected_raisesOnce()
        {
            var registry = new NoteRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.SetCollected("n1");
            registry.SetCollected("n1");

            Assert.AreEqual(1, raised);
        }

        [Test]
        public void Door_MarkUnlocked_raisesOnlyWhenStateChanges()
        {
            var registry = new DoorStateRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.MarkLocked("d1");    // Unknown -> Locked
            registry.MarkUnlocked("d1");  // Locked -> Unlocked
            registry.MarkUnlocked("d1");  // no change
            registry.MarkLocked("d1");    // ignored: unlocked doors stay unlocked

            Assert.AreEqual(2, raised);
        }

        [Test]
        public void Socket_SetInserted_raises()
        {
            var registry = new ItemSocketStateRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.SetInserted("s1", new[] { true, false });

            Assert.AreEqual(1, raised);
        }

        [Test]
        public void LoadStateAndClearAll_neverRaise()
        {
            var pickups = new PickupRegistry();
            var notes   = new NoteRegistry();
            var doors   = new DoorStateRegistry();
            var sockets = new ItemSocketStateRegistry();
            int raised = 0;
            pickups.Changed += () => raised++;
            notes.Changed   += () => raised++;
            doors.Changed   += () => raised++;
            sockets.Changed += () => raised++;

            pickups.LoadState(new[] { "p1" });
            notes.LoadState(new[] { "n1" });
            doors.LoadState(new Dictionary<string, DoorMapState> { ["d1"] = DoorMapState.Unlocked });
            sockets.LoadState(new Dictionary<string, bool[]> { ["s1"] = new[] { true } });
            pickups.ClearAll(); notes.ClearAll(); doors.ClearAll(); sockets.ClearAll();

            Assert.AreEqual(0, raised);
        }
    }
}

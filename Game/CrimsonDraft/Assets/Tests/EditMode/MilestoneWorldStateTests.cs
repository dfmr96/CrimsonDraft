#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneWorldStateTests
    {
        private PickupRegistry pickups = null!;
        private NoteRegistry notes = null!;
        private ItemSocketStateRegistry sockets = null!;
        private DoorStateRegistry doors = null!;
        private PuzzleStateRegistry puzzles = null!;
        private FakeInventoryService inventory = null!;
        private MilestoneWorldState state = null!;
        private readonly List<MilestoneSource> raised = new();

        [SetUp]
        public void SetUp()
        {
            this.pickups = new PickupRegistry(); this.notes = new NoteRegistry(); this.sockets = new ItemSocketStateRegistry();
            this.doors = new DoorStateRegistry(); this.puzzles = new PuzzleStateRegistry(); this.inventory = new FakeInventoryService();
            this.state = new MilestoneWorldState(this.pickups, this.notes, this.sockets, this.doors, this.puzzles, this.inventory);
            this.raised.Clear();
            this.state.SourceChanged += s => this.raised.Add(s);
        }

        // Review Focus 3: empty / partial sockets are not complete.
        [Test]
        public void IsSocketComplete_requiresEverySlot()
        {
            Assert.IsFalse(this.state.IsSocketComplete("s"));
            this.sockets.SetInserted("s", new bool[0]);
            Assert.IsFalse(this.state.IsSocketComplete("s"));
            this.sockets.SetInserted("s", new[] { true, false });
            Assert.IsFalse(this.state.IsSocketComplete("s"));
            this.sockets.SetInserted("s", new[] { true, true });
            Assert.IsTrue(this.state.IsSocketComplete("s"));
        }

        [Test]
        public void EverySource_forwardsItsChange()
        {
            this.pickups.SetCollected("p");
            this.inventory.RaiseContentsChanged();
            this.sockets.SetInserted("s", new[] { true });
            this.doors.MarkUnlocked("d");
            this.notes.SetCollected("n");
            this.puzzles.SetSolved("z");

            CollectionAssert.AreEqual(new[]
            {
                MilestoneSource.Pickups, MilestoneSource.Inventory, MilestoneSource.Sockets,
                MilestoneSource.Doors, MilestoneSource.Notes, MilestoneSource.Puzzles,
            }, this.raised);
        }

        [Test]
        public void Dispose_stopsForwarding()
        {
            this.state.Dispose();
            this.pickups.SetCollected("p");
            Assert.AreEqual(0, this.raised.Count);
        }
    }
}

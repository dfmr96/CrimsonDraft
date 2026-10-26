#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Gates;
using CrimsonDraft.Navigation.Interactables;
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
        private RadioCallRegistry radios = null!;
        private GateStateService gates = null!;
        private FakeInventoryService inventory = null!;
        private MilestoneWorldState state = null!;
        private readonly List<MilestoneSource> raised = new();

        [SetUp]
        public void SetUp()
        {
            this.pickups = new PickupRegistry(); this.notes = new NoteRegistry(); this.sockets = new ItemSocketStateRegistry();
            this.doors = new DoorStateRegistry(); this.puzzles = new PuzzleStateRegistry(); this.radios = new RadioCallRegistry();
            this.inventory = new FakeInventoryService();
            this.gates = new GateStateService(this.sockets, this.puzzles, this.radios, new GateRegistry());
            this.state = new MilestoneWorldState(this.pickups, this.notes, this.doors, this.gates, this.inventory);
            this.raised.Clear();
            this.state.SourceChanged += s => this.raised.Add(s);
        }

        [TearDown]
        public void TearDown()
        {
            this.state.Dispose();
            this.gates.Dispose();
        }

        [Test]
        public void IsGateCompleted_asksTheGates()
        {
            Assert.IsFalse(this.state.IsGateCompleted(GateIds.Radio("radio_01")));
            this.radios.SetHeard("radio_01");
            Assert.IsTrue(this.state.IsGateCompleted(GateIds.Radio("radio_01")));

            // A socket counts once it records completion, not just when its slots are filled.
            this.sockets.SetInserted("fuse", new[] { true, true });
            Assert.IsFalse(this.state.IsGateCompleted(GateIds.Socket("fuse")));
            this.sockets.SetInserted(ItemSocketInteractable.CompletedKey("fuse"), new[] { true });
            Assert.IsTrue(this.state.IsGateCompleted(GateIds.Socket("fuse")));
        }

        [Test]
        public void EverySource_forwardsItsChange()
        {
            this.pickups.SetCollected("p");
            this.inventory.RaiseContentsChanged();
            this.doors.MarkUnlocked("d");
            this.notes.SetCollected("n");
            this.sockets.SetInserted("s", new[] { true });
            this.puzzles.SetSolved("z");
            this.radios.SetHeard("r");

            CollectionAssert.AreEqual(new[]
            {
                MilestoneSource.Pickups, MilestoneSource.Inventory, MilestoneSource.Doors, MilestoneSource.Notes,
                MilestoneSource.Gates, MilestoneSource.Gates, MilestoneSource.Gates,
            }, this.raised);
        }

        [Test]
        public void Dispose_stopsForwarding()
        {
            this.state.Dispose();
            this.pickups.SetCollected("p");
            this.radios.SetHeard("r");
            Assert.AreEqual(0, this.raised.Count);
        }
    }
}

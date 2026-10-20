#nullable enable

using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Milestones;
using FakeWorld = CrimsonDraft.Tests.MilestoneTriggerEvaluatorTests.FakeWorld;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneTrackerTests
    {
        private MilestoneGraph graph = null!;
        private FakeWorld world = null!;
        private MilestoneRegistry registry = null!;
        private MilestoneNode pickupNode = null!;
        private MilestoneNode doorNode = null!;

        [SetUp]
        public void SetUp()
        {
            this.graph = ScriptableObject.CreateInstance<MilestoneGraph>();
            this.pickupNode = this.graph.AddNode("Take Sheep Key", Vector2.zero);
            this.pickupNode.trigger = new MilestoneTrigger { kind = MilestoneTriggerKind.PickupCollected, targetId = "sheep_key" };
            this.doorNode = this.graph.AddNode("Unlock Lavatory", Vector2.one);
            this.doorNode.trigger = new MilestoneTrigger { kind = MilestoneTriggerKind.DoorUnlocked, targetId = "lavatory" };
            this.graph.AddNode("Unassigned", Vector2.right); // trigger None
            this.world = new FakeWorld();
            this.registry = new MilestoneRegistry();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(this.graph);

        private MilestoneTracker Build()
        {
            var tracker = new MilestoneTracker(this.world, this.registry, new[] { this.graph });
            ((IInitializable)tracker).Initialize();
            return tracker;
        }

        // Review Focus 2: condition already true at load -> completed in Initialize, once.
        [Test]
        public void Initialize_completesWhatAlreadyHolds_once()
        {
            this.world.Pickups.Add("sheep_key");
            int raised = 0;
            this.registry.Completed += _ => raised++;

            Build();

            Assert.IsTrue(this.registry.IsCompleted(this.pickupNode.id));
            Assert.IsFalse(this.registry.IsCompleted(this.doorNode.id));
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void SourceChanged_completesMatchingNode()
        {
            Build();
            this.world.Doors.Add("lavatory");
            this.world.Raise(MilestoneSource.Doors);
            Assert.IsTrue(this.registry.IsCompleted(this.doorNode.id));
        }

        [Test]
        public void OtherSourceChanged_doesNotEvaluateUnrelatedKinds()
        {
            Build();
            this.world.Doors.Add("lavatory");
            this.world.Raise(MilestoneSource.Pickups);
            Assert.IsFalse(this.registry.IsCompleted(this.doorNode.id));
        }

        [Test]
        public void Completed_neverUncompletes()
        {
            this.world.Pickups.Add("sheep_key");
            Build();
            this.world.Pickups.Remove("sheep_key");
            this.world.Raise(MilestoneSource.Pickups);
            Assert.IsTrue(this.registry.IsCompleted(this.pickupNode.id));
        }

        [Test]
        public void Dispose_unsubscribes()
        {
            var tracker = Build();
            ((System.IDisposable)tracker).Dispose();
            this.world.Doors.Add("lavatory");
            this.world.Raise(MilestoneSource.Doors);
            Assert.IsFalse(this.registry.IsCompleted(this.doorNode.id));
        }
    }
}

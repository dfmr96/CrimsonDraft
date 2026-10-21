#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Tests
{
    // "Available" = not completed, and every incoming edge comes from a completed milestone.
    public sealed class MilestoneAvailabilityTests
    {
        private MilestoneGraph graph = null!;
        private MilestoneRegistry registry = null!;

        [SetUp]
        public void SetUp()
        {
            this.graph    = ScriptableObject.CreateInstance<MilestoneGraph>();
            this.registry = new MilestoneRegistry();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(this.graph);

        private MilestoneNode Node(string title) => this.graph.AddNode(title, Vector2.zero);

        private void Edge(MilestoneNode from, MilestoneNode to) =>
            this.graph.Edges.Add(new MilestoneEdge { fromNodeId = from.id, toNodeId = to.id });

        private string[] Titles() =>
            MilestoneAvailability.AvailableTitles(new[] { this.graph }, this.registry).ToArray();

        [Test]
        public void NodeWithoutIncomingEdges_isAvailableFromTheStart()
        {
            var key = Node("Take Sheep Key");
            var use = Node("Use Sheep Key");
            Edge(key, use);

            CollectionAssert.AreEqual(new[] { "Take Sheep Key" }, Titles());
        }

        [Test]
        public void CompletingPredecessor_makesSuccessorAvailable_andHidesItself()
        {
            var key = Node("Take Sheep Key");
            var use = Node("Use Sheep Key");
            Edge(key, use);

            this.registry.Complete(key.id);

            CollectionAssert.AreEqual(new[] { "Use Sheep Key" }, Titles());
        }

        [Test]
        public void SeveralIncomingEdges_needEveryPredecessorCompleted()
        {
            var dog   = Node("Dog Emblem");
            var horse = Node("Horse Emblem");
            var place = Node("Place Emblems in Pig Portrait");
            Edge(dog, place);
            Edge(horse, place);

            this.registry.Complete(dog.id);
            CollectionAssert.DoesNotContain(Titles(), "Place Emblems in Pig Portrait");

            this.registry.Complete(horse.id);
            CollectionAssert.Contains(Titles(), "Place Emblems in Pig Portrait");
        }

        [Test]
        public void EdgeFromMissingNode_doesNotBlock()
        {
            var use = Node("Use Sheep Key");
            this.graph.Edges.Add(new MilestoneEdge { fromNodeId = "deleted", toNodeId = use.id });

            CollectionAssert.AreEqual(new[] { "Use Sheep Key" }, Titles());
        }

        [Test]
        public void EverythingCompleted_listsNothing()
        {
            var key = Node("Take Sheep Key");
            this.registry.Complete(key.id);

            Assert.IsEmpty(Titles());
        }

        [Test]
        public void SeveralGraphs_areListedTogether_inGraphOrder()
        {
            var other = ScriptableObject.CreateInstance<MilestoneGraph>();
            try
            {
                Node("Deck B start");
                other.AddNode("Deck C start", Vector2.zero);

                var titles = MilestoneAvailability.AvailableTitles(new[] { this.graph, null!, other }, this.registry).ToArray();

                CollectionAssert.AreEqual(new[] { "Deck B start", "Deck C start" }, titles);
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void Overlay_firstToggleShows_secondHides()
        {
            Node("Take Sheep Key");
            var overlay = new MilestoneDebugOverlay(this.registry, new[] { this.graph });
            try
            {
                overlay.Toggle();
                Assert.IsTrue(overlay.IsVisible);
                Assert.AreEqual("Take Sheep Key", overlay.Text);

                overlay.Toggle();
                Assert.IsFalse(overlay.IsVisible);
            }
            finally { ((System.IDisposable)overlay).Dispose(); }
        }

        [Test]
        public void OverlayText_oneTitlePerLine_orNoneWhenEmpty()
        {
            Assert.AreEqual("Take Handle\nEmpty Flask", MilestoneAvailability.FormatList(new[] { "Take Handle", "Empty Flask" }));
            Assert.AreEqual("(none)", MilestoneAvailability.FormatList(System.Array.Empty<string>()));
        }
    }
}

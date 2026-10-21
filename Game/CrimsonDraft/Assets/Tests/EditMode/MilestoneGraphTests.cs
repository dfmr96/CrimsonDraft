#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneGraphTests
    {
        private MilestoneGraph graph = null!;

        [SetUp]    public void SetUp()    => this.graph = ScriptableObject.CreateInstance<MilestoneGraph>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(this.graph);

        [Test]
        public void AddNode_assignsUniqueStableId()
        {
            var a = this.graph.AddNode("Take Sheep Key", Vector2.zero);
            var b = this.graph.AddNode("Use Sheep Key", Vector2.one);

            Assert.IsFalse(string.IsNullOrEmpty(a.id));
            Assert.AreNotEqual(a.id, b.id);

            string before = a.id;
            a.title = "Take Sheep Key in Save Room";
            Assert.AreEqual(before, this.graph.FindNode(before)!.id);
        }

        // Review Focus 5: deleting a node leaves no dangling ids behind.
        [Test]
        public void RemoveNode_dropsItsEdgesAndFrameMembership()
        {
            var a = this.graph.AddNode("A", Vector2.zero);
            var b = this.graph.AddNode("B", Vector2.one);
            this.graph.Edges.Add(new MilestoneEdge { fromNodeId = a.id, toNodeId = b.id });
            this.graph.Frames.Add(new MilestoneFrame { id = "f", nodeIds = { a.id, b.id } });

            Assert.IsTrue(this.graph.RemoveNode(a.id));

            Assert.IsNull(this.graph.FindNode(a.id));
            Assert.AreEqual(0, this.graph.Edges.Count);
            CollectionAssert.AreEqual(new[] { b.id }, this.graph.Frames[0].nodeIds);
        }

        [Test]
        public void MilestoneRef_isValidOnlyWithGraphAndId()
        {
            var node = this.graph.AddNode("A", Vector2.zero);
            Assert.IsTrue(new MilestoneRef(this.graph, node.id).IsValid);
            Assert.IsFalse(new MilestoneRef(null, node.id).IsValid);
            Assert.IsFalse(new MilestoneRef(this.graph, "").IsValid);
        }
    }
}

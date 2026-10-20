#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Editor.Milestones;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Tests
{
    // Editor-window behaviour found by the final review: Undo must capture the state before each
    // edit (I2), opening other assets must not be hijacked (I3), focusing the window must not
    // rebuild it (I4).
    public sealed class MilestoneGraphEditorTests
    {
        private MilestoneGraph graph = null!;
        private MilestoneNode node = null!;
        private MilestoneGraphView view = null!;

        [SetUp]
        public void SetUp()
        {
            this.graph = ScriptableObject.CreateInstance<MilestoneGraph>();
            this.graph.Lanes.Add(new MilestoneLane { id = "cyan", name = "Cyan", color = Color.cyan });
            this.node = this.graph.AddNode("Take Sheep Key", new Vector2(10f, 20f));
            this.view = new MilestoneGraphView(this.graph);
            this.view.Load();
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            Object.DestroyImmediate(this.graph);
            if (EditorWindow.HasOpenInstances<MilestoneGraphWindow>())
                EditorWindow.GetWindow<MilestoneGraphWindow>().Close();
        }

        private MilestoneNodeView NodeView => this.view.nodes.ToList().OfType<MilestoneNodeView>().Single();

        private void UndoLastEdit()
        {
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();
        }

        private MilestoneNode Stored => this.graph.FindNode(this.node.id)!;

        [Test]
        public void Undo_restoresNodePosition()
        {
            Undo.IncrementCurrentGroup();
            NodeView.SetPosition(new Rect(500f, 80f, 220f, 120f));

            UndoLastEdit();

            Assert.AreEqual(new Vector2(10f, 20f), Stored.position);
        }

        [Test]
        public void Undo_restoresTitleAndLane()
        {
            Undo.IncrementCurrentGroup();
            NodeView.SetTitle("Renamed");
            NodeView.SetLane("cyan");

            UndoLastEdit();

            Assert.AreEqual("Take Sheep Key", Stored.title);
            Assert.AreEqual("", Stored.laneId);
        }

        [Test]
        public void Undo_restoresTriggerKind()
        {
            var field = NodeView.Q<MilestoneTriggerField>();
            Undo.IncrementCurrentGroup();
            field.SetKind(MilestoneTriggerKind.DoorUnlocked);

            UndoLastEdit();

            Assert.AreEqual(MilestoneTriggerKind.None, Stored.trigger.kind);
        }

        [Test]
        public void OnOpenAsset_otherAssetWhileGraphSelected_isNotHijacked()
        {
            var other = ScriptableObject.CreateInstance<ItemData>();
            try
            {
                Selection.activeObject = this.graph;
#pragma warning disable CS0618 // instance ids are what OnOpenAsset receives
                bool handled = MilestoneGraphWindow.OnOpenAsset(other.GetInstanceID(), 0);
#pragma warning restore CS0618
                Assert.IsFalse(handled);
            }
            finally
            {
                Selection.activeObject = null;
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void RefreshLaneColors_keepsTheSameNodeViews()
        {
            var before = NodeView;
            NodeView.SetLane("cyan");
            this.graph.Lanes[0].color = Color.red;

            this.view.RefreshLaneColors();

            Assert.AreSame(before, NodeView);
            Assert.AreEqual(Color.red, NodeView.titleContainer.style.backgroundColor.value);
        }
    }
}

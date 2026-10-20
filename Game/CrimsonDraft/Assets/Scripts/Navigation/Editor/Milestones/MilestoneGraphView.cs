#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    // The asset is the only source of truth: every view change schedules Save(), which writes the
    // view back into the asset under Undo; Undo/Redo calls Load() to rebuild the view from it.
    public sealed class MilestoneGraphView : GraphView
    {
        private readonly MilestoneGraph graph;
        private bool loading;
        private bool saveScheduled;

        public MilestoneGraph Graph => this.graph;

        public MilestoneGraphView(MilestoneGraph graph)
        {
            this.graph = graph;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
            this.AddManipulator(new ContextualMenuManipulator(BuildCanvasMenu));
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) =>
            ports.Where(p => p.direction != startPort.direction && p.node != startPort.node).ToList();

        private void BuildCanvasMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target is not GraphView) return;
            Vector2 local = contentViewContainer.WorldToLocal(evt.mousePosition);
            evt.menu.AppendAction("Add Milestone", _ =>
            {
                Undo.RecordObject(this.graph, "Add Milestone");
                var node = this.graph.AddNode("New milestone", local);
                EditorUtility.SetDirty(this.graph);
                AddElement(new MilestoneNodeView(node, this));
            });
        }

        public void Load()
        {
            this.loading = true;
            DeleteElements(graphElements.ToList());

            var views = new Dictionary<string, MilestoneNodeView>();
            foreach (var node in this.graph.Nodes)
            {
                var view = new MilestoneNodeView(node, this);
                views[node.id] = view;
                AddElement(view);
            }

            foreach (var edge in this.graph.Edges)
            {
                if (!views.TryGetValue(edge.fromNodeId, out var from) || !views.TryGetValue(edge.toNodeId, out var to)) continue;
                var view = from.Output.ConnectTo<MilestoneEdgeView>(to.Input);
                view.Bind(edge, this);
                AddElement(view);
            }

            this.loading = false;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (this.loading) return change;

            if (change.edgesToCreate != null)
                foreach (var edge in change.edgesToCreate.OfType<MilestoneEdgeView>())
                    edge.Bind(new MilestoneEdge(), this);

            ScheduleSave();
            return change;
        }

        public void ScheduleSave()
        {
            if (this.loading || this.saveScheduled) return;
            this.saveScheduled = true;
            schedule.Execute(() => { this.saveScheduled = false; Save(); });
        }

        // Drops deleted nodes and rebuilds the edge list from the view. Node positions and titles
        // are written into the data by MilestoneNodeView as they change.
        public void Save()
        {
            Undo.RecordObject(this.graph, "Edit Milestone Graph");

            var nodeViews = nodes.OfType<MilestoneNodeView>().ToList();
            var alive = new HashSet<string>(nodeViews.Select(v => v.Node.id));
            foreach (var id in this.graph.Nodes.Select(n => n.id).Where(id => !alive.Contains(id)).ToList())
                this.graph.RemoveNode(id);

            this.graph.Edges.Clear();
            foreach (var edge in edges.OfType<MilestoneEdgeView>())
            {
                if (edge.output?.node is not MilestoneNodeView from || edge.input?.node is not MilestoneNodeView to) continue;
                if (!alive.Contains(from.Node.id) || !alive.Contains(to.Node.id)) continue;
                edge.Data.fromNodeId = from.Node.id;
                edge.Data.toNodeId   = to.Node.id;
                this.graph.Edges.Add(edge.Data);
            }

            EditorUtility.SetDirty(this.graph);
        }

        public Color LaneColor(string laneId) => this.graph.FindLane(laneId)?.color ?? new Color(0.35f, 0.35f, 0.35f);
    }
}

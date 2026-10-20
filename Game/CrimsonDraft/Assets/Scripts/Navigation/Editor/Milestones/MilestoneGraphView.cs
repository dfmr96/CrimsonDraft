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

            graphViewChanged         = OnGraphViewChanged;
            elementsAddedToGroup     = (_, _) => ScheduleSave();
            elementsRemovedFromGroup = (_, _) => ScheduleSave();
            groupTitleChanged        = (_, _) => ScheduleSave();
            this.AddManipulator(new ContextualMenuManipulator(BuildCanvasMenu));

            // Copy/paste milestones. EditorJsonUtility keeps the ItemData / DocumentData
            // references; pasted nodes get new ids so saves never alias them.
            serializeGraphElements = elements => string.Join("\n",
                elements.OfType<MilestoneNodeView>().Select(v => EditorJsonUtility.ToJson(v.Node)));
            canPasteSerializedData = data => !string.IsNullOrEmpty(data);
            unserializeAndPaste = (_, data) =>
            {
                Undo.RecordObject(this.graph, "Paste Milestones");
                foreach (var line in data.Split('\n'))
                {
                    var copy = new MilestoneNode();
                    EditorJsonUtility.FromJsonOverwrite(line, copy);
                    copy.id = MilestoneGraph.NewId();
                    copy.position += new Vector2(30f, 30f);
                    this.graph.Nodes.Add(copy);
                }
                EditorUtility.SetDirty(this.graph);
                Load();
            };
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

            evt.menu.AppendAction("Add Note", _ =>
            {
                Undo.RecordObject(this.graph, "Add Note");
                this.graph.Notes.Add(new MilestoneNote { id = MilestoneGraph.NewId(), text = "Note", rect = new Rect(local, new Vector2(200f, 120f)) });
                EditorUtility.SetDirty(this.graph);
                Load();
            });

            var selectedNodes = selection.OfType<MilestoneNodeView>().ToList();
            evt.menu.AppendAction("Add Frame Around Selection", _ =>
            {
                Undo.RecordObject(this.graph, "Add Frame");
                this.graph.Frames.Add(new MilestoneFrame
                {
                    id = MilestoneGraph.NewId(),
                    title = "Frame",
                    nodeIds = selectedNodes.Select(v => v.Node.id).ToList(),
                });
                EditorUtility.SetDirty(this.graph);
                Load();
            }, selectedNodes.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction("Add Lane", _ =>
            {
                Undo.RecordObject(this.graph, "Add Lane");
                this.graph.Lanes.Add(new MilestoneLane
                {
                    id = MilestoneGraph.NewId(),
                    name = $"Lane {this.graph.Lanes.Count + 1}",
                    color = Color.HSVToRGB(Random.value, 0.6f, 0.9f),
                });
                EditorUtility.SetDirty(this.graph);
                Load();
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

            foreach (var note in this.graph.Notes)
                AddElement(new MilestoneNoteView(note, this));

            foreach (var frame in this.graph.Frames)
            {
                var group = new MilestoneFrameView(frame, this);
                AddElement(group);
                foreach (var id in frame.nodeIds)
                    if (views.TryGetValue(id, out var member)) group.AddElement(member);
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

        // Drops deleted nodes and rebuilds edges, notes and frames from the view. Node positions and
        // titles, and note rects, are written into the data by their views as they change.
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

            this.graph.Notes.Clear();
            foreach (var note in graphElements.OfType<MilestoneNoteView>())
                this.graph.Notes.Add(note.Capture());

            this.graph.Frames.Clear();
            foreach (var frame in graphElements.OfType<MilestoneFrameView>())
                this.graph.Frames.Add(frame.Capture(alive));

            EditorUtility.SetDirty(this.graph);
        }

        public Color LaneColor(string laneId) => this.graph.FindLane(laneId)?.color ?? new Color(0.35f, 0.35f, 0.35f);

        // Appends "Lane/(none)" + one entry per lane to a context menu.
        public void AppendLaneMenu(DropdownMenu menu, string currentLaneId, System.Action<string> setLane)
        {
            menu.AppendAction("Lane/(none)", _ => setLane(""),
                _ => currentLaneId == "" ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            foreach (var lane in this.graph.Lanes)
            {
                var id = lane.id;
                menu.AppendAction($"Lane/{lane.name}", _ => setLane(id),
                    _ => currentLaneId == id ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
        }
    }
}

#nullable enable

using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    public sealed class MilestoneNodeView : Node
    {
        public MilestoneNode Node { get; }
        public Port Input  { get; }
        public Port Output { get; }

        private readonly MilestoneGraphView owner;

        public MilestoneNodeView(MilestoneNode node, MilestoneGraphView owner)
        {
            this.Node  = node;
            this.owner = owner;
            title = node.title;
            SetPosition(new Rect(node.position, new Vector2(220f, 120f)));

            // Port.Create<MilestoneEdgeView> (not InstantiatePort): the port's edge connector builds
            // edges of this type, so edges the user drags are MilestoneEdgeViews and get saved.
            Input  = Port.Create<MilestoneEdgeView>(Orientation.Horizontal, Direction.Input,  Port.Capacity.Multi, typeof(bool));
            Output = Port.Create<MilestoneEdgeView>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            Input.portName = ""; Output.portName = "";
            inputContainer.Add(Input);
            outputContainer.Add(Output);

            var titleField = new TextField { value = node.title };
            titleField.RegisterValueChangedCallback(e =>
            {
                this.Node.title = e.newValue;
                title = e.newValue;
                this.owner.ScheduleSave();
            });
            extensionContainer.Add(titleField);

            var laneNames = owner.Graph.Lanes.Select(l => l.name).Prepend("(none)").ToList();
            int current = owner.Graph.Lanes.FindIndex(l => l.id == node.laneId) + 1;
            var lane = new PopupField<string>("Lane", laneNames, current);
            lane.RegisterValueChangedCallback(e =>
            {
                int index = laneNames.IndexOf(e.newValue) - 1;
                this.Node.laneId = index >= 0 ? this.owner.Graph.Lanes[index].id : "";
                ApplyLaneColor();
                this.owner.ScheduleSave();
            });
            extensionContainer.Add(lane);

            this.badge = new Label { style = { color = new Color(1f, 0.8f, 0.2f), unityFontStyleAndWeight = FontStyle.Bold } };
            titleContainer.Add(this.badge);
            extensionContainer.Add(new MilestoneTriggerField(node.trigger, () =>
            {
                RefreshBadge();
                this.owner.ScheduleSave();
            }));
            RefreshBadge();

            ApplyLaneColor();
            RefreshExpandedState();
            RefreshPorts();
        }

        private readonly Label badge;

        public void RefreshBadge()
        {
            string problem = FindProblem();
            this.badge.text    = problem.Length > 0 ? "⚠" : "";
            this.badge.tooltip = problem;
        }

        // First problem found, or "" when the trigger looks valid.
        public string FindProblem()
        {
            var t = this.Node.trigger;
            switch (t.kind)
            {
                case MilestoneTriggerKind.None:
                    return "No trigger";
                case MilestoneTriggerKind.ItemHeld when t.item == null:
                case MilestoneTriggerKind.NoteRead when t.note == null:
                    return "No asset assigned";
                case MilestoneTriggerKind.PickupCollected:
                case MilestoneTriggerKind.SocketCompleted:
                case MilestoneTriggerKind.DoorUnlocked:
                case MilestoneTriggerKind.PuzzleSolved:
                    if (!MilestoneSceneIds.For(t.kind).Contains(t.targetId)) return "Id not found in open scenes";
                    break;
            }

            var twin = this.owner.Graph.Nodes.FirstOrDefault(n => n != this.Node && n.trigger.kind == t.kind
                && n.trigger.targetId == t.targetId && n.trigger.item == t.item && n.trigger.note == t.note);
            if (twin != null) return $"Same trigger as '{twin.title}'";

            foreach (var guid in AssetDatabase.FindAssets("t:MilestoneGraph"))
            {
                var other = AssetDatabase.LoadAssetAtPath<MilestoneGraph>(AssetDatabase.GUIDToAssetPath(guid));
                if (other != null && other != this.owner.Graph && other.FindNode(this.Node.id) != null)
                    return $"Duplicate id in {other.name}";
            }
            return "";
        }

        public void ApplyLaneColor() =>
            titleContainer.style.backgroundColor = this.owner.LaneColor(this.Node.laneId);

        // The position is written to the data here, not read back in Save(): GetPosition() is the
        // layout rect, which is zero until the view has been laid out.
        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            this.Node.position = newPos.position;
            this.owner?.ScheduleSave();
        }
    }
}

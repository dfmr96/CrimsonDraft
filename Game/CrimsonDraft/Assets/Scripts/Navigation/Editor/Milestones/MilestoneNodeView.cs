#nullable enable

using UnityEditor.Experimental.GraphView;
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

            ApplyLaneColor();
            RefreshExpandedState();
            RefreshPorts();
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

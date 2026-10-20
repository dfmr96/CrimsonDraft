#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    // Titled frame around a set of milestones (e.g. the yellow box in the design sketch), tinted
    // by its lane. Its bounds follow its members, so only title, lane and members are stored.
    public sealed class MilestoneFrameView : Group
    {
        private readonly MilestoneFrame frame;
        private readonly MilestoneGraphView owner;

        public MilestoneFrameView(MilestoneFrame frame, MilestoneGraphView owner)
        {
            this.frame = frame;
            this.owner = owner;
            title = frame.title;
            ApplyLaneColor();

            this.AddManipulator(new ContextualMenuManipulator(evt =>
                owner.AppendLaneMenu(evt.menu, this.frame.laneId, laneId =>
                {
                    owner.RecordUndo("Change Frame Lane");
                    this.frame.laneId = laneId;
                    ApplyLaneColor();
                    owner.ScheduleSave();
                })));
        }

        public void ApplyLaneColor()
        {
            var color = this.owner.LaneColor(this.frame.laneId);
            style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = color;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 2f;
        }

        public MilestoneFrame Capture(HashSet<string> aliveNodeIds)
        {
            this.frame.title   = title;
            this.frame.nodeIds = containedElements.OfType<MilestoneNodeView>()
                .Select(v => v.Node.id)
                .Where(aliveNodeIds.Contains)
                .ToList();
            return this.frame;
        }
    }
}

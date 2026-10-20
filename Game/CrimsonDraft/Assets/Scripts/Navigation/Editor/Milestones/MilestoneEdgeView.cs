#nullable enable

using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    // Colour from its lane (or a custom colour); Dashed edges hide the stock line and draw dashes
    // along the same control points. Documentation only.
    public sealed class MilestoneEdgeView : Edge
    {
        public MilestoneEdge Data { get; private set; } = new();
        private MilestoneGraphView? owner;
        private readonly VisualElement dashes = new() { pickingMode = PickingMode.Ignore };

        public MilestoneEdgeView()
        {
            this.dashes.StretchToParentSize();
            this.dashes.generateVisualContent += DrawDashes;
            edgeControl.Add(this.dashes);
        }

        public void Bind(MilestoneEdge data, MilestoneGraphView view)
        {
            this.Data  = data;
            this.owner = view;
            UpdateEdgeControl();
        }

        private Color DataColor =>
            this.Data.useCustomColor || this.owner == null ? this.Data.customColor : this.owner.LaneColor(this.Data.laneId);

        public override bool UpdateEdgeControl()
        {
            bool result = base.UpdateEdgeControl();
            if (!selected)
            {
                var color = this.Data.style == EdgeStyle.Dashed ? Color.clear : DataColor;
                edgeControl.inputColor  = color;
                edgeControl.outputColor = color;
            }
            this.dashes.MarkDirtyRepaint();
            return result;
        }

        private void DrawDashes(MeshGenerationContext ctx)
        {
            if (this.Data.style != EdgeStyle.Dashed || edgeControl.controlPoints == null) return;
            var points = edgeControl.controlPoints;
            var painter = ctx.painter2D;
            painter.strokeColor = selected ? Color.white : DataColor;
            painter.lineWidth = 2f;
            const float dash = 8f, gap = 6f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                float length = Vector2.Distance(a, b);
                Vector2 dir = length > 0f ? (b - a) / length : Vector2.zero;
                for (float t = 0f; t < length; t += dash + gap)
                {
                    painter.BeginPath();
                    painter.MoveTo(a + dir * t);
                    painter.LineTo(a + dir * Mathf.Min(t + dash, length));
                    painter.Stroke();
                }
            }
        }
    }
}

#nullable enable

using System;

namespace CrimsonDraft.Navigation.Milestones
{
    // Inspector-friendly pointer to one milestone (graph + node id); MilestoneRefDrawer shows a
    // dropdown of the graph's node titles, so code never hard-codes milestone ids.
    [Serializable]
    public struct MilestoneRef
    {
        public MilestoneGraph? graph;
        public string          nodeId;

        public MilestoneRef(MilestoneGraph? graph, string nodeId)
        {
            this.graph  = graph;
            this.nodeId = nodeId;
        }

        public bool IsValid => this.graph != null && !string.IsNullOrEmpty(this.nodeId);
    }
}

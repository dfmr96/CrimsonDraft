#nullable enable

using System;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Milestones
{
    public sealed class MilestoneService : IMilestoneService, IDisposable
    {
        private readonly MilestoneRegistry registry;
        private readonly MilestoneGraph[]  graphs;

        public event Action<MilestoneRef>? Completed;

        [Preserve]
        public MilestoneService(MilestoneRegistry registry, MilestoneGraph[] graphs)
        {
            this.registry = registry;
            this.graphs   = graphs;
            this.registry.Completed += OnCompleted;
        }

        public bool IsCompleted(MilestoneRef milestone) => milestone.IsValid && this.registry.IsCompleted(milestone.nodeId);
        public bool IsCompleted(string milestoneId)    => this.registry.IsCompleted(milestoneId);

        private void OnCompleted(string id)
        {
            foreach (var graph in this.graphs)
                if (graph != null && graph.FindNode(id) != null)
                {
                    Completed?.Invoke(new MilestoneRef(graph, id));
                    return;
                }
        }

        public void Dispose() => this.registry.Completed -= OnCompleted;
    }
}

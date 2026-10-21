#nullable enable

using System;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Milestones
{
    // Completes milestones whose trigger holds: every pending node once on Initialize (registered
    // after SaveGameLoader, so loaded state counts), then only the nodes tied to a source when that
    // source changes. Never un-completes.
    public sealed class MilestoneTracker : IInitializable, IDisposable
    {
        private readonly IMilestoneWorldState world;
        private readonly MilestoneRegistry    registry;
        private readonly MilestoneGraph[]     graphs;

        [Preserve]
        public MilestoneTracker(IMilestoneWorldState world, MilestoneRegistry registry, MilestoneGraph[] graphs)
        {
            this.world    = world;
            this.registry = registry;
            this.graphs   = graphs;
        }

        void IInitializable.Initialize()
        {
            Evaluate(null);
            this.world.SourceChanged += OnSourceChanged;
        }

        private void OnSourceChanged(MilestoneSource source) => Evaluate(source);

        // null = every kind.
        private void Evaluate(MilestoneSource? source)
        {
            foreach (var graph in this.graphs)
            {
                if (graph == null) continue;
                foreach (var node in graph.Nodes)
                {
                    if (this.registry.IsCompleted(node.id)) continue;
                    if (source.HasValue && MilestoneTriggerEvaluator.SourceOf(node.trigger.kind) != source.Value) continue;
                    if (MilestoneTriggerEvaluator.Holds(node.trigger, this.world))
                        this.registry.Complete(node.id);
                }
            }
        }

        void IDisposable.Dispose() => this.world.SourceChanged -= OnSourceChanged;
    }
}

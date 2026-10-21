#nullable enable

using System.Collections.Generic;
using System.Linq;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Milestones
{
    // Which milestones are "next" according to the diagram: not completed yet, and every incoming
    // edge comes from a completed milestone (no incoming edges = available from the start).
    // Edges still never gate gameplay; this only reads the diagram, for debugging.
    public static class MilestoneAvailability
    {
        public static IEnumerable<string> AvailableTitles(IEnumerable<MilestoneGraph?> graphs, MilestoneRegistry registry)
        {
            foreach (var graph in graphs)
            {
                if (graph == null) continue;
                foreach (var node in graph.Nodes)
                    if (IsAvailable(graph, node, registry))
                        yield return node.title;
            }
        }

        // One title per line; "(none)" so an empty list still shows the overlay is on.
        public static string FormatList(IEnumerable<string> titles)
        {
            var list = titles.ToList();
            return list.Count == 0 ? "(none)" : string.Join("\n", list);
        }

        private static bool IsAvailable(MilestoneGraph graph, MilestoneNode node, MilestoneRegistry registry)
        {
            if (registry.IsCompleted(node.id)) return false;
            foreach (var edge in graph.Edges)
            {
                if (edge.toNodeId != node.id) continue;
                // An edge from a deleted node can never complete, so it doesn't block.
                if (graph.FindNode(edge.fromNodeId) == null) continue;
                if (!registry.IsCompleted(edge.fromNodeId)) return false;
            }
            return true;
        }
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Navigation.Milestones
{
    // One progression diagram (an area or act). Edited in Tools > CrimsonDraft > Milestone Graph;
    // read at runtime by MilestoneTracker. Node ids are what saves store -- never reuse or edit them.
    [CreateAssetMenu(menuName = "CrimsonDraft/Milestone Graph", fileName = "Milestones")]
    public sealed class MilestoneGraph : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] private List<MilestoneLane>  lanes  = new();
        [SerializeField] private List<MilestoneNode>  nodes  = new();
        [SerializeField] private List<MilestoneEdge>  edges  = new();
        [SerializeField] private List<MilestoneNote>  notes  = new();
        [SerializeField] private List<MilestoneFrame> frames = new();

        public List<MilestoneLane>  Lanes  => this.lanes;
        public List<MilestoneNode>  Nodes  => this.nodes;
        public List<MilestoneEdge>  Edges  => this.edges;
        public List<MilestoneNote>  Notes  => this.notes;
        public List<MilestoneFrame> Frames => this.frames;

        public static string NewId() => Guid.NewGuid().ToString("N");

        public MilestoneNode AddNode(string title, Vector2 position)
        {
            var node = new MilestoneNode { id = NewId(), title = title, position = position };
            this.nodes.Add(node);
            return node;
        }

        public bool RemoveNode(string nodeId)
        {
            if (this.nodes.RemoveAll(n => n.id == nodeId) == 0) return false;
            this.edges.RemoveAll(e => e.fromNodeId == nodeId || e.toNodeId == nodeId);
            foreach (var frame in this.frames)
                frame.nodeIds.Remove(nodeId);
            return true;
        }

        public MilestoneNode? FindNode(string nodeId) => this.nodes.Find(n => n.id == nodeId);

        public MilestoneLane? FindLane(string laneId) => this.lanes.Find(l => l.id == laneId);

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        // Old graphs may still hold SocketCompleted / PuzzleSolved triggers: upgrade them as they load,
        // so runtime and the editor only ever see GateCompleted. The asset is rewritten on its next save.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            foreach (var node in this.nodes)
                node.trigger?.UpgradeLegacy();
        }
    }
}

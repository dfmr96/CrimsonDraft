#nullable enable

using System.Collections.Generic;
using UnityEditor;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    // What the validation badges check against, gathered once per MilestoneGraphView.Load():
    // scene ids per trigger kind (scanned lazily, at most once each) and the node ids of every
    // other MilestoneGraph asset. Keeps badge refreshes from rescanning scenes and the AssetDatabase.
    public sealed class MilestoneValidation
    {
        private readonly Dictionary<MilestoneTriggerKind, HashSet<string>> sceneIds = new();
        private readonly Dictionary<string, string> otherGraphNodeIds = new(); // node id -> asset name

        public MilestoneValidation(MilestoneGraph current)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:MilestoneGraph"))
            {
                var other = AssetDatabase.LoadAssetAtPath<MilestoneGraph>(AssetDatabase.GUIDToAssetPath(guid));
                if (other == null || other == current) continue;
                foreach (var node in other.Nodes)
                    this.otherGraphNodeIds[node.id] = other.name;
            }
        }

        public bool SceneHas(MilestoneTriggerKind kind, string id)
        {
            if (!this.sceneIds.TryGetValue(kind, out var ids))
                this.sceneIds[kind] = ids = new HashSet<string>(MilestoneSceneIds.For(kind));
            return ids.Contains(id);
        }

        public string? OtherGraphWith(string nodeId) =>
            this.otherGraphNodeIds.TryGetValue(nodeId, out var name) ? name : null;
    }
}

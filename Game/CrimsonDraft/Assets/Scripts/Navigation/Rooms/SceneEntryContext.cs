#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Navigation/SceneEntryContext")]
    public sealed class SceneEntryContext : ScriptableObject
    {
        public string? PendingEntryPointId { get; private set; }

        public void SetPendingEntry(string entryPointId)
            => this.PendingEntryPointId = entryPointId;

        public string? Consume()
        {
            var id                   = this.PendingEntryPointId;
            this.PendingEntryPointId = null;
            return id;
        }
    }
}

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

        // A script reload in the editor round-trips these private strings through Unity's serializer,
        // which turns null into "": empty means nothing pending.
        public string? Consume()
        {
            var id                   = this.PendingEntryPointId;
            this.PendingEntryPointId = null;
            return string.IsNullOrEmpty(id) ? null : id;
        }

        /// <summary>Room the next deck scene starts in (debug teleport); wins over an entry point.</summary>
        public string? PendingRoomId { get; private set; }

        public void SetPendingRoom(string roomId) => this.PendingRoomId = roomId;

        public string? ConsumeRoom()
        {
            var id             = this.PendingRoomId;
            this.PendingRoomId = null;
            return string.IsNullOrEmpty(id) ? null : id;
        }
    }
}

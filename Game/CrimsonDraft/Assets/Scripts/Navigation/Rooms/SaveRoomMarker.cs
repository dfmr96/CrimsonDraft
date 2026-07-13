#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    // Empty tag component — its presence on a room's GameObject marks it as the save room.
    // MusicManagerController checks for it via GetComponent.
    public sealed class SaveRoomMarker : MonoBehaviour { }
}

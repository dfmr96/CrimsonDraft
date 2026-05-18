#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private RoomController? fromRoom;

        public RoomController? FromRoom => this.fromRoom;
    }
}

#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    // Optional sibling of RoomController on rooms that have a MarineraSector assigned.
    // Rooms without this component fall back to MusicManagerController's defaultSector.
    public sealed class RoomSectorProfile : MonoBehaviour
    {
        [SerializeField] private AK.Wwise.Switch marineraSector = new();

        public AK.Wwise.Switch MarineraSector => this.marineraSector;
    }
}

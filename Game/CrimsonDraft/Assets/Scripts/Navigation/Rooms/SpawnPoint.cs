#nullable enable

using Unity.Cinemachine;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private RoomController?    fromRoom;
        [SerializeField] private CinemachineCamera? camera;

        public RoomController? FromRoom => this.fromRoom;

        public void ActivateCamera(IFixedCameraZoneService zoneService)
        {
            if (this.camera != null) zoneService.ActivateZone(this.camera);
        }
    }
}

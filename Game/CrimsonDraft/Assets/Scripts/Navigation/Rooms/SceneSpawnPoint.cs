#nullable enable

using Unity.Cinemachine;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class SceneSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string             entryPointId = null!;
        [SerializeField] private RoomController     startingRoom = null!;
        [SerializeField] private CinemachineCamera? camera;

        public string         EntryPointId => this.entryPointId;
        public RoomController StartingRoom => this.startingRoom;

        public void ActivateCamera(IFixedCameraZoneService zoneService)
        {
            if (this.camera != null) zoneService.ActivateZone(this.camera);
        }
    }
}

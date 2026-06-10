#nullable enable

using Unity.Cinemachine;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class SceneSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string             entryPointId = null!;
        [SerializeField] private RoomController     startingRoom = null!;
        [SerializeField] private CinemachineCamera? camera;

        public string         EntryPointId => this.entryPointId;
        public RoomController StartingRoom => this.startingRoom;

        public void ActivateCamera()
        {
            if (this.camera == null) return;

            var room = GetComponentInParent<RoomController>(includeInactive: true);
            if (room == null) return;

            foreach (var cam in room.GetComponentsInChildren<CinemachineCamera>(includeInactive: true))
                cam.gameObject.SetActive(false);

            this.camera.gameObject.SetActive(true);
        }
    }
}

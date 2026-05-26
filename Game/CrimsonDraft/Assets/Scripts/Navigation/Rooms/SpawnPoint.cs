#nullable enable

using Unity.Cinemachine;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private RoomController?    fromRoom;
        [SerializeField] private CinemachineCamera? camera;

        public RoomController? FromRoom => this.fromRoom;

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

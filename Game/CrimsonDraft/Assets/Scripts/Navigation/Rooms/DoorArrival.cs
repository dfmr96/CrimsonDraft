#nullable enable

using Unity.Cinemachine;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Navigation.Rooms
{
    /// <summary>Where the player appears when arriving through this door, and the optional shot to cut
    /// to. Lives on the same object as a RoomDoorInteractable or SceneDoorInteractable; the arrival
    /// point is a child transform, placed by default in front of the door and movable by hand.</summary>
    public sealed class DoorArrival : MonoBehaviour
    {
        [SerializeField] private Transform          arrivalPoint = null!;
        [SerializeField] private CinemachineCamera? arrivalCamera;

        public Transform          ArrivalPoint  => this.arrivalPoint;
        public CinemachineCamera? ArrivalCamera => this.arrivalCamera;

        public void ActivateCamera(IFixedCameraZoneService zoneService)
        {
            if (this.arrivalCamera != null) zoneService.ActivateZone(this.arrivalCamera);
        }

        /// <summary>Puts the arrival point (created on first use) at the default pose in front of the door.</summary>
        public void PlaceAtDefault(float distance, Vector3 interiorHint)
        {
            if (this.arrivalPoint == null)
            {
                var child = new GameObject("ArrivalPoint").transform;
                child.SetParent(this.transform, false);
                this.arrivalPoint = child;
            }

            var pose = DoorArrivalPlacement.DefaultPose(DoorArrivalPlacement.DoorBounds(this), interiorHint, distance);
            this.arrivalPoint.SetPositionAndRotation(pose.position, pose.rotation);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            var room = this.GetComponentInParent<RoomController>(true);
            var hint = room != null ? DoorArrivalPlacement.InteriorHint(room) : this.transform.position + this.transform.forward;
            this.PlaceAtDefault(DoorArrivalPlacement.DefaultDistance, hint);
        }
#endif
    }
}

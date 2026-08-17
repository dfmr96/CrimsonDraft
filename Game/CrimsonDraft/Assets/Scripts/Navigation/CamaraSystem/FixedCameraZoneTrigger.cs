#nullable enable

using Unity.Cinemachine;
using UnityEngine;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    public sealed class FixedCameraZoneTrigger : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera zoneCamera = null!;

        public CinemachineCamera ZoneCamera => this.zoneCamera;

        private IFixedCameraZoneService? zoneService;

        public void Construct(IFixedCameraZoneService zoneService)
        {
            this.zoneService = zoneService;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (this.zoneCamera == null || this.zoneService == null) return;

            this.zoneService.ActivateZone(this.zoneCamera);
        }
    }
}

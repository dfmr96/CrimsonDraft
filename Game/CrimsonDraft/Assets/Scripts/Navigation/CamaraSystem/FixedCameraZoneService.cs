#nullable enable

using Unity.Cinemachine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    public sealed class FixedCameraZoneService : IFixedCameraZoneService
    {
        private CinemachineCamera? current;

        public CinemachineCamera? CurrentZoneCamera => this.current;

        [Preserve]
        public FixedCameraZoneService() { }

        public void ActivateZone(CinemachineCamera zoneCamera)
        {
            if (zoneCamera == null || zoneCamera == this.current) return;

            if (this.current != null)
                this.current.enabled = false;

            zoneCamera.enabled = true;
            this.current = zoneCamera;
        }
    }
}

#nullable enable

using Unity.Cinemachine;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Tests
{
    public sealed class FakeCameraZoneService : IFixedCameraZoneService
    {
        public CinemachineCamera? CurrentZoneCamera  { get; set; }
        public CinemachineCamera? GameplayZoneCamera { get; set; }

        public void ActivateZone(CinemachineCamera zoneCamera)
        {
            this.CurrentZoneCamera  = zoneCamera;
            this.GameplayZoneCamera = zoneCamera;
        }

        public void ActivateInspectionShot(CinemachineCamera inspectCamera) => this.CurrentZoneCamera = inspectCamera;
    }
}

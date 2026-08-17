#nullable enable

using Unity.Cinemachine;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    public interface IFixedCameraZoneService
    {
        CinemachineCamera? CurrentZoneCamera { get; }
        void ActivateZone(CinemachineCamera zoneCamera);
    }
}

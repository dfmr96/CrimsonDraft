#nullable enable

using Unity.Cinemachine;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    public interface IFixedCameraZoneService
    {
        CinemachineCamera? CurrentZoneCamera { get; }

        /// <summary>The room shot the player is in, ignoring any inspection close-up layered on
        /// top -- what a save records and what an inspection returns to.</summary>
        CinemachineCamera? GameplayZoneCamera { get; }

        void ActivateZone(CinemachineCamera zoneCamera);

        /// <summary>Cuts to a temporary close-up (telegraph, generator panel...) without replacing
        /// <see cref="GameplayZoneCamera"/>.</summary>
        void ActivateInspectionShot(CinemachineCamera inspectCamera);
    }
}

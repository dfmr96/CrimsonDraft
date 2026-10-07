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
            if (zoneCamera == null) return;

            if (this.current != null && this.current != zoneCamera)
                this.current.enabled = false;

            // Deliberately re-asserted even when zoneCamera is already the current one: the
            // enabled flag can have been turned off behind this service's back -- e.g.
            // FixedCameraZoneBootstrap normalizes a room's cameras directly at scene load, and a
            // room being deactivated/reactivated takes its cameras down with it. Early-returning
            // on "already current" left those cases with a current camera that renders nothing.
            zoneCamera.enabled = true;
            this.current = zoneCamera;
        }
    }
}

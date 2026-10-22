#nullable enable

using Unity.Cinemachine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    public sealed class FixedCameraZoneService : IFixedCameraZoneService
    {
        private CinemachineCamera? current;
        private CinemachineCamera? gameplay;

        public CinemachineCamera? CurrentZoneCamera  => this.current;
        public CinemachineCamera? GameplayZoneCamera => this.gameplay;

        [Preserve]
        public FixedCameraZoneService() { }

        public void ActivateZone(CinemachineCamera zoneCamera)
        {
            if (zoneCamera == null) return;
            SwitchTo(zoneCamera);
            this.gameplay = zoneCamera;
        }

        public void ActivateInspectionShot(CinemachineCamera inspectCamera)
        {
            if (inspectCamera == null) return;
            SwitchTo(inspectCamera);
        }

        private void SwitchTo(CinemachineCamera camera)
        {
            if (this.current != null && this.current != camera)
                this.current.enabled = false;

            // Deliberately re-asserted even when camera is already the current one: the
            // enabled flag can have been turned off behind this service's back -- e.g.
            // FixedCameraZoneBootstrap normalizes a room's cameras directly at scene load, and a
            // room being deactivated/reactivated takes its cameras down with it. Early-returning
            // on "already current" left those cases with a current camera that renders nothing.
            camera.enabled = true;
            this.current = camera;
        }
    }
}

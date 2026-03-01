#nullable enable

using UnityEngine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Cameras
{
    public sealed class CameraService : ICameraService
    {
        private Camera? navigationCamera;
        private Camera? combatCamera;

        [Preserve]
        public CameraService() { }

        public void RegisterNavigationCamera(Camera camera) => this.navigationCamera = camera;
        public void RegisterCombatCamera(Camera camera)    => this.combatCamera    = camera;

        public void ActivateNavigationCamera()
        {
            SetEnabled(this.combatCamera, false);
            SetEnabled(this.navigationCamera, true);
        }

        public void ActivateCombatCamera()
        {
            SetEnabled(this.navigationCamera, false);
            SetEnabled(this.combatCamera, true);
        }

        private static void SetEnabled(Camera? camera, bool enabled)
        {
            if (camera == null) return;
            camera.enabled = enabled;
            var listener = camera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = enabled;
        }
    }
}

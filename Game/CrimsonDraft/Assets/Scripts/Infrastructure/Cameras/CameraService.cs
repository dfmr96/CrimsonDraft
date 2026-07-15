#nullable enable

using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Cameras
{
    public sealed class CameraService : ICameraService
    {
        private Camera? navigationCamera;
        private Camera? combatCamera;

        public Camera? ActiveCamera { get; private set; }
        public event Action<Camera>? ActiveCameraChanged;

        [Preserve]
        public CameraService() { }

        public void RegisterNavigationCamera(Camera camera)
        {
            this.navigationCamera = camera;
            if (camera.enabled) SetActive(camera);
        }

        public void RegisterCombatCamera(Camera camera)
        {
            this.combatCamera = camera;
            if (camera.enabled) SetActive(camera);
        }

        public void ActivateNavigationCamera()
        {
            SetEnabled(this.combatCamera, false);
            SetEnabled(this.navigationCamera, true);
            SetActive(this.navigationCamera);
        }

        public void ActivateCombatCamera()
        {
            SetEnabled(this.navigationCamera, false);
            SetEnabled(this.combatCamera, true);
            SetActive(this.combatCamera);
        }

        private void SetActive(Camera? camera)
        {
            if (camera == null) return;
            this.ActiveCamera = camera;
            this.ActiveCameraChanged?.Invoke(camera);
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

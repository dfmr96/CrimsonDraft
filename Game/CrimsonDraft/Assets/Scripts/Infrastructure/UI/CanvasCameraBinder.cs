#nullable enable

using UnityEngine;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Cameras;

namespace CrimsonDraft.Infrastructure.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class CanvasCameraBinder : MonoBehaviour, IInitializable, System.IDisposable
    {
        [Inject] private ICameraService cameraService = null!;

        private Canvas canvas = null!;

        public void Initialize()
        {
            this.canvas = GetComponent<Canvas>();
            this.cameraService.ActiveCameraChanged += OnActiveCameraChanged;
            if (this.cameraService.ActiveCamera != null) OnActiveCameraChanged(this.cameraService.ActiveCamera);
        }

        private void OnActiveCameraChanged(Camera camera) => this.canvas.worldCamera = camera;

        public void Dispose()
        {
            this.cameraService.ActiveCameraChanged -= OnActiveCameraChanged;
        }
    }
}

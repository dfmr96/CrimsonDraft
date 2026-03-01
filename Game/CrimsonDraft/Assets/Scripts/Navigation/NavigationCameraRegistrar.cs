#nullable enable

using UnityEngine;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Cameras;

namespace CrimsonDraft.Navigation
{
    [RequireComponent(typeof(Camera))]
    public sealed class NavigationCameraRegistrar : MonoBehaviour, IInitializable
    {
        [Inject] private ICameraService cameraService = null!;

        void IInitializable.Initialize()
        {
            this.cameraService.RegisterNavigationCamera(GetComponent<Camera>());
        }
    }
}

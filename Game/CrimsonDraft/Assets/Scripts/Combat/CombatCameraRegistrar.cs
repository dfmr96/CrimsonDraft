#nullable enable

using UnityEngine;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Cameras;

namespace CrimsonDraft.Combat
{
    [RequireComponent(typeof(Camera))]
    public sealed class CombatCameraRegistrar : MonoBehaviour, IInitializable
    {
        [Inject] private ICameraService cameraService = null!;

        void IInitializable.Initialize()
        {
            var cam = GetComponent<Camera>();
            cam.enabled = false;

            var listener = GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;

            this.cameraService.RegisterCombatCamera(cam);
        }
    }
}

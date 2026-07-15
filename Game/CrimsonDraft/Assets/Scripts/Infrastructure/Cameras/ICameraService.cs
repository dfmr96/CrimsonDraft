#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Cameras
{
    public interface ICameraService
    {
        Camera? ActiveCamera { get; }
        event Action<Camera>? ActiveCameraChanged;

        void RegisterNavigationCamera(Camera camera);
        void RegisterCombatCamera(Camera camera);
        void ActivateNavigationCamera();
        void ActivateCombatCamera();
    }
}

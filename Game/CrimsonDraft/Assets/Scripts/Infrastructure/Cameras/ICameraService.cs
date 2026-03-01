#nullable enable

using UnityEngine;

namespace CrimsonDraft.Infrastructure.Cameras
{
    public interface ICameraService
    {
        void RegisterNavigationCamera(Camera camera);
        void RegisterCombatCamera(Camera camera);
        void ActivateNavigationCamera();
        void ActivateCombatCamera();
    }
}

#nullable enable

using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    public sealed class CameraRelativeMovementService : ICameraRelativeMovementService, IInitializable, IDisposable
    {
        private readonly CinemachineBrain brain;

        private Transform? basisTransform;
        private Transform? pendingBasisTransform;

        [Preserve]
        public CameraRelativeMovementService(CinemachineBrain brain)
        {
            this.brain = brain;
        }

        void IInitializable.Initialize()
        {
            CinemachineCore.CameraActivatedEvent.AddListener(OnCameraActivated);
            this.basisTransform = ResolveTransform(this.brain.ActiveVirtualCamera);
        }

        void IDisposable.Dispose()
        {
            CinemachineCore.CameraActivatedEvent.RemoveListener(OnCameraActivated);
        }

        public void Tick(bool inputHeld)
        {
            if (this.pendingBasisTransform == null || inputHeld) return;

            this.basisTransform = this.pendingBasisTransform;
            this.pendingBasisTransform = null;
        }

        public Vector3 Forward => Flatten(this.basisTransform != null ? this.basisTransform.forward : Vector3.forward);
        public Vector3 Right   => Flatten(this.basisTransform != null ? this.basisTransform.right   : Vector3.right);

        private void OnCameraActivated(ICinemachineCamera.ActivationEventParams evt)
        {
            if (!ReferenceEquals(evt.Origin, this.brain)) return;

            var incoming = ResolveTransform(evt.IncomingCamera);
            if (incoming != null) this.pendingBasisTransform = incoming;
        }

        private static Transform? ResolveTransform(ICinemachineCamera? camera) => (camera as Component)?.transform;

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }
}

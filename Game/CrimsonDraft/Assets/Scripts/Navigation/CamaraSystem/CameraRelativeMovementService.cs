#nullable enable

using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    // Modeled on Resident Evil HD Remaster's "Alternate" control scheme: holding a direction
    // defines a "north" in the world that survives any number of fixed-camera cuts, so the
    // player never gets spun around mid-hold by a camera switching behind them. The basis is
    // only ever resampled against whichever camera is active *right now*, and only when the
    // held input direction itself changes beyond a small deadzone (or goes from released to
    // held) -- never on a timer, and never just because the camera changed.
    public sealed class CameraRelativeMovementService : ICameraRelativeMovementService, IInitializable
    {
        // How far the held stick direction has to swing, in degrees, before it counts as a
        // deliberate re-aim rather than analog jitter -- placeholder, needs a feel pass.
        private const float DirectionChangeDeadzoneDegrees = 15f;
        private const float HeldSqrMagnitudeThreshold       = 0.0001f;

        private readonly CinemachineBrain brain;

        private Transform? basisTransform;
        private Vector2    referenceDirection;
        private bool       referenceIsSet;

        [Preserve]
        public CameraRelativeMovementService(CinemachineBrain brain)
        {
            this.brain = brain;
        }

        void IInitializable.Initialize()
        {
            this.basisTransform = ResolveTransform(this.brain.ActiveVirtualCamera);
        }

        public void Tick(Vector2 heldDirection)
        {
            if (heldDirection.sqrMagnitude < HeldSqrMagnitudeThreshold)
            {
                // Released -- nothing to preserve. The next press, in any direction, must
                // resample the camera that's active at that moment.
                this.referenceIsSet = false;
                return;
            }

            if (!ShouldResampleBasis(this.referenceIsSet, this.referenceDirection, heldDirection, DirectionChangeDeadzoneDegrees))
                return;

            this.basisTransform     = ResolveTransform(this.brain.ActiveVirtualCamera) ?? this.basisTransform;
            this.referenceDirection = heldDirection;
            this.referenceIsSet     = true;
        }

        // Pure decision, split out from Tick() so the policy itself -- "how much does a held
        // direction have to swing before it counts as a deliberate re-aim" -- can be unit
        // tested without a live Cinemachine brain/vcam setup.
        internal static bool ShouldResampleBasis(bool referenceIsSet, Vector2 referenceDirection, Vector2 heldDirection, float deadzoneDegrees)
        {
            if (!referenceIsSet) return true;
            return Vector2.Angle(referenceDirection, heldDirection) > deadzoneDegrees;
        }

        public Vector3 Forward => Flatten(this.basisTransform != null ? this.basisTransform.forward : Vector3.forward);
        public Vector3 Right   => Flatten(this.basisTransform != null ? this.basisTransform.right   : Vector3.right);

        private static Transform? ResolveTransform(ICinemachineCamera? camera) => (camera as Component)?.transform;

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }
}

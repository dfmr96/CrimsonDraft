#nullable enable

using UnityEngine;

namespace CrimsonDraft.Rendering.Shadows
{
    public sealed class BlobShadowFootstepRelay : MonoBehaviour
    {
        [SerializeField] private BlobShadow shadow      = null!;
        [SerializeField] private Rigidbody  rb          = null!;
        [SerializeField] private float      minSpeedSqr = 0.05f;

        // Called by the same Animation Events that drive FootstepController (walk/run foot contacts)
        public void OnWalkStep() => Relay(FootstepPace.Walk);

        public void OnRunStep() => Relay(FootstepPace.Run);

        private void Relay(FootstepPace pace)
        {
            if (this.rb.linearVelocity.sqrMagnitude < this.minSpeedSqr) return;
            this.shadow.PulseStep(pace);
        }
    }
}

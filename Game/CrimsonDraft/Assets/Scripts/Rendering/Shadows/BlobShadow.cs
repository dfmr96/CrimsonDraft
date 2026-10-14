#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Rendering.Shadows
{
    public sealed class BlobShadow : MonoBehaviour
    {
        [SerializeField] private BlobShadowSettings settings       = null!;
        [SerializeField] private Renderer           shadowRenderer = null!;
        [SerializeField] private Transform?         owner;

        private const int MaxGroundHits = 8;

        private readonly RaycastHit[]    groundHits = new RaycastHit[MaxGroundHits];
        private readonly BlobShadowPulse pulse      = new BlobShadowPulse();

        private Transform followed = null!;

        private Transform? ResolveOwner() => this.owner != null ? this.owner : transform.parent;

        private void Awake()
        {
            var resolved = ResolveOwner();
            if (resolved == null)
                throw new InvalidOperationException(
                    $"{nameof(BlobShadow)} on '{name}' needs an {nameof(this.owner)} or a parent transform to follow.");

            this.followed = resolved;
        }

        public void PulseStep(FootstepPace pace)
            => this.pulse.Trigger(this.settings.PulseAmplitude(pace), this.settings.PulseDuration);

        private void LateUpdate()
        {
            this.pulse.Tick(Time.deltaTime);
            transform.localScale = BlobShadowPose.FootprintScale(this.settings.Size, this.pulse.ScaleMultiplier);

            var origin   = this.followed.position + Vector3.up * this.settings.RayStartHeight;
            var grounded = TryFindGround(origin, out var ground);

            this.shadowRenderer.enabled = grounded;
            if (!grounded) return;

            var pose = BlobShadowPose.FromGroundHit(ground.point, ground.normal, this.followed.right, this.settings.SurfaceBias);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        private bool TryFindGround(Vector3 origin, out RaycastHit ground)
        {
            var hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, this.groundHits, this.settings.RayDistance,
                                                   this.settings.GroundMask, QueryTriggerInteraction.Ignore);
            ground = default;
            var found = false;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = this.groundHits[i];
                if (hit.collider.transform.IsChildOf(this.followed)) continue;
                if (found && hit.distance >= ground.distance) continue;

                ground = hit;
                found  = true;
            }

            return found;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var resolved = ResolveOwner();
            if (this.settings == null || resolved == null) return;

            var origin = resolved.position + Vector3.up * this.settings.RayStartHeight;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, origin + Vector3.down * this.settings.RayDistance);
        }
#endif
    }
}

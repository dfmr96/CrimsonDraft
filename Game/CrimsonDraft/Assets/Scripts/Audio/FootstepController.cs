#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    public sealed class FootstepController : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event walkEvent = new AK.Wwise.Event();
        [SerializeField] private AK.Wwise.Event runEvent  = new AK.Wwise.Event();

        [Header("Surface")]
        [SerializeField] private SurfaceTypeMapping mapping     = null!;
        [SerializeField] private LayerMask          floorMask;
        [SerializeField] private float              rayDistance = 0.3f;

        [Header("Motion Guard")]
        [SerializeField] private Rigidbody rb          = null!;
        [SerializeField] private float     minSpeedSqr = 0.05f;

        // Called by Animation Event on walk clip (left and right foot contacts)
        public void OnWalkStep()
        {
#if UNITY_EDITOR
            //Debug.Log($"[Footstep] OnWalkStep — velocity sqr: {rb.linearVelocity.sqrMagnitude:F3} (threshold: {minSpeedSqr})");
#endif
            if (rb.linearVelocity.sqrMagnitude < minSpeedSqr) return;
            DetectAndPost(walkEvent);
        }

        // Called by Animation Event on run clip (left and right foot contacts)
        public void OnRunStep()
        {
#if UNITY_EDITOR
            //Debug.Log($"[Footstep] OnRunStep — velocity sqr: {rb.linearVelocity.sqrMagnitude:F3} (threshold: {minSpeedSqr})");
#endif
            if (rb.linearVelocity.sqrMagnitude < minSpeedSqr) return;
            DetectAndPost(runEvent);
        }

        private void DetectAndPost(AK.Wwise.Event wwiseEvent)
        {
            var surface = DetectSurface();
            var sw      = mapping.Resolve(surface);
#if UNITY_EDITOR
            //Debug.Log($"[Footstep] DetectAndPost — surface: {(surface != null ? surface.name : "null(fallback)")} — switch valid: {sw.IsValid()} — event valid: {wwiseEvent.IsValid()}");
#endif
            sw.SetValue(gameObject);
            var playingId = wwiseEvent.Post(gameObject);
#if UNITY_EDITOR
            //Debug.Log($"[Footstep] Post result — playingId: {playingId} (0 = failed)");
#endif
        }

        private SurfaceType? DetectSurface()
        {
            var ray = new Ray(transform.position + Vector3.up * 0.1f, Vector3.down);
            if (Physics.Raycast(ray, out var hit, rayDistance, floorMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.TryGetComponent<Surface>(out var surface))
                    return surface.Type;
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var origin = transform.position + Vector3.up * 0.1f;
            var end    = origin + Vector3.down * rayDistance;

            var ray = new Ray(origin, Vector3.down);
            if (Physics.Raycast(ray, out var hit, rayDistance, floorMask, QueryTriggerInteraction.Ignore))
            {
                UnityEngine.Gizmos.color = Color.green;
                UnityEngine.Gizmos.DrawLine(origin, hit.point);
                UnityEngine.Gizmos.DrawSphere(hit.point, 0.04f);
            }
            else
            {
                UnityEngine.Gizmos.color = Color.red;
                UnityEngine.Gizmos.DrawLine(origin, end);
            }
        }
#endif
    }
}

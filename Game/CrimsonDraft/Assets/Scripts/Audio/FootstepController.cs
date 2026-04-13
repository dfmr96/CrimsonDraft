#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    public sealed class FootstepController : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event walkEvent   = new AK.Wwise.Event();
        [SerializeField] private AK.Wwise.Event runEvent    = new AK.Wwise.Event();
        [SerializeField] private string         switchGroup = "SurfaceType";

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
            if (rb.linearVelocity.sqrMagnitude < minSpeedSqr) return;
            DetectAndPost(walkEvent);
        }

        // Called by Animation Event on run clip (left and right foot contacts)
        public void OnRunStep()
        {
            if (rb.linearVelocity.sqrMagnitude < minSpeedSqr) return;
            DetectAndPost(runEvent);
        }

        private void DetectAndPost(AK.Wwise.Event wwiseEvent)
        {
            var surface = DetectSurface();
            var state   = mapping.Resolve(surface);
            AkSoundEngine.SetSwitch(switchGroup, state, gameObject);
            wwiseEvent.Post(gameObject);
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
    }
}

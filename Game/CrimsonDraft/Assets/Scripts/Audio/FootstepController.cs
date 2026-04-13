#nullable enable

using AK.Wwise;
using HorrorEngine;
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
        [SerializeField] private SurfaceTypeMapping mapping    = null!;
        [SerializeField] private SurfaceDetector    surfaceDet = null!;
        [SerializeField] private GroundDetector     groundDet  = null!;

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
            groundDet.Detect(transform.position);
            var state = mapping.Resolve(surfaceDet.CurrentSurface);
            AkSoundEngine.SetSwitch(switchGroup, state, gameObject);
            wwiseEvent.Post(gameObject);
        }
    }
}

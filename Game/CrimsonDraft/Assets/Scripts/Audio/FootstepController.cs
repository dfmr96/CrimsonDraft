#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    public sealed class FootstepController : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event  footstepEvent = new AK.Wwise.Event();
        [SerializeField] private AK.Wwise.Switch walkSwitch    = new AK.Wwise.Switch();
        [SerializeField] private AK.Wwise.Switch runSwitch     = new AK.Wwise.Switch();

        [Header("Motion Guard")]
        [SerializeField] private Rigidbody rb          = null!;
        [SerializeField] private float     minSpeedSqr = 0.05f;

        // Called by Animation Event on walk clip (left and right foot contacts)
        public void OnWalkStep()
        {
            if (rb.linearVelocity.sqrMagnitude < minSpeedSqr) return;
            Post(walkSwitch);
        }

        // Called by Animation Event on run clip (left and right foot contacts)
        public void OnRunStep()
        {
            if (rb.linearVelocity.sqrMagnitude < minSpeedSqr) return;
            Post(runSwitch);
        }

        private void Post(AK.Wwise.Switch speedSwitch)
        {
            speedSwitch.SetValue(gameObject);
            footstepEvent.Post(gameObject);
        }
    }
}

#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    // Must live on the same GameObject as the Animator — Animation Events only call
    // methods on components attached there, regardless of what's assigned below.
    // Add a "Step" Animation Event (Function: Step) at each footstep frame, in as
    // many clips as needed; every occurrence calls Step() and fires wwiseEvent.
    public sealed class FootstepAnimationTrigger : MonoBehaviour
    {
        [SerializeField] private Animator animator = null!;
        [SerializeField] private WwiseTrigger wwiseEvent = new();

        private void Reset() => animator = GetComponent<Animator>();

        private void Awake()
        {
            if (animator != null && animator.gameObject != gameObject)
                Debug.LogWarning($"[FootstepAnimationTrigger] Animator is on '{animator.gameObject.name}', " +
                                  $"but this component is on '{gameObject.name}' — Animation Events won't reach Step().", this);
        }

        public void Step() => wwiseEvent.Fire(gameObject);
    }
}

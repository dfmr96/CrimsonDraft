#nullable enable

using System.Collections;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class DoorTransitionController : MonoBehaviour
    {
        [SerializeField] private float animationTimeout = 5f;

        private RoomTransitionContext? context;
        private bool completed;

        private void Start()
        {
            this.context = Resources.Load<RoomTransitionContext>("RoomTransitionContext");

            if (this.context == null)
            {
                Debug.LogError("[DoorTransitionController] RoomTransitionContext not found in Resources.");
                return;
            }

            if (this.context.DoorPrefab == null)
            {
                Debug.LogError("[DoorTransitionController] DoorPrefab is null — calling NotifyComplete immediately.");
                this.context.NotifyComplete();
                return;
            }

            var door = Instantiate(this.context.DoorPrefab, transform);
            door.transform.localPosition = Vector3.zero;
            door.transform.localRotation = Quaternion.identity;

            var animator = door.GetComponent<Animator>();
            if (animator != null)
                animator.Play(0);

            StartCoroutine(TimeoutFallback());
        }

        public void OnAnimationComplete()
        {
            if (this.completed) return;
            this.completed = true;
            this.context?.NotifyComplete();
        }

        private IEnumerator TimeoutFallback()
        {
            yield return new WaitForSeconds(this.animationTimeout);

            if (!this.completed)
            {
                Debug.LogWarning("[DoorTransitionController] Animation timeout — forcing transition complete.");
                OnAnimationComplete();
            }
        }
    }
}

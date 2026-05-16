#nullable enable

using System.Collections;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class DoorTransitionController : MonoBehaviour
    {
        [SerializeField] private float      animationTimeout = 5f;
        [SerializeField] private GameObject? defaultDoorPrefab;

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

            var prefab = this.context.DoorPrefab ?? this.defaultDoorPrefab;

            if (prefab == null)
            {
                Debug.LogWarning("[DoorTransitionController] No door prefab — completing transition immediately.");
                this.context.NotifyComplete();
                return;
            }

            var door = Instantiate(prefab, transform);
            door.transform.localPosition = Vector3.zero;
            door.transform.localRotation = Quaternion.identity;

            var animator = door.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.gameObject.AddComponent<DoorAnimationRelay>().Init(this);
                animator.Play(0);
            }

            StartCoroutine(TimeoutFallback());
        }

        internal void OnAnimationComplete()
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

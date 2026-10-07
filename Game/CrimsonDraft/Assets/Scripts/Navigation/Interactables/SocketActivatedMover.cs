#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    // Moves this object to a fixed local position once the linked ItemSocketInteractable is
    // activated (e.g. the corpse dropping to the floor when its chain breaks).
    //
    // Polls IsActivated instead of subscribing to OnActivated on purpose: ItemSocketInteractable
    // restores an already-activated socket from the registry without replaying onActivated, so
    // an event-only hookup would leave the object in its old pose after revisiting the room.
    // If the socket is already activated the first time we look, the move is snapped; a
    // transition seen afterwards is animated.
    public sealed class SocketActivatedMover : MonoBehaviour
    {
        [SerializeField] private ItemSocketInteractable socket = null!;
        [SerializeField] private Vector3                localPositionOnActivate;
        [SerializeField, Min(0f)] private float          duration = 0.8f;

        private bool  firstCheck = true;
        private bool  moving;
        private float elapsed;
        private Vector3 from;

        void Update()
        {
            if (this.moving)
            {
                this.elapsed += Time.deltaTime;
                float t = this.duration <= 0f ? 1f : Mathf.Clamp01(this.elapsed / this.duration);
                // Ease-in: accelerates like something falling.
                float eased = t * t;
                transform.localPosition = Vector3.LerpUnclamped(this.from, this.localPositionOnActivate, eased);

                if (t >= 1f)
                {
                    this.moving = false;
                    enabled     = false;
                }
                return;
            }

            if (this.socket == null || !this.socket.IsActivated)
            {
                this.firstCheck = false;
                return;
            }

            if (this.firstCheck)
            {
                transform.localPosition = this.localPositionOnActivate;
                enabled = false;
                return;
            }

            this.from    = transform.localPosition;
            this.elapsed = 0f;
            this.moving  = true;
        }
    }
}

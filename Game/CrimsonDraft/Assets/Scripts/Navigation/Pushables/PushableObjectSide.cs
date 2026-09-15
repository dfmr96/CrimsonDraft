#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Pushables
{
    // Child trigger placed on one face of a PushableObject. Replaces contact-based detection
    // (OnCollisionStay), which could drop for a single frame due to a timing mismatch between
    // the player's velocity-integrated movement and the box's MovePosition-driven movement,
    // making the player flicker in and out of push mode. A trigger volume is forgiving by
    // design -- as long as the player's collider overlaps it, contact never drops.
    //
    // The push direction is resolved once at Awake from this trigger's own local offset from the
    // box's center (via PushableObject.ResolveAxis) -- place a child on a face and it already
    // knows which way that face pushes, no per-frame recomputation needed.
    [RequireComponent(typeof(Collider))]
    public sealed class PushableObjectSide : MonoBehaviour
    {
        private PushableObject parent        = null!;
        private Vector3        pushDirection;

        private void Awake()
        {
            this.parent = GetComponentInParent<PushableObject>();

            // ResolveAxis(boxPosition, playerPosition) points away from playerPosition -- this
            // trigger's own position stands in for "where the player is" (that's where they have
            // to be to enter it), so the box goes here, the trigger there.
            this.pushDirection = PushableObject.ResolveAxis(this.parent.transform.position, transform.position);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent(out PlayerController player))
                player.SetTouchingPushable(this.parent, this.pushDirection);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent(out PlayerController player))
                player.ClearTouchingPushable(this.parent);
        }
    }
}

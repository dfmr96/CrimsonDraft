#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.Navigation.Pushables
{
    // Contact-driven push mechanic (RE-classic style): walking into a tagged prop pushes it
    // along a world axis. Activation is via child PushableObjectSide triggers (one per face,
    // each with a fixed push direction resolved once at Awake via ResolveAxis below); per-stride
    // movement is driven from PlayerController.FixedUpdate, which calls TryStep. This component
    // owns only the prop's own logic: which axis each side pushes along, and whether a given
    // stride is clear. See docs/superpowers/specs/2026-09-15-pushable-objects-design.md.
    // No Rigidbody: a pushable prop doesn't need gravity or dynamics, only discrete positioning
    // validated by hand via Physics.OverlapBox below. Its solid BoxCollider still blocks the
    // player like any other static level collider -- a Collider needs no Rigidbody of its own
    // to do that; trigger events on the child PushableObjectSide colliders still fire too, since
    // the player's own Rigidbody satisfies Unity's "at least one side of the pair has one" rule.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PushableObject : MonoBehaviour
    {
        [SerializeField] private LayerMask obstructionMask;          // walls, other pushables
        [SerializeField] private Vector3   obstructionHalfExtents = new Vector3(0.4f, 0.4f, 0.4f);
        [SerializeField] private float     strideTweenDuration = 0.3f; // placeholder -- debe ser menor a PlayerController.pushStrideInterval para no solapar zancadas

        // PlayerController reads this to tween its own stride the exact same duration -- a
        // single source of truth so the two tweens can never drift out of sync with each other.
        public float StrideTweenDuration => this.strideTweenDuration;

        private BoxCollider ownCollider = null!;

        private void Awake()
        {
            this.ownCollider = GetComponent<BoxCollider>();
        }

        // Pure math -- no Unity physics/NavMesh involved. Dominant axis wins regardless of
        // contact angle, so a corner hit snaps to one axis instead of pushing diagonally.
        public static Vector3 ResolveAxis(Vector3 boxPosition, Vector3 playerPosition)
        {
            Vector3 delta = boxPosition - playerPosition;
            return Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)
                ? new Vector3(Mathf.Sign(delta.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(delta.z));
        }

        // Validates and, if clear, performs one step. Returns false (no movement) if the next
        // position overlaps something solid. No NavMesh check -- level geometry (walls) already
        // bounds where a pushable puzzle can go, and sampling the NavMesh from a box turned out
        // to be a persistent source of bugs (the obstacle's own carve hole, the pivot-vs-ground
        // height mismatch); physics obstruction alone is simpler and sufficient here.
        public bool TryStep(Vector3 axisDirection, float stepDistance)
        {
            Vector3 next = transform.position + axisDirection * stepDistance;

            // QueryTriggerInteraction.Ignore is required here -- Physics.OverlapBox includes
            // triggers by default, and this object's own PushableObjectSide triggers sit close
            // enough (one per face) that the opposite side's trigger falls inside the check
            // volume at the new position, self-blocking every single stride. Obstruction should
            // only ever mean solid geometry anyway (walls, other pushables' solid colliders).
            var hits = Physics.OverlapBox(next, this.obstructionHalfExtents, transform.rotation, this.obstructionMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit == this.ownCollider) continue;
                return false;
            }

            transform.DOKill();
            transform.DOMove(next, this.strideTweenDuration).SetEase(Ease.OutQuad);
            return true;
        }

#if UNITY_EDITOR
        // Push is always world-axis-locked (ResolveAxis only ever returns +-X or +-Z, never a
        // diagonal) -- these arrows show exactly the four directions a push can resolve to from
        // this object, regardless of which side the player approaches from.
        //
        // Also draws, from the parent so selecting the crate shows everything at once:
        // - the obstruction check volume TryStep uses (yellow), to see why a stride is/isn't
        //   blocked at the current position;
        // - each child PushableObjectSide's trigger bounds (green) and its resolved push
        //   direction (recomputed live here via ResolveAxis, not read from the side's own Awake
        //   -- so this is accurate even outside Play mode).
        private void OnDrawGizmosSelected()
        {
            const float axisLength = 1.5f;
            Vector3 center = transform.position;

            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(Vector3.right), axisLength, EventType.Repaint);
            UnityEditor.Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(Vector3.left), axisLength, EventType.Repaint);

            UnityEditor.Handles.color = Color.blue;
            UnityEditor.Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(Vector3.forward), axisLength, EventType.Repaint);
            UnityEditor.Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(Vector3.back), axisLength, EventType.Repaint);

            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, this.obstructionHalfExtents * 2f);
            Gizmos.matrix = Matrix4x4.identity;

            foreach (var side in GetComponentsInChildren<PushableObjectSide>())
            {
                if (side.TryGetComponent(out BoxCollider sideCollider))
                {
                    Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
                    Gizmos.matrix = side.transform.localToWorldMatrix;
                    Gizmos.DrawCube(sideCollider.center, sideCollider.size);
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(sideCollider.center, sideCollider.size);
                    Gizmos.matrix = Matrix4x4.identity;
                }

                Vector3 sideDirection = ResolveAxis(center, side.transform.position);
                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.ArrowHandleCap(0, side.transform.position, Quaternion.LookRotation(sideDirection), 0.5f, EventType.Repaint);
            }
        }
#endif
    }
}

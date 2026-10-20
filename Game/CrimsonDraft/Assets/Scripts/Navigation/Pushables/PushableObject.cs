#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.Navigation.Pushables
{
    // Contact-driven push mechanic (RE-classic style): walking into a tagged prop pushes it
    // along a world axis. Activation is geometric, from this prop's own BoxCollider bounds (see
    // PushDetector: touch, same floor, square to a face, moving into it); per-stride movement is
    // driven from PlayerController.FixedUpdate, which calls TryStep. This component owns only the
    // prop's own logic: its bounds and whether a given stride is clear. Props are only ever placed
    // at 90-degree rotations, so the collider's world bounds are exact.
    // See docs/superpowers/specs/2026-09-15-pushable-objects-design.md.
    // No Rigidbody: a pushable prop doesn't need gravity or dynamics, only discrete positioning
    // validated by hand via Physics.OverlapBox below. Its solid BoxCollider still blocks the
    // player like any other static level collider -- a Collider needs no Rigidbody of its own.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PushableObject : MonoBehaviour
    {
        [SerializeField] private LayerMask obstructionMask;          // walls, other pushables
        [SerializeField] private Vector3   obstructionHalfExtents = new Vector3(0.4f, 0.4f, 0.4f);
        [SerializeField] private float     strideTweenDuration = 0.3f; // placeholder -- debe ser menor a PlayerController.pushStrideInterval para no solapar zancadas

        // PlayerController reads this to tween its own stride the exact same duration -- a
        // single source of truth so the two tweens can never drift out of sync with each other.
        public float StrideTweenDuration => this.strideTweenDuration;

        // World-space bounds of the solid collider -- what PushDetector tests contact and faces against.
        public Bounds Bounds => this.ownCollider.bounds;

        private BoxCollider ownCollider = null!;

        private void Awake()
        {
            this.ownCollider = GetComponent<BoxCollider>();
        }

        // Validates and, if clear, performs one step. Returns false (no movement) if the next
        // position overlaps something solid. No NavMesh check -- level geometry (walls) already
        // bounds where a pushable puzzle can go, and sampling the NavMesh from a box turned out
        // to be a persistent source of bugs (the obstacle's own carve hole, the pivot-vs-ground
        // height mismatch); physics obstruction alone is simpler and sufficient here.
        public bool TryStep(Vector3 axisDirection, float stepDistance)
        {
            Vector3 next = transform.position + axisDirection * stepDistance;

            // QueryTriggerInteraction.Ignore: Physics.OverlapBox includes triggers by default, and
            // obstruction should only ever mean solid geometry (walls, other pushables' colliders),
            // never interaction or camera trigger volumes the prop happens to slide into.
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
        // Push is always world-axis-locked (PushDetector only ever resolves +-X or +-Z, never a
        // diagonal) -- these arrows show exactly the four directions a push can resolve to.
        //
        // Also draws:
        // - the obstruction check volume TryStep uses (yellow), to see why a stride is/isn't
        //   blocked at the current position;
        // - on the floor, the span of each face the player can push from (green) and the corner
        //   zones where pushing never starts (red), using PushDetector's default corner margin.
        private void OnDrawGizmosSelected()
        {
            const float axisLength = 1.5f;
            Vector3 center = transform.position;
            var boxCollider = GetComponent<BoxCollider>();

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

            if (boxCollider == null) return;

            Bounds  bounds  = boxCollider.bounds;
            Vector3 extents = bounds.extents;
            float   margin  = PushDetector.DefaultCornerMargin;
            float   floorY  = bounds.min.y + 0.01f;
            const float bandDepth = 0.3f;

            // Pushable face spans (green) and dead corners (red), as thin bands just outside each face.
            foreach (var sign in new[] { -1f, 1f })
            {
                var zFace = new Vector3(bounds.center.x, floorY, bounds.center.z + sign * (extents.z + bandDepth * 0.5f));
                var xFace = new Vector3(bounds.center.x + sign * (extents.x + bandDepth * 0.5f), floorY, bounds.center.z);

                Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
                Gizmos.DrawCube(zFace, new Vector3(Mathf.Max(0f, extents.x - margin) * 2f, 0.02f, bandDepth));
                Gizmos.DrawCube(xFace, new Vector3(bandDepth, 0.02f, Mathf.Max(0f, extents.z - margin) * 2f));

                Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
                foreach (var cornerSign in new[] { -1f, 1f })
                {
                    Gizmos.DrawCube(new Vector3(bounds.center.x + cornerSign * (extents.x - margin * 0.5f), floorY, zFace.z),
                                    new Vector3(margin, 0.02f, bandDepth));
                    Gizmos.DrawCube(new Vector3(xFace.x, floorY, bounds.center.z + cornerSign * (extents.z - margin * 0.5f)),
                                    new Vector3(bandDepth, 0.02f, margin));
                }
            }
        }
#endif
    }
}

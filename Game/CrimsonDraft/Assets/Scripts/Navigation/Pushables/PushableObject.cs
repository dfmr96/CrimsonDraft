#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Pushables
{
    // Contact-driven push mechanic (RE-classic style): walking into a tagged prop pushes it
    // along a world axis. Activation is geometric, from this prop's own BoxCollider bounds (see
    // PushDetector: touch, same floor, square to a face, moving into it); continuous movement is
    // driven from PlayerController.FixedUpdate, which calls TryMove every physics step while the
    // push lasts. This component owns only the prop's own logic: its bounds and whether a move
    // is clear. Props are only ever placed at 90-degree rotations, so the collider's world bounds
    // are exact. See docs/superpowers/specs/2026-09-15-pushable-objects-design.md.
    // No Rigidbody: a pushable prop doesn't need gravity or dynamics, only positioning validated
    // by hand via Physics.OverlapBox below. Its solid BoxCollider still blocks the player like any
    // other static level collider -- a Collider needs no Rigidbody of its own.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PushableObject : MonoBehaviour
    {
        [SerializeField] private LayerMask obstructionMask;              // walls, other pushables, enemies
        [SerializeField] private float     obstructionSkin = 0.01f;      // shrink on X/Z so resting against a wall isn't a hit
        [SerializeField] private float     floorClearance  = 0.1f;       // bottom slice ignored so the floor it rests on isn't a hit

        // World-space bounds of the solid collider -- what PushDetector tests contact and faces against.
        public Bounds Bounds => this.ownCollider.bounds;

        private readonly Collider[] hits = new Collider[8];
        private BoxCollider ownCollider = null!;

        private void Awake()
        {
            this.ownCollider = GetComponent<BoxCollider>();
        }

        // Moves the prop `distance` along `axis` if the moved bounds are clear, like RE2's object
        // loop (PC 0x4CC320): the whole move is rejected on any overlap, never partially applied.
        // `pusher` (the player's own collider) is ignored -- it is always right behind the face.
        // No NavMesh check -- level geometry (walls) already bounds where a pushable puzzle can go,
        // and sampling the NavMesh from a box turned out to be a persistent source of bugs (the
        // obstacle's own carve hole, the pivot-vs-ground height mismatch).
        public bool TryMove(Vector3 axis, float distance, Collider? pusher)
        {
            GetObstructionVolume(this.Bounds, out Vector3 center, out Vector3 halfExtents);
            center += axis * distance;

            // QueryTriggerInteraction.Ignore: Physics.OverlapBox includes triggers by default, and
            // obstruction should only ever mean solid geometry (walls, other pushables' colliders),
            // never interaction or camera trigger volumes the prop happens to slide into.
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, this.hits, Quaternion.identity,
                this.obstructionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = this.hits[i];
                if (hit == this.ownCollider || hit == pusher) continue;
                return false;
            }

            transform.position += axis * distance;
            return true;
        }

        private void GetObstructionVolume(Bounds bounds, out Vector3 center, out Vector3 halfExtents)
        {
            halfExtents = new Vector3(
                Mathf.Max(0f, bounds.extents.x - this.obstructionSkin),
                Mathf.Max(0f, bounds.extents.y - this.floorClearance * 0.5f),
                Mathf.Max(0f, bounds.extents.z - this.obstructionSkin));
            center = bounds.center + Vector3.up * (this.floorClearance * 0.5f);
        }

#if UNITY_EDITOR
        // Push is always world-axis-locked (PushDetector only ever resolves +-X or +-Z, never a
        // diagonal) -- these arrows show exactly the four directions a push can resolve to.
        //
        // Also draws:
        // - the obstruction volume TryMove tests (yellow), to see why a push is/isn't blocked;
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

            if (boxCollider == null) return;

            Bounds bounds = boxCollider.bounds;
            GetObstructionVolume(bounds, out Vector3 volumeCenter, out Vector3 volumeHalf);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(volumeCenter, volumeHalf * 2f);

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

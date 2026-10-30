#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Pushables
{
    // Push activation rule taken from Resident Evil 2 (PC LeonU.exe: object loop 0x4CC320, front check
    // 0x4CD2A0; see Design/Reference/RE2/navegacion/objetos-empujables.md), in world axes -- pushables
    // only ever sit at 90-degree rotations, so their collider bounds are exact:
    //   1. the player touches the box (closest point of the box within radius + skin, on XZ);
    //   2. the player stands at the box's floor level;
    //   3. the player is square to one face, not at a corner (inside the face span minus a margin);
    //   4. the move direction is within an angle limit of the axis pointing into that face --
    //      i.e. the player is moving into the face, not along it or away.
    // Pure math, no physics queries: PlayerController gathers the candidates and enforces RE2's
    // "exactly one pushable touched" rule before calling this.
    public static class PushDetector
    {
        // RE2 corner margin is 128 units (~0.13 m); 0.05 here because our player is wider relative to
        // the props, and 0.13 left too narrow a pushable span on the sides. RE2's probe (1.2 x half-width + 128 units ahead)
        // depended on the gap between player and box, so a diagonal input near its limit flickered in
        // and out of the push; here "moving into the face" is an angle limit instead: the move
        // direction must align with the push axis by at least this cosine (0.6 ~ 53 degrees).
        public const float DefaultCornerMargin     = 0.05f;
        public const float DefaultMinAlignment     = 0.6f;

        public static bool IsTouching(Vector3 playerPosition, float playerRadius, Bounds box, float contactSkin)
        {
            float closestX = Mathf.Clamp(playerPosition.x, box.min.x, box.max.x);
            float closestZ = Mathf.Clamp(playerPosition.z, box.min.z, box.max.z);
            float dx = playerPosition.x - closestX;
            float dz = playerPosition.z - closestZ;
            float reach = playerRadius + contactSkin;
            return dx * dx + dz * dz <= reach * reach;
        }

        public static bool IsSameFloor(float playerFeetY, Bounds box, float floorTolerance) =>
            Mathf.Abs(playerFeetY - box.min.y) <= floorTolerance;

        // Push axis (unit world X or Z, pointing away from the player) if the player is square to a
        // face and moving into it; false at corners and when moving along or away from the face.
        public static bool TryResolveAxis(
            Vector3 playerPosition,
            Vector3 moveDirection,
            Bounds  box,
            float   cornerMargin,
            float   minAlignment,
            out Vector3 axis)
        {
            axis = Vector3.zero;

            var move = new Vector3(moveDirection.x, 0f, moveDirection.z);
            if (move.sqrMagnitude < 1e-6f)
                return false;

            Vector3 offset  = playerPosition - box.center;
            Vector3 extents = box.extents;

            // Square to a +-Z face when within that face's width along X (and vice versa).
            bool squareToZFace = Mathf.Abs(offset.x) <= extents.x - cornerMargin;
            bool squareToXFace = Mathf.Abs(offset.z) <= extents.z - cornerMargin;
            if (!squareToZFace && !squareToXFace)
                return false; // corner

            // Only possible when the player is over the box itself: pick the face it is closer to
            // leaving through, relative to the box size.
            bool useZ = squareToZFace &&
                        (!squareToXFace ||
                         Mathf.Abs(offset.z) / Mathf.Max(extents.z, 1e-4f) >= Mathf.Abs(offset.x) / Mathf.Max(extents.x, 1e-4f));

            Vector3 candidate = useZ
                ? new Vector3(0f, 0f, offset.z <= 0f ? 1f : -1f)
                : new Vector3(offset.x <= 0f ? 1f : -1f, 0f, 0f);

            if (Vector3.Dot(move.normalized, candidate) < minAlignment)
                return false;

            axis = candidate;
            return true;
        }
    }
}

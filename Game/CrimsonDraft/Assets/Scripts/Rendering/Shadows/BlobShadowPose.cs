#nullable enable

using UnityEngine;

namespace CrimsonDraft.Rendering.Shadows
{
    public static class BlobShadowPose
    {
        private const float DegenerateAxisSqrThreshold = 1e-6f;
        private const float NearlyVerticalDot          = 0.99f;

        public static Pose FromGroundHit(Vector3 hitPoint, Vector3 groundNormal, Vector3 ownerRight, float surfaceBias)
        {
            var position = hitPoint + Vector3.up * surfaceBias;
            var forward  = ForwardOnSurface(groundNormal, ownerRight);
            return new Pose(position, Quaternion.LookRotation(forward, groundNormal));
        }

        public static Vector3 FootprintScale(Vector2 size, float pulseMultiplier)
            => new Vector3(size.x, 1f, size.y) * pulseMultiplier;

        private static Vector3 ForwardOnSurface(Vector3 groundNormal, Vector3 ownerRight)
        {
            var forward = Vector3.Cross(ownerRight, groundNormal);
            if (forward.sqrMagnitude > DegenerateAxisSqrThreshold)
                return forward.normalized;

            var fallbackReference = Mathf.Abs(Vector3.Dot(groundNormal, Vector3.up)) < NearlyVerticalDot ?Vector3.up : Vector3.forward;
            return Vector3.Cross(fallbackReference, groundNormal).normalized;
        }
    }
}

using UnityEngine;

namespace HorrorEngine
{
    public static class VectorExtensions
    {
        public static Vector2 ToXZ(this Vector3 v3)
        {
            return new Vector2(v3.x, v3.z);
        }

        public static float DistanceXZ(this Vector3 v3, Vector3 other)
        {
            return Vector3.Distance(new Vector3(v3.x, 0, v3.z), new Vector3(other.x, 0, other.z));
        }
    }
}
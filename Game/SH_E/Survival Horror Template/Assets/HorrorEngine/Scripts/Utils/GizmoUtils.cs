using UnityEngine;

namespace HorrorEngine
{
    public static class GizmoUtils
    {

        public static void DrawCross(Vector3 position, Vector3 right, Vector3 up, Vector3 forward, float Size)
        {
            Gizmos.DrawLine(position - right * Size * 0.5f, position + right * Size * 0.5f);
            Gizmos.DrawLine(position - forward * Size * 0.5f, position + forward * Size * 0.5f);
            Gizmos.DrawLine(position - up * Size * 0.5f, position + up * Size * 0.5f);
        }

        public static void DrawArrow(Vector3 position, Vector3 forward, float Size, float TipSize = 0)
        {
            if (TipSize == 0)
                TipSize = Size;

            forward.Normalize();
            Gizmos.DrawLine(position, position + forward * Size);

            Vector3 cross = Vector3.Cross(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.forward)) == 1 ? Vector3.up : Vector3.forward).normalized;
            Vector3 perpendicular1 = Vector3.Cross(forward, cross);

            Vector3 perpendicular2 = Vector3.Cross(forward, perpendicular1);

            float dist = Size - TipSize;
            Vector3 p1 = position + (forward * dist - perpendicular1 * TipSize * 0.5f);
            Vector3 p2 = position + (forward * dist + perpendicular1 * TipSize * 0.5f);
            Vector3 p3 = position + (forward * dist - perpendicular2 * TipSize * 0.5f);
            Vector3 p4 = position + (forward * dist + perpendicular2 * TipSize * 0.5f);
            Gizmos.DrawLine(position + forward * Size, p1);
            Gizmos.DrawLine(position + forward * Size, p2);
            Gizmos.DrawLine(position + forward * Size, p3);
            Gizmos.DrawLine(position + forward * Size, p4);

            Gizmos.DrawLine(p3, p2);
            Gizmos.DrawLine(p2, p4);
            Gizmos.DrawLine(p1, p3);
            Gizmos.DrawLine(p1, p4);
        }

        public static void DrawWireCapsule(Matrix4x4 space, Vector3 upper, Vector3 lower, float radius)
        {
            Gizmos.matrix = space;

            Gizmos.DrawWireSphere(upper, radius);
            Gizmos.DrawLine(upper + Vector3.right * radius, lower + Vector3.right * radius);
            Gizmos.DrawLine(upper + Vector3.forward * radius, lower + Vector3.forward * radius);
            Gizmos.DrawLine(upper - Vector3.right * radius, lower - Vector3.right * radius);
            Gizmos.DrawLine(upper - Vector3.forward * radius, lower - Vector3.forward * radius);
            Gizmos.DrawWireSphere(lower, radius);

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
using System.Drawing;
using UnityEngine;

namespace HorrorEngine
{
    public class DebugUtils : MonoBehaviour
    {
        public static int Segments = 8;

        public static void DrawCross(Vector3 position, Vector3 right, Vector3 up, Vector3 forward, float Size, UnityEngine.Color c, float duration)
        {
            Debug.DrawLine(position - right * Size * 0.5f, position + right * Size * 0.5f, c, duration);
            Debug.DrawLine(position - forward * Size * 0.5f, position + forward * Size * 0.5f, c, duration);
            Debug.DrawLine(position - up * Size * 0.5f, position + up * Size * 0.5f, c, duration);
        }

        public static void DrawBox(Vector3 pos, Quaternion rot, Vector3 size, Vector3 scale, UnityEngine.Color c, float duration)
        {
            // create matrix
            Matrix4x4 m = new Matrix4x4();
            m.SetTRS(pos, rot, scale);

            Vector3 hsize = size * 0.5f;
            var point1 = m.MultiplyPoint(new Vector3(hsize.x, -hsize.y, hsize.z));
            var point2 = m.MultiplyPoint(new Vector3(-hsize.x, -hsize.y, hsize.z));
            var point3 = m.MultiplyPoint(new Vector3(-hsize.x, -hsize.y, -hsize.z));
            var point4 = m.MultiplyPoint(new Vector3(hsize.x, -hsize.y, -hsize.z));

            var point5 = m.MultiplyPoint(new Vector3(hsize.x, hsize.y, hsize.z));
            var point6 = m.MultiplyPoint(new Vector3(-hsize.x, hsize.y, hsize.z));
            var point7 = m.MultiplyPoint(new Vector3(-hsize.x, hsize.y, -hsize.z));
            var point8 = m.MultiplyPoint(new Vector3(hsize.x, hsize.y, -hsize.z));

            Debug.DrawLine(point1, point2, c, duration);
            Debug.DrawLine(point2, point3, c, duration);
            Debug.DrawLine(point3, point4, c, duration);
            Debug.DrawLine(point4, point1, c, duration);

            Debug.DrawLine(point5, point6, c, duration);
            Debug.DrawLine(point6, point7, c, duration);
            Debug.DrawLine(point7, point8, c, duration);
            Debug.DrawLine(point8, point5, c, duration);

            Debug.DrawLine(point1, point5, c, duration);
            Debug.DrawLine(point2, point6, c, duration);
            Debug.DrawLine(point3, point7, c, duration);
            Debug.DrawLine(point4, point8, c, duration);
        }

        public static void DrawCollider(Collider collider, UnityEngine.Color color, float duration)
        {
            if (collider is BoxCollider boxCollider)
            {
                DrawBox(boxCollider.transform.TransformPoint(boxCollider.center), boxCollider.transform.rotation, boxCollider.size, boxCollider.transform.lossyScale, color, duration);
            }
            else if (collider is SphereCollider sphereCollider)
            {
                DrawSphere(sphereCollider.transform.TransformPoint(sphereCollider.center), sphereCollider.transform.rotation, sphereCollider.transform.lossyScale, sphereCollider.radius, color, duration);
            }
            else if (collider is CapsuleCollider capsuleCollider)
            {
                DrawCapsule(capsuleCollider.transform.TransformPoint(capsuleCollider.center), capsuleCollider.transform.rotation, capsuleCollider.transform.lossyScale, capsuleCollider.radius, capsuleCollider.height, color, duration);
            }
            else
            {
                Debug.LogWarning("Collider type not supported for drawing.");
            }
        }

        public static void DrawSphere(Vector3 pos, Quaternion rot, Vector3 scale, float radius, UnityEngine.Color color, float duration)
        {
            // Draw horizontal circle (XZ plane)
            DrawCircle(pos, rot, scale, radius, color, duration);

            // Rotate 90 degrees around X-axis to draw the vertical circle (YZ plane)
            Quaternion rotX = rot * Quaternion.Euler(90, 0, 0);
            DrawCircle(pos, rotX, scale, radius, color, duration);

            // Rotate 90 degrees around Z-axis to draw the vertical circle (XZ plane)
            Quaternion rotZ = rot * Quaternion.Euler(0, 0, 90);
            DrawCircle(pos, rotZ, scale, radius, color, duration);
        }

        public static void DrawCapsule(Vector3 pos, Quaternion rot, Vector3 scale, float radius, float height, UnityEngine.Color color, float duration)
        {
            // create matrix
            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetTRS(pos, rot, scale);

            Vector3 top = new Vector3(0, height / 2 - radius, 0);
            Vector3 bottom = new Vector3(0, -height / 2 + radius, 0);
            top = matrix.MultiplyPoint3x4(top);
            bottom = matrix.MultiplyPoint3x4(bottom);

            DrawSphere(top, rot, scale, radius, color, duration);
            DrawSphere(bottom, rot, scale, radius, color, duration);

            Debug.DrawLine(top, bottom, color);

            int segments = 4;
            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep;
                Quaternion segmentRot = rot * Quaternion.Euler(0, angle, 0);
                Vector3 p1 = top + segmentRot * Vector3.forward * radius;
                Vector3 p2 = bottom + segmentRot * Vector3.forward * radius;
                Debug.DrawLine(p1, p2, color, duration);
            }
        }
        private static void DrawCircle(Vector3 pos, Quaternion rot, Vector3 scale, float radius, UnityEngine.Color color, float duration)
        {
            // create matrix
            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetTRS(pos, rot, scale);

            int segments = Segments;
            float angleStep = Mathf.PI * 2 / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;

                Vector3 p1 = new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 p2 = new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

                p1 = matrix.MultiplyPoint3x4(p1);
                p2 = matrix.MultiplyPoint3x4(p2);

                Debug.DrawLine(p1, p2, color, duration);
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    public class Shape : MonoBehaviour
    {
        public Color GizmoColor = Color.white;
        public bool DrawGizmo = true;

        public List<Vector3> Points = new List<Vector3>(){
            new Vector3(-1,0,-1),
            new Vector3(-1, 0,1),
            new Vector3(1, 0, 1),
            new Vector3(1, 0, -1)
            };

        public bool CloseShape = true;

        public Vector3 GetWorldPoint(int index)
        {
            Debug.Assert(index >= 0 && index < Points.Count, "GetWorldPoint: index was out of range");

            return transform.TransformPoint(Points[index]);
        }

        public Vector3 GetPointTangent(int index)
        {
            Debug.Assert(index >= 0 && index < Points.Count-1, "GetPointTangent: index was out of range or not enough points in shape");

            if (Points.Count > 1)
            {
                return (Points[index + 1] - Points[index]).normalized;
            }

            return Vector3.zero;
        }

        public Vector3 GetWorldPointAtT(float t)
        {
            Debug.Assert(t >= 0 && t <= 1, "GetWorldPointAtT: t was out of range");

            if (Points.Count > 1)
            {
                if (t <= 0f)
                    return GetWorldPoint(0);
                if (t >= 1f)
                    return GetWorldPoint(Points.Count - 1);

                float totalD = GetLength();
                if (totalD > 0f)
                {
                    float currentD = 0;
                    for (int i = 0; i < Points.Count - 1; ++i)
                    {
                        Vector3 p1 = GetWorldPoint(i);
                        Vector3 p2 = GetWorldPoint(i + 1);
                        float d = Vector3.Distance(p1, p2);
                        float toD = currentD + d;

                        float toT = toD / totalD;
                        if (t <= toT) // Target T is in current range
                        {
                            float fromT = currentD / totalD;
                            float intervalT = MathUtils.Map(t, fromT, toT, 0f, 1f);
                            return Vector3.Lerp(p1, p2, intervalT);
                        }

                        currentD += d;
                    }
                }
            }

            return GetWorldPoint(0);
        }

        public float GetLength()
        {
            float l = 0f;
            if (Points.Count > 1)
            {
                for (int i = 1; i < Points.Count; ++i)
                {
                    l += Vector3.Distance(GetWorldPoint(i-1), GetWorldPoint(i));
                }
            }
            return l;
        }

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            if (DrawGizmo)
            {
                Handles.color = GizmoColor;
                for (int i = 0; i < Points.Count; i++)
                {
                    Vector3 position = transform.TransformPoint(Points[i]);

                    if (i > 0)
                    {
                        Vector3 prevPosition = transform.TransformPoint(Points[i - 1]);
                        Handles.DrawLine(position, prevPosition);
                    }
                }

                if (Points.Count >= 3 && CloseShape)
                {
                    Vector3 p1 = transform.TransformPoint(Points[Points.Count - 1]);
                    Vector3 p2 = transform.TransformPoint(Points[0]);
                    Handles.DrawLine(p1, p2);
                }
            }
        }
#endif

    }
}
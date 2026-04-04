
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;


namespace HorrorEngine
{
    [RequireComponent(typeof(Shape))]
    public class MatchShapeToCamera : MonoBehaviour
    {
#if UNITY_6000_0_OR_NEWER
        [SerializeField] CinemachineCamera m_Camera;
#else
        [SerializeField] CinemachineVirtualCamera m_Camera;
#endif
        [SerializeField] float m_Length = 10;


        private void OnValidate()
        {
            MatchShape();
        }

        private void MatchShape()
        {
            var shape = GetComponent<Shape>();

            if (shape != null && m_Camera != null)
            {
#if UNITY_6000_0_OR_NEWER
                float fov = m_Camera.Lens.FieldOfView;
#else
                float fov = m_Camera.m_Lens.FieldOfView;
#endif
                float aspect = 14 / 9f;
                float halfHeightFar = m_Length * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);   
                float halfWidthFar = halfHeightFar * aspect;

                Transform vcamTransform = m_Camera.transform;

                Vector3 localLFar = Vector3.right * -halfWidthFar + Vector3.forward * m_Length;
                Vector3 localRFar = Vector3.right * halfWidthFar + Vector3.forward * m_Length;

                Vector3 worldLFar = vcamTransform.TransformPoint(localLFar);
                Vector3 worldRFar = vcamTransform.TransformPoint(localRFar);
                
                
                Vector3 localCentre = transform.InverseTransformPoint(vcamTransform.position);
                Vector3 localShapeLFar = transform.InverseTransformPoint(worldLFar);
                Vector3 localShapeRFar = transform.InverseTransformPoint(worldRFar);

                List<Vector3> points = new List<Vector3>
                {
                    new Vector3(localCentre.x, localCentre.y, localCentre.z),
                    new Vector3(localShapeLFar.x, localCentre.y, localShapeLFar.z),
                    new Vector3(localShapeRFar.x, localCentre.y, localShapeRFar.z)
                };

                shape.Points = points;

                var shapeCol = GetComponent<ShapeCollider>();
                shapeCol?.UpdateCollider();
            }

        }

    }
}
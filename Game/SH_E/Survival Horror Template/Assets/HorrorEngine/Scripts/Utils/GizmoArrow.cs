using UnityEngine;

namespace HorrorEngine
{
    public enum ArrowAxis
    {
        Fwd,
        Bwd,
        Up,
        Down,
        Right,
        Left
    }

    public class GizmoArrow : MonoBehaviour
    {
        [SerializeField]
        private float m_ArrowLength = 1f;
        [SerializeField]
        private float m_ArrowTipSize = 1f;

        [SerializeField]
        private Color m_ArrowColor = Color.red;

        [SerializeField]
        private ArrowAxis m_Axis;

        private void OnDrawGizmos()
        {
            Gizmos.color = m_ArrowColor;
            GizmoUtils.DrawArrow(transform.position, GetArrowAxis(), m_ArrowLength, m_ArrowTipSize);
        }

        private Vector3 GetArrowAxis()
        {
            switch (m_Axis)
            {
                case ArrowAxis.Fwd:
                    return transform.forward;
                case ArrowAxis.Bwd:
                    return -transform.forward;
                case ArrowAxis.Up:
                    return transform.up;
                case ArrowAxis.Down:
                    return -transform.up;
                case ArrowAxis.Right:
                    return transform.right;
                case ArrowAxis.Left:
                    return -transform.right;
                default:
                    return transform.forward;
            }
        }

    }
}
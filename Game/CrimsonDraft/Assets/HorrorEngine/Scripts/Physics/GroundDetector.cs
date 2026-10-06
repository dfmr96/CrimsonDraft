using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [System.Serializable]
    public class OnGroundChangedEvent : UnityEvent<Collider> { }
    public class GroundDetector : MonoBehaviour
    {
        [SerializeField] float m_OffsetUp = 1f;
        [SerializeField] float m_Distance = 2f;
        [SerializeField] LayerMask m_GroundCheckLayerMask;

        public OnGroundChangedEvent OnGroundChanged = new OnGroundChangedEvent();

        public Vector3 Normal { get; private set; }
        public Vector3 Position { get; private set; }
        public Collider Collider { get; private set; }

        public bool Detect(Vector3 position)
        {
            if (Physics.Raycast(new Ray(position + Vector3.up * m_OffsetUp, Vector3.down), out RaycastHit hitRC, m_Distance, m_GroundCheckLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hitRC.collider != Collider)
                {
                    Collider = hitRC.collider;
                    OnGroundChanged?.Invoke(Collider);
                }

                Normal = hitRC.normal;
                Position = hitRC.point;
                return true;
            }
            
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * m_OffsetUp, transform.position + Vector3.up * m_OffsetUp + Vector3.down * m_Distance);
            Gizmos.color = Color.white;
        }
    }
}
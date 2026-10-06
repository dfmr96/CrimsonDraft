using UnityEngine;

namespace HorrorEngine
{
    public class SightCheck : MonoBehaviour
    {
        [SerializeField] private LayerMask m_TargetMask;
        [SerializeField] private LayerMask m_SightBlockerMask;
        [SerializeField] private Transform m_SightPoint;
        [SerializeField] private float m_MaxDistance = 100;
        [SerializeField] private Vector3 m_Offset;

        public bool IsInSight(Transform target)
        {
            Vector3 targetPos = target.position;
            return IsInSight(targetPos + m_Offset);
        }

        public bool IsInSight(Vector3 position)
        {
            // Check player visibility
            var dist = Mathf.Min(Vector3.Distance(m_SightPoint.position, position), m_MaxDistance);
            var dirToPlayer = (position - m_SightPoint.position).normalized;

            // First throw a ray to check sight blockers in the path and then a sight check with the proper mask (these mask are different)
            Physics.Raycast(new Ray(m_SightPoint.position, dirToPlayer), out RaycastHit blockerHit, dist, m_SightBlockerMask, QueryTriggerInteraction.Ignore);
            if (Physics.Raycast(new Ray(m_SightPoint.position, dirToPlayer), out RaycastHit sightHit, dist, m_TargetMask, QueryTriggerInteraction.Collide))
            {
                float distanceDiff = blockerHit.distance - sightHit.distance;
                if (!blockerHit.collider || blockerHit.distance > sightHit.distance || Mathf.Abs(distanceDiff) < Mathf.Epsilon)
                {
                    Debug.DrawLine(m_SightPoint.position, position, Color.green);
                    return true;
                }
            }
            else
            {
                Debug.DrawLine(m_SightPoint.position, position, Color.red);
                Debug.DrawLine(m_SightPoint.position, m_SightPoint.position + dirToPlayer * dist, Color.magenta);
            }

            

            return false;
        }
    }
}
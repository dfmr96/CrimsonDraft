using UnityEngine;

namespace HorrorEngine
{
    public class PlayerLookAtLookable : MonoBehaviour, IDeactivateWithActor
    {
        [SerializeField] float m_WeightSpeed = 1f;
        [SerializeField] float m_MaxAngle = 35f;
        [SerializeField] Transform m_SightPoint;
        [SerializeField] Transform m_LookAtPoint;

        public float LookIntensity = 1f;

        [Header("Detection Capsule")]
        [SerializeField] Vector3 m_Offset = Vector3.forward;
        [SerializeField] float m_Size = 1f;
        [SerializeField] float m_Radius = 1f;
        [SerializeField] LayerMask m_Mask;

        private Animator m_Animator;
        private float m_Weight;
        private Vector3 m_FocusPos;

        private Lookable m_Current;
        private float m_CheckInterval = 0.5f;
        private float m_LastCheckTime = 0f;
        private Collider[] m_Colliders = new Collider[3];

        private Lookable m_Override;

        // --------------------------------------------------------------------

        void Awake()
        {
            m_Animator = GetComponentInChildren<Animator>();
            m_LookAtPoint.position = m_SightPoint.position + transform.forward;
        }


        // --------------------------------------------------------------------

        private void OnDisable()
        {
            m_Current = null;
            m_FocusPos = m_SightPoint.position + transform.forward;
            m_LookAtPoint.position = m_FocusPos;
            m_Weight = 0f;
        }

        // --------------------------------------------------------------------

        void OnAnimatorIK(int layerIndex)
        {
            if (!m_Override)
                UpdateCurrentLookable();
            else
                m_Current = m_Override;

            bool lookingAtSomething = false;
            if (m_Current)
            {
                m_FocusPos = m_Current.LookPosition;
                m_Weight = Mathf.MoveTowards(m_Weight, LookIntensity, Time.deltaTime * m_WeightSpeed);

                float distanceToTarget = Vector3.Distance(m_SightPoint.position, m_FocusPos);
                Vector3 dirToLook = (m_FocusPos - m_SightPoint.position) / Mathf.Max(Mathf.Epsilon, distanceToTarget);
                
                float angle = Vector3.Angle(m_SightPoint.forward, dirToLook);
                lookingAtSomething = angle < 90f;
                if (lookingAtSomething)
                {
                    if (angle > m_MaxAngle) // Adjust look direction so it does not go beyond max angle
                    {
                        dirToLook = Vector3.RotateTowards(dirToLook, m_SightPoint.forward, Mathf.Deg2Rad * (angle - m_MaxAngle), 1f);
                        m_FocusPos = m_SightPoint.position + dirToLook * distanceToTarget;
                    }
                }
            }
            else
            {
                lookingAtSomething = false;
            }

            if (!lookingAtSomething)
            {
                m_FocusPos = m_SightPoint.position + transform.forward;
                m_Weight = Mathf.MoveTowards(m_Weight, 0f, Time.deltaTime* m_WeightSpeed);
            }

            m_LookAtPoint.position = Vector3.MoveTowards(m_LookAtPoint.position, m_FocusPos, Time.deltaTime * m_WeightSpeed);

            m_Animator.SetLookAtWeight(m_Weight);
            m_Animator.SetLookAtPosition(m_LookAtPoint.position);
        }

        // --------------------------------------------------------------------

        private void UpdateCurrentLookable()
        {
            if (Time.time > m_LastCheckTime + m_CheckInterval)
            {
                Vector3 up = Vector3.forward * m_Offset.z + Vector3.right * m_Size * 0.5f + Vector3.up* m_Offset.y;
                Vector3 down = Vector3.forward * m_Offset.z - Vector3.right * m_Size * 0.5f + Vector3.up * m_Offset.y;

                m_Current = null;
                if (Physics.OverlapCapsuleNonAlloc(transform.TransformPoint(up), transform.TransformPoint(down), m_Radius, m_Colliders, m_Mask, QueryTriggerInteraction.Collide) > 0)
                {
                    foreach (var col in m_Colliders)
                    {
                        if (col)
                        {
                            Lookable lookable = col.GetComponentInChildren<Lookable>();
                            if (lookable && (!m_Current || lookable.Priority > m_Current.Priority))
                            {
                                m_Current = lookable;
                            }
                        }
                    }
                }

                m_LastCheckTime = Time.time;
            }
        }

        // --------------------------------------------------------------------

        public void SetOverride(Lookable lookable)
        {
            m_Override = lookable;
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            Vector3 up = Vector3.forward * m_Offset.z + Vector3.right * m_Size * 0.5f + Vector3.up * m_Offset.y;
            Vector3 down = Vector3.forward * m_Offset.z - Vector3.right * m_Size * 0.5f + Vector3.up * m_Offset.y;

            GizmoUtils.DrawWireCapsule(transform.localToWorldMatrix, up, down, m_Radius);
        }
    }
}
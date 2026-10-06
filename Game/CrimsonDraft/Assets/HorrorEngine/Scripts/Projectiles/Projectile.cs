using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] Vector3 m_Gravity = Physics.gravity;

        public UnityEvent OnLaunch;
        public UnityEvent<Collision> OnStop;

        private Vector3 m_CurrentVelocity;
        private Rigidbody m_Rigidbody;
        private Collider m_Collider;

        private int m_LastFixedUpdateFrame;
        private int m_StopFrame;
        private Collision m_StoppedByCollision;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Collider = GetComponent<Collider>();
            m_Rigidbody = GetComponent<Rigidbody>();
            Debug.Assert(m_Rigidbody, "Projectiles need a Rigidbody component attached");
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            m_StoppedByCollision = null;
            m_Rigidbody.isKinematic = false;
            m_Collider.enabled = true;
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            m_Rigidbody.isKinematic = true;
            m_Collider.enabled = false;
            m_StoppedByCollision = null;
        }

        // --------------------------------------------------------------------

        public void Launch(Vector3 initialVelocity)
        {
            m_CurrentVelocity = initialVelocity;
            enabled = true;

            OnLaunch?.Invoke();
        }

        // --------------------------------------------------------------------

        private void FixedUpdate()
        {
            if (m_StoppedByCollision != null)
            {
                if (m_LastFixedUpdateFrame > m_StopFrame)
                {
                    OnStop?.Invoke(m_StoppedByCollision);
                    enabled = false;
                }

                m_LastFixedUpdateFrame = Time.frameCount;
            }
            else
            {
                m_LastFixedUpdateFrame = Time.frameCount;
                m_CurrentVelocity += m_Gravity * Time.deltaTime;
                m_Rigidbody.MovePosition(m_Rigidbody.position + m_CurrentVelocity * Time.deltaTime);
            }
        }

        // --------------------------------------------------------------------

        private void OnCollisionEnter(Collision collision)
        {
            m_StopFrame = Time.frameCount;
            m_StoppedByCollision = collision; // Stop does't happen immediately. A FixedUpdate is left inbetween to allow other systems to act at the current position (e.g. An attack Hitbox check)
        }
    }
}
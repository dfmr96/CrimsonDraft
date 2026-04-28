using System;
using UnityEngine;

namespace HorrorEngine
{
    public class PushDetector : MonoBehaviour
    {
        public PlayerMovement m_PlayerMovement;
        [SerializeField] private float m_PushTimeThreshold = 1f;
        [SerializeField] private float m_PushIntentDotThreshold = 0.5f;

        private float m_PushTime;
        private Action<OnDisableNotifier> m_OnPushableDisabled;
        private Pushable m_Pushable;
        
        public Pushable Pushable => m_Pushable;
        public Vector3 PushAxis { get; private set; }
        public bool IsPushing => m_PushTime > m_PushTimeThreshold && m_Pushable.CanBePushed;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_OnPushableDisabled = OnPushableDisabled;
            enabled = false;
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            m_PushTime = 0f;
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            m_PushTime = 0f;
            if (m_Pushable)
            {
                var disableNotif = m_Pushable.GetComponent<OnDisableNotifier>();
                if (disableNotif)
                    disableNotif.RemoveCallback(m_OnPushableDisabled);

                m_Pushable = null;
            }
        }

        // --------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out Pushable pushable))
            {
                m_Pushable = pushable;
                enabled = true;

                var disableNotif = other.GetComponent<OnDisableNotifier>();
                if (disableNotif)
                    disableNotif.AddCallback(m_OnPushableDisabled);
            }
        }

        // --------------------------------------------------------------------

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out Pushable pushable) && pushable == m_Pushable)
            {
                enabled = false;

                var disableNotif = other.GetComponent<OnDisableNotifier>();
                if (disableNotif)
                    disableNotif.RemoveCallback(m_OnPushableDisabled);
            }
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            Vector3 dirToObj = m_Pushable.transform.position - transform.position;
            dirToObj.y = 0;
            dirToObj.Normalize();

            Vector3 intendedMov = m_PlayerMovement.IntendedMovement;
            float intendedMovSpeed = intendedMov.magnitude;
            
            intendedMov.y = 0;
            intendedMov.Normalize();

            Debug.DrawLine(transform.position, transform.position + dirToObj, Color.red);
            Debug.DrawLine(transform.position, transform.position + intendedMov, Color.magenta);

            float intent = Vector3.Dot(dirToObj, intendedMov);
            if (intent > m_PushIntentDotThreshold)
            {
                bool wasPushing = IsPushing;
                m_PushTime += Time.deltaTime;
                if (IsPushing && !wasPushing)
                {
                    PushAxis = m_Pushable.GetPushingAxis(intendedMov, out bool foundAxis);
                    if (!foundAxis)
                    {
                        Debug.Log("Push tried to start but pushing axis couldn't be found");
                        m_PushTime = 0;
                    }
                }
            }
            else
            {
                m_PushTime = 0f;
            }
        }

        // --------------------------------------------------------------------

        private void OnPushableDisabled(OnDisableNotifier pushable)
        {
            pushable.RemoveCallback(m_OnPushableDisabled);
            m_Pushable = null;
            enabled = false;
        }
    }
}

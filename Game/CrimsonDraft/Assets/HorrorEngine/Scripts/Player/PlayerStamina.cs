using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class PlayerStamina : MonoBehaviour
    {
        [SerializeField] float m_MaxStamina = 1f;
        [SerializeField] float m_DepletionPerSecond = 0.1f;
        [SerializeField] float m_RestorationPerSecond = 0.1f;
        [SerializeField] bool m_ShowDebug;

        private float m_CurrentStamina;
        private bool m_CanDeplete;
        private float m_DisallowedTime;
        public UnityEvent OnStaminaDepleted;
        public UnityEvent OnStaminaFullyRestored;
        public UnityEvent OnDepletionReallowed;

        private float m_LastDepletedFrame;

        public float NormalizedStamina => m_CurrentStamina / Mathf.Max(m_MaxStamina, Mathf.Epsilon);

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_CurrentStamina = m_MaxStamina;
            m_CanDeplete = true;
        }

        // --------------------------------------------------------------------

        public bool Deplete(float rate)
        {
            if (!m_CanDeplete)
                return false;

            bool hadStamina = m_CurrentStamina > 0;

            m_CurrentStamina -= Time.deltaTime * m_DepletionPerSecond * rate;
            m_CurrentStamina = Mathf.Max(m_CurrentStamina, 0f);

            bool staminaDepleted = m_CurrentStamina == 0f;

            if (hadStamina && staminaDepleted)
            {
                OnStaminaDepleted?.Invoke();
            }

            m_LastDepletedFrame = Time.frameCount;

            if (m_ShowDebug)
            {
                Debug.Log("CONSUME: " + m_CurrentStamina);
            }

            return !staminaDepleted;
        }

        // --------------------------------------------------------------------

        private void LateUpdate()
        {
            if (m_DisallowedTime > 0)
            {
                m_DisallowedTime -= Time.deltaTime;
                if (m_DisallowedTime <= 0 && !m_CanDeplete)
                {
                    m_CanDeplete = true;
                    OnDepletionReallowed?.Invoke();
                }
            }

            if (Time.frameCount != m_LastDepletedFrame)
            {
                bool wasDepleted = m_CurrentStamina < m_MaxStamina;
                m_CurrentStamina += Time.deltaTime * m_RestorationPerSecond;
                m_CurrentStamina = Mathf.Min(m_CurrentStamina, m_MaxStamina);

                bool isMaxed = m_CurrentStamina >= m_MaxStamina;
                if (wasDepleted && isMaxed) 
                {
                    OnStaminaFullyRestored?.Invoke();
                }

                if (m_ShowDebug) 
                {
                    Debug.Log("RESTORE: " + m_CurrentStamina);
                }
            }

            if (m_ShowDebug)
            {
                for (int i = 0; i < 3; ++i)
                {
                    Vector3 camRight = Camera.main.transform.right;
                    Vector3 barStart = transform.position - camRight * 0.5f + Vector3.up * (2 + (0.01f * i));
                    Vector3 barEnd = transform.position + camRight * 0.5f + Vector3.up * (2 + (0.01f * i));
                    Vector3 barStaminaEnd = Vector3.Lerp(barStart, barEnd, NormalizedStamina);

                    Debug.DrawLine(barStart, barEnd, Color.black);
                    Debug.DrawLine(barStart, barStaminaEnd, Color.green);
                }
            }
        }

        // --------------------------------------------------------------------

        public void SetDepletionAllowed(bool allowed)
        {
            m_CanDeplete = allowed;
        }

        // --------------------------------------------------------------------

        public void DisallowDepletionForTime(float time)
        {
            m_CanDeplete = false;
            m_DisallowedTime = time;
        }
    }
}
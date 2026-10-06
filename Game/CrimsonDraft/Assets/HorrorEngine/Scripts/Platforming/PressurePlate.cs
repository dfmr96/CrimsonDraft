using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class PressurePlate : MonoBehaviour, ISavableObjectStateExtra
    {
        [Tooltip("Indicates how much weight is needed to activate the pressure plate")]
        [SerializeField] float m_WeightThreshold = 1f;
        [Tooltip("Indicates if the pressure plate can be activated more than once. This value is saved")]
        [SerializeField] bool m_ActivateOnlyOnce;

        public UnityEvent OnActivate;
        public UnityEvent OnDeactivate;

        private bool m_Activated;
        private float m_CurrentWeight;
        private Action<OnDisableNotifier> m_OnWeightDisabled;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_OnWeightDisabled = OnWeightDisabled;
        }

        // --------------------------------------------------------------------

        private void OnWeightDisabled(OnDisableNotifier notifier)
        {
            if (notifier.TryGetComponent(out Weight weight))
            {
                RemoveWeight(weight);
            }
        }

        // --------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out Weight weight))
            {
                float prevWeight = m_CurrentWeight;
                m_CurrentWeight += weight.Value;
                if (m_CurrentWeight >= m_WeightThreshold && prevWeight < m_WeightThreshold)
                {
                    if (!m_ActivateOnlyOnce || !m_Activated)
                    {
                        m_Activated = true;
                        OnActivate?.Invoke();
                    }
                }

                if (other.TryGetComponent(out OnDisableNotifier notifier))
                {
                    notifier.AddCallback(m_OnWeightDisabled);
                }
            }
        }

        // --------------------------------------------------------------------

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out Weight weight))
            {
                RemoveWeight(weight);
            }
        }

        // --------------------------------------------------------------------

        private void RemoveWeight(Weight weight)
        {
            if (weight.TryGetComponent(out OnDisableNotifier notifier))
            {
                notifier.RemoveCallback(m_OnWeightDisabled);
            }

            float prevWeight = m_CurrentWeight;
            m_CurrentWeight -= weight.Value;
            if (m_CurrentWeight < m_WeightThreshold && prevWeight >= m_WeightThreshold)
            {
                OnDeactivate?.Invoke();
            }
        }

        // --------------------------------------------------------------------
        // ISavable implementation
        // --------------------------------------------------------------------

        public string GetSavableData()
        {
            return m_Activated.ToString();
        }

        public void SetFromSavedData(string savedData)
        {
            m_Activated = Convert.ToBoolean(savedData);
        }
    }
}
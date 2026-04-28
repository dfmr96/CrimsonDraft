using System;
using UnityEngine;

namespace HorrorEngine
{
    public interface IPlayerVerticalAiming
    {
        public float Verticality
        {
            get; set;
        }
    }

    public class PlayerVerticalAimingFromInput : MonoBehaviour, IPlayerVerticalAiming
    {
        [Tooltip("Input value from which verticality will to be considered to be triggered and therefore at its max value")]
        [SerializeField] private float m_AimVerticalInputThreshold;
        [SerializeField] private float m_AimVerticalLerpSpeed;
        [Tooltip("When enabled aiming will only be available at its higher or lower points")]
        [SerializeField] private bool m_AimVerticalAnalog;

        private IPlayerInput m_Input;
        private float m_Verticality;

        public float Verticality { get => m_Verticality; set => m_Verticality = value; }

        private void Awake()
        {
            m_Input = GetComponentInParent<IPlayerInput>();
        }

        public void Update()
        {
            float verticality = m_Input.GetPrimaryAxis().y;
            if (Mathf.Abs(verticality) > m_AimVerticalInputThreshold)
            {
                verticality = Mathf.Sign(verticality);
            }
            else if (!m_AimVerticalAnalog)
            {
                verticality = 0f;
            }

            m_Verticality = Mathf.MoveTowards(m_Verticality, verticality, Time.deltaTime * m_AimVerticalLerpSpeed);
        }
    }

}
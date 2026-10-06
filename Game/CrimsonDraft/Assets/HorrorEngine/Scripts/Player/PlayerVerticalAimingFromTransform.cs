using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class PlayerVerticalAimingFromTransform : MonoBehaviour, IPlayerVerticalAiming
    {
        [SerializeField] Transform m_Transform;
        [SerializeField] float m_MinAngle = -45;
        [SerializeField] float m_MaxAngle = 45;
        [SerializeField] float m_MinVerticality = -1;
        [SerializeField] float m_MaxVerticality = 1;
        [SerializeField] float m_Smoothness = 1;
        private float m_Verticality;

        public float Verticality { get => m_Verticality; set { } }

        private void OnEnable()
        {
            enabled = m_Transform != null;
        }

        public void SetTransform(Transform t)
        {
            m_Transform = t;
            enabled = true;
        }

        private void Update()
        {
            Vector3 zForward = m_Transform.forward;
            zForward.y = 0;
            
            float angle = Vector3.Angle(zForward, m_Transform.forward) * Mathf.Sign(m_Transform.forward.y);
            float desiredVerticality = MathUtils.Map(angle, m_MinAngle, m_MaxAngle, m_MinVerticality, m_MaxVerticality);
            m_Verticality = Mathf.Lerp(m_Verticality, desiredVerticality, Time.deltaTime * m_Smoothness);
        }
    }
}
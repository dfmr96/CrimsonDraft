using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "StatusEffectApplyHealthDeltaOnDisplacement", menuName = "Horror Engine/Status/Effects/ApplyHealthDeltaOnDisplacement")]
    public class StatusEffectApplyHealthDeltaOnDisplacement : StatusEffectApplyHealthDelta
    {
        [SerializeField] private float m_HealthDeltaPerDisplacement;
        [SerializeField] private float m_DisplacementDistance = 10;

        private Transform m_TargetTransform;
        private Vector3 m_PrevPos;
        private float m_CurrentDisplacement;
        private Actor m_Actor;
        private UnityAction m_OnTeleported;


        StatusEffectApplyHealthDeltaOnDisplacement()
        {
            m_OnTeleported = OnTeleportedEvent;
        }

        // --------------------------------------------------------------------


        public override bool ShouldTick()
        {
            return true;
        }

        // --------------------------------------------------------------------

        public override void StartEffect(StatusEffectHandler handler)
        {
            base.StartEffect(handler);

            m_CurrentDisplacement = 0;
            m_TargetTransform = handler.transform;
            m_PrevPos = m_TargetTransform.position;

            m_Actor = handler.gameObject.GetComponent<Actor>();
            if (m_Actor)
            {
                m_Actor.OnTeleported.AddListener(m_OnTeleported);
            }
        }

        // --------------------------------------------------------------------

        public override void FixedTimeTick()
        {
            base.FixedTimeTick();

            Vector3 displacement = m_TargetTransform.position - m_PrevPos;
            m_CurrentDisplacement += displacement.magnitude;
            if (m_CurrentDisplacement >= m_DisplacementDistance)
            {
                ApplyHealthDelta(m_HealthDeltaPerDisplacement);
                m_CurrentDisplacement-= m_DisplacementDistance;
            }

            m_PrevPos = m_TargetTransform.position;
        }

        // --------------------------------------------------------------------

        public override void EndEffect()
        {
            if (m_Actor)
            {
                m_Actor.OnTeleported.AddListener(m_OnTeleported);
            }

            base.EndEffect();
        }

        // --------------------------------------------------------------------

        private void OnTeleportedEvent()
        {
            m_PrevPos = m_TargetTransform.position;
        }
    }
}

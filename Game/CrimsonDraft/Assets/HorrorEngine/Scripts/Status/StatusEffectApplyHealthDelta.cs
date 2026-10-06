using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class StatusEffectApplyHealthDelta : StatusEffect
    {
        [Tooltip("Health delta will only be applied when health is above this threshold")]
        [SerializeField] private float m_MinHealthThreshold = 0;

        private Health m_Health;


        // --------------------------------------------------------------------

        public override void StartEffect(StatusEffectHandler handler)
        {
            base.StartEffect(handler);

            m_Health = handler.gameObject.GetComponent<Health>();
            if (!m_Health)
            {
                Debug.LogError("Tried to apply StatusEffectApplyHealthDeltaAtRate on an object without a Healh component");
                EndEffect();
                return;
            }
        }

        // --------------------------------------------------------------------

        protected void ApplyHealthDelta(float delta)
        {
            if (m_Health.Value > m_MinHealthThreshold)
            {
                m_Health.TakeDamage(delta);
            }
        }
    }
}
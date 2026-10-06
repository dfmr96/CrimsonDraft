using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "StatusEffectApplyHealthDeltaAtRate", menuName = "Horror Engine/Status/Effects/ApplyHealthDeltaAtRate")]
    public class StatusEffectApplyHealthDeltaAtRate : StatusEffectApplyHealthDelta
    {
        [SerializeField] private float m_HealthDeltaPerTick;
        [SerializeField] private float m_TickRateInSeconds = 1f;
        
        private float m_RateTimer;

        // --------------------------------------------------------------------

        public override bool ShouldTick()
        {
            return true;
        }


        // --------------------------------------------------------------------

        public override void Tick()
        {
            base.Tick();

            m_RateTimer += Time.deltaTime;
            if (m_RateTimer > m_TickRateInSeconds)
            {
                ApplyHealthDelta(m_HealthDeltaPerTick);
                m_RateTimer = 0;
            }
        }

    }
}

using UnityEngine;

namespace HorrorEngine
{
    public class SenseDamage : Sense
    {
        private Health m_Health;
        private bool m_WasEverDamaged;

        // --------------------------------------------------------------------

        public override void Init(SenseController controller)
        {
            base.Init(controller);

            m_Health = controller.GetComponentInParent<Health>();

            m_Health.OnHealthDecreased.AddListener(OnHealthDecreased);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            if (m_Health)
            {
                m_Health.OnHealthDecreased.RemoveListener(OnHealthDecreased);
            }
        }

        // --------------------------------------------------------------------

        private void OnHealthDecreased(float amount)
        {
            m_WasEverDamaged = true;
            Transform transformRef = null;
            if (m_Health.LastInstigatorAttack)
            {
                var combatant = m_Health.LastInstigatorAttack.Owner;
                if (combatant)
                    transformRef = combatant.transform;
            }
            OnChanged?.Invoke(this, transformRef);
        }

        // --------------------------------------------------------------------

        public override bool SuccessfullySensed()
        {
            return m_WasEverDamaged;
        }

        // --------------------------------------------------------------------

        public override void Tick()
        {
            // Not actually needed
        }
    }
}
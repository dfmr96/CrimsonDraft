using UnityEngine;

namespace HorrorEngine
{
    public class Combatant : MonoBehaviour
    {
        public CombatantFaction Faction;
        public bool DisableAllDamageablesOnDeath = true;
        public bool StopAllAttacksOnDeath = true;

        Health m_Health;

        private void Awake()
        {
            m_Health = GetComponent<Health>();

            if (m_Health)
                m_Health.OnDeath.AddListener(OnDeath);
        }
        private void OnDestroy()
        {
            if (m_Health)
                m_Health.OnDeath.RemoveListener(OnDeath);
        }

        private void OnDeath(Health health)
        {
            if (DisableAllDamageablesOnDeath)
            {
                var damageables = GetComponentsInChildren<Damageable>(true);
                foreach (var damageable in damageables)
                {
                    damageable.enabled = false;
                }
            }
            if (StopAllAttacksOnDeath)
            {
                var attacks = GetComponentsInChildren<AttackBase>(true);
                foreach (var attack in attacks)
                {
                    attack.StopAttack();
                }
            }
        }
    }
}
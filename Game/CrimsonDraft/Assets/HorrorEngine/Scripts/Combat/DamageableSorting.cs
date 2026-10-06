using System.Collections.Generic;

namespace HorrorEngine
{
    public class DamageableSorting
    {
        private Dictionary<Combatant, Damageable> m_DamageablePerCombatant = new Dictionary<Combatant, Damageable>();
        private List<Damageable> m_NonCombatantDamageables = new List<Damageable>();
        private List<Damageable> m_AlreadyProcessedNonCombatant = new List<Damageable>();
        private List<Combatant> m_AlreadyProcessedCombatants = new List<Combatant>();

        // --------------------------------------------------------------------

        public void SortAndGetImpacted(ref List<Damageable> damageables, AttackType attack)
        {
            foreach (var damageable in damageables)
            {
                Combatant combatant = damageable.GetComponentInParent<Combatant>();

                if (combatant == null || !m_AlreadyProcessedCombatants.Contains(combatant))
                {
                    AttackImpact impact = attack.GetImpact(damageable.Type);

                    if (impact != null && impact.Damage > 0.0f)
                    {
                        if (combatant == null)
                        {
                            if (!m_NonCombatantDamageables.Contains(damageable) &&
                            !m_AlreadyProcessedNonCombatant.Contains(damageable)) // Non-combatant damageables
                            {
                                m_NonCombatantDamageables.Add(damageable);
                                continue;
                            }

                        }
                        else
                        {
                            if (!m_DamageablePerCombatant.ContainsKey(combatant))
                            {
                                m_DamageablePerCombatant[combatant] = damageable;
                            }
                            else
                            {
                                Damageable currentHighestPriority = m_DamageablePerCombatant[combatant];
                                if (damageable.Priority > currentHighestPriority.Priority)
                                {
                                    m_DamageablePerCombatant[combatant] = damageable;
                                }
                            }
                        }
                    }
                }
            }

            damageables.Clear();
            GetImpacted(ref damageables);
        }

        // --------------------------------------------------------------------

        public void Clear()
        {
            m_DamageablePerCombatant.Clear();
            m_NonCombatantDamageables.Clear();
            m_AlreadyProcessedCombatants.Clear();
            m_AlreadyProcessedNonCombatant.Clear();
        }


        // --------------------------------------------------------------------

        private void GetImpacted(ref List<Damageable> impacted)
        {
            GetImpactedCombatants(ref impacted);
            GetImpactedNonCombatants(ref impacted);
        }

        // --------------------------------------------------------------------

        private void GetImpactedCombatants(ref List<Damageable> impacted)
        {
            var alreadyProcessedCombatant = m_AlreadyProcessedCombatants;
            foreach (var combatantDamageablePair in m_DamageablePerCombatant)
            {
                Combatant combatant = combatantDamageablePair.Key;

                if (!alreadyProcessedCombatant.Contains(combatant))
                {
                    Damageable highestPriorityDamageable = combatantDamageablePair.Value;
                    impacted.Add(highestPriorityDamageable);
                    alreadyProcessedCombatant.Add(combatant);
                }
            }
        }

        // --------------------------------------------------------------------

        private void GetImpactedNonCombatants(ref List<Damageable> impacted)
        {
            foreach (var damageable in m_NonCombatantDamageables)
            {
                if (!m_AlreadyProcessedNonCombatant.Contains(damageable))
                {
                    impacted.Add(damageable);
                    m_AlreadyProcessedNonCombatant.Add(damageable);
                }
            }
        }
    }
}

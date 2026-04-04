using System.Collections.Generic;

namespace HorrorEngine
{
    public class DamageableSorting
    {
        // These lists are persistent to keep track of already damaged combatants/non combatants
        private List<Damageable> m_AlreadyProcessedNonCombatant = new List<Damageable>();
        private List<Combatant> m_AlreadyProcessedCombatants = new List<Combatant>();

        // These lists are cleared/reused each time sorting happens
        private Dictionary<Combatant, Damageable> m_DamageablePerCombatant = new Dictionary<Combatant, Damageable>();
        private List<Damageable> m_NonCombatantDamageables = new List<Damageable>();
        private List<Combatant> m_ProcessedCombatantsThisTime = new List<Combatant>();
        private List<Combatant> m_CombatantsToForget = new List<Combatant>();
        private List<Damageable> m_NonCombatantsToForget = new List<Damageable>();

        // --------------------------------------------------------------------

        public void SortAndGetImpacted(ref List<Damageable> damageables, AttackType attack)
        {
            m_DamageablePerCombatant.Clear();
            m_NonCombatantDamageables.Clear();
            m_ProcessedCombatantsThisTime.Clear();

            foreach (var damageable in damageables)
            {
                Combatant combatant = damageable.GetComponentInParent<Combatant>();
                if (!m_ProcessedCombatantsThisTime.Contains(combatant))
                    m_ProcessedCombatantsThisTime.Add(combatant);

                if (combatant == null || !m_AlreadyProcessedCombatants.Contains(combatant))
                {
                    AttackImpact impact = attack.GetImpact(damageable.Type);

                    if (impact != null && impact.Damage > 0.0f)
                    {
                        Health health = damageable.GetComponentInParent<Health>();
                        if (health && (health.IsDead || health.Invulnerable))
                            continue;

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

            // ----- After sorting cleanup
            ForgetUnprocessedCombatants();
            ForgetUnprocessedNonCombatants(damageables);

            damageables.Clear();
            GetImpacted(ref damageables);
        }

        // --------------------------------------------------------------------

        private void ForgetUnprocessedNonCombatants(List<Damageable> damageables)
        {
            foreach (var nonCombatantDamageable in m_AlreadyProcessedNonCombatant)
            {
                if (!damageables.Contains(nonCombatantDamageable))
                    m_NonCombatantsToForget.Add(nonCombatantDamageable);
            }

            foreach (var nonCombatantToForget in m_NonCombatantsToForget)
            {
                m_NonCombatantDamageables.Remove(nonCombatantToForget);
            }
        }

        // --------------------------------------------------------------------

        private void ForgetUnprocessedCombatants()
        {
            m_CombatantsToForget.Clear();
            foreach (var trackedCombatant in m_AlreadyProcessedCombatants)
            {
                if (!m_ProcessedCombatantsThisTime.Contains(trackedCombatant))
                {
                    m_CombatantsToForget.Add(trackedCombatant);
                }
            }

            foreach (var combatantToForget in m_CombatantsToForget)
            {
                m_AlreadyProcessedCombatants.Remove(combatantToForget);
            }
        }

        // --------------------------------------------------------------------

        public void Clear()
        {
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
            foreach (var combatantDamageablePair in m_DamageablePerCombatant)
            {
                Combatant combatant = combatantDamageablePair.Key;

                if (!m_AlreadyProcessedCombatants.Contains(combatant))
                {
                    Damageable highestPriorityDamageable = combatantDamageablePair.Value;
                    impacted.Add(highestPriorityDamageable);
                    m_AlreadyProcessedCombatants.Add(combatant);
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

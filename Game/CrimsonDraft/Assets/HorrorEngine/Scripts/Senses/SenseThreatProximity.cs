using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using System;

namespace HorrorEngine
{
    public class SenseThreatProximity : Sense
    {
        [SerializeField] private float m_DetectionRadius = 10f;                   
        [SerializeField] private LayerMask m_LayerMask;
        [NonSerialized] public List<Combatant> DetectedCombatants = new();        

        private Combatant m_Combatant;
        private bool m_IsThreatened;                                 
        private NavMeshPath m_ReachabilityPath;
        private Collider[] m_Colliders = new Collider[10];

        public bool IsThreatened => m_IsThreatened;

        private void Awake()
        {
            m_Combatant = GetComponentInParent<Combatant>();
            m_ReachabilityPath = new();
            Debug.Assert(m_Combatant && m_Combatant.Faction != null, "SenseThreat requires a combatant component with a valid faction", this);
        }

        // --------------------------------------------------------------------

        public override bool SuccessfullySensed()
        {
            return m_IsThreatened;
        }

        // --------------------------------------------------------------------

        public override void Tick()
        {
            bool wasThreatened = m_IsThreatened;
            DetectedCombatants.Clear();

            int count = Physics.OverlapSphereNonAlloc(transform.position, m_DetectionRadius, m_Colliders, m_LayerMask);

            for (int i = 0; i < count; ++i)
            {
                Collider collider = m_Colliders[i];
                if (collider == null)
                    continue;

                Combatant combatant = collider.GetComponentInParent<Combatant>();
                if (IsAThreat(combatant))
                {
                    // Check reachability
                    if (NavMesh.CalculatePath(transform.position, combatant.transform.position, NavMesh.AllAreas, m_ReachabilityPath))
                    {
                        if (m_ReachabilityPath.status == NavMeshPathStatus.PathComplete)
                            DetectedCombatants.Add(combatant);
                    }
                }
                
            }

            m_IsThreatened = DetectedCombatants.Count > 0;

            if (m_IsThreatened != wasThreatened)
            {
                OnChanged?.Invoke(this, DetectedCombatants.Count > 0 ? DetectedCombatants[0].transform : null);
            }
        }

        // --------------------------------------------------------------------

        private bool IsAThreat(Combatant combatant)
        {
            if (combatant == null)
            {
                return false;
            }

            Health health = combatant.GetComponent<Health>();
            if (health != null && health.IsDead)
                return false;

            
            return m_Combatant.Faction.IsHostile(combatant.Faction);
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, m_DetectionRadius);
        }
    }
}
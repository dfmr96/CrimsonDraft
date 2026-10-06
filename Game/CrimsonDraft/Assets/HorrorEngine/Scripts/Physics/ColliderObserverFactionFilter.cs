using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class ColliderObserverFactionFilter : ColliderObserverFilter
    {
        [SerializeField] List<CombatantFaction> m_Factions;

        public override bool Passes(Collider other) 
        {
            Combatant combatant = other.GetComponentInParent<Combatant>();
            return combatant && m_Factions.Contains(combatant.Faction);
        }
    }
}
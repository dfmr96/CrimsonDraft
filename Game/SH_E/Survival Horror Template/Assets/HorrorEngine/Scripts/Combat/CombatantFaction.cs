using System.Linq;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Combat/CombatantFaction")]
    public class CombatantFaction : ScriptableObject
    {
        public CombatantFaction Prototype; // This allows faction hierachy
        [ShowIf(nameof(Prototype))]
        public CombatantFaction[] ExcludeHostileFactions;
        public CombatantFaction[] HostileFactions;

        public bool IsHostile(CombatantFaction faction)
        {
            bool isHostile = false;

            if (Prototype)
            {
                isHostile = Prototype.IsHostile(faction) && !ExcludeHostileFactions.Contains(faction);
            }

            isHostile |= HostileFactions.Contains(faction);
            return isHostile;
        }
    }
}
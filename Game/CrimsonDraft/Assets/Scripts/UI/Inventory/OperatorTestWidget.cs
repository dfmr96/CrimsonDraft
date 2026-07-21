#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NaughtyAttributes;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    public sealed class OperatorTestWidget : MonoBehaviour
    {
        [SerializeField] private int            operatorIndex;
        [SerializeField] private int            damageAmount  = 10;
        [SerializeField] private int            healAmount    = 10;
        [SerializeField] private PartyPanelView inventoryPanel = null!;

        private IOperatorRoster? Roster => this.inventoryPanel?.Roster;

        [Button] public void Damage()
        {
            this.Roster?[this.operatorIndex].ApplyDamage(this.damageAmount);
            this.inventoryPanel?.Refresh();
        }

        [Button] public void Kill()
        {
            var op = this.Roster?[this.operatorIndex];
            if (op != null) op.ApplyDamage(op.Hp);
            this.inventoryPanel?.Refresh();
        }

        [Button] public void Heal()
        {
            this.Roster?[this.operatorIndex].Heal(this.healAmount);
            this.inventoryPanel?.Refresh();
        }
    }
}

#endif

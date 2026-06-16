#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NaughtyAttributes;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    public sealed class OperatorTestWidget : MonoBehaviour
    {
        [SerializeField] private int operatorIndex;
        [SerializeField] private int damageAmount = 10;
        [SerializeField] private int healAmount   = 10;

        private OperatorRoster? liveRoster;

        public void RegisterRoster(OperatorRoster roster) => this.liveRoster = roster;

        [Button] public void Damage() => this.liveRoster?[this.operatorIndex].ApplyDamage(this.damageAmount);
        [Button] public void Kill()   { var op = this.liveRoster?[this.operatorIndex]; if (op != null) op.ApplyDamage(op.Hp); }
        [Button] public void Heal()   => this.liveRoster?[this.operatorIndex].Heal(this.healAmount);
    }
}

#endif

#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NaughtyAttributes;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    /// <summary>
    /// Provee un roster fake para testear el party panel en Ui_CRT sin la escena de producción.
    /// Asignar OperatorData ScriptableObjects existentes en el Inspector.
    /// </summary>
    public sealed class PartyTestHarness : MonoBehaviour, IOperatorRosterSeedProvider
    {
        [System.Serializable]
        public struct FakeOperator
        {
            public OperatorData? data;
            [Range(0, 100)] public int startingHp;
        }

        [SerializeField] private FakeOperator[] operators = System.Array.Empty<FakeOperator>();
        [SerializeField] private int defaultMaxHp = 100;

        private OperatorRoster? liveRoster;

        public OperatorRosterSeed GetSeed()
        {
            var datas = new OperatorData?[this.operators.Length];
            for (int i = 0; i < this.operators.Length; i++)
                datas[i] = this.operators[i].data;
            return new OperatorRosterSeed(datas, this.defaultMaxHp);
        }

        public void RegisterRoster(OperatorRoster roster) => this.liveRoster = roster;

        [Button] public void DamageOperator(int index, int amount)
            => this.liveRoster?[index].ApplyDamage(amount);

        [Button] public void KillOperator(int index)
        {
            var op = this.liveRoster?[index];
            if (op != null) op.ApplyDamage(op.Hp);
        }

        [Button] public void HealOperator(int index, int amount)
            => this.liveRoster?[index].Heal(amount);
    }
}

#endif

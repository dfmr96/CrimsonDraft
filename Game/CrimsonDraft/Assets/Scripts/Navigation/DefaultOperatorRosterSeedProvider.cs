#nullable enable

using CrimsonDraft.Operators;
using UnityEngine;

namespace CrimsonDraft.Navigation
{
    public sealed class DefaultOperatorRosterSeedProvider : IOperatorRosterSeedProvider
    {
        private const int DefaultOperatorSlots = 4;
        private const int DefaultOperatorHp = 100;
        private const int DefaultOperatorAmmo = 6;

        public OperatorRosterSeed GetSeed()
        {
            var operators = new OperatorData?[DefaultOperatorSlots];
            for (int i = 0; i < operators.Length; i++)
                operators[i] = ScriptableObject.CreateInstance<OperatorData>();

            return new OperatorRosterSeed(operators, DefaultOperatorHp, DefaultOperatorAmmo);
        }
    }
}

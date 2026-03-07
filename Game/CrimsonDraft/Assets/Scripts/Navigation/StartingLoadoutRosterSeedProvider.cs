#nullable enable

using UnityEngine.Scripting;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    public sealed class StartingLoadoutRosterSeedProvider : IOperatorRosterSeedProvider
    {
        private const int DefaultHp = 100;
        private readonly StartingLoadout loadout;

        [Preserve]
        public StartingLoadoutRosterSeedProvider(StartingLoadout loadout) => this.loadout = loadout;

        public OperatorRosterSeed GetSeed() =>
            new OperatorRosterSeed(this.loadout.OperatorSlots, DefaultHp);
    }
}

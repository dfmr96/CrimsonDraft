#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Operators
{
    public sealed class OperatorRoster : IOperatorRoster
    {
        private readonly IOperatorRosterSeedProvider seedProvider;
        private OperatorRuntime[] slots = System.Array.Empty<OperatorRuntime>();
        private readonly List<int> scratchAlive = new();
        public bool IsInitialized { get; private set; }

        [Preserve]
        public OperatorRoster(IOperatorRosterSeedProvider seedProvider) => this.seedProvider = seedProvider;

        public int Count
        {
            get
            {
                this.EnsureInitialized();
                return this.slots.Length;
            }
        }

        public OperatorRuntime this[int slotIndex]
        {
            get
            {
                this.EnsureInitialized();
                return this.slots[slotIndex];
            }
        }

        public void EnsureInitialized()
        {
            if (this.IsInitialized)
                return;

            OperatorRosterSeed seed = this.seedProvider.GetSeed();
            this.slots = new OperatorRuntime[seed.Operators.Length];
            for (int i = 0; i < seed.Operators.Length; i++)
            {
                bool isPresent = seed.Operators[i] != null;
                this.slots[i] = new OperatorRuntime(i, seed.Operators[i], isPresent, seed.DefaultHp, seed.DefaultAmmo);
            }

            this.IsInitialized = true;
        }

        public IReadOnlyList<int> GetAliveSlots()
        {
            this.EnsureInitialized();
            this.scratchAlive.Clear();
            for (int i = 0; i < this.slots.Length; i++)
            {
                if (this.slots[i].IsAlive)
                    this.scratchAlive.Add(i);
            }
            return this.scratchAlive;
        }
    }
}

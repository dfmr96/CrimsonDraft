#nullable enable

using System.Collections.Generic;
using System.Linq;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class FakeRoster : IOperatorRoster
    {
        private readonly OperatorRuntime[] slots;

        public FakeRoster(params OperatorRuntime[] slots) => this.slots = slots;

        public static FakeRoster WithOperators(int count) =>
            new FakeRoster(Enumerable.Range(0, count).Select(InventoryTestData.Alive).ToArray());

        public bool IsInitialized => true;
        public int  Count         => this.slots.Length;
        public OperatorRuntime this[int slotIndex] => this.slots[slotIndex];

        public IReadOnlyList<int> GetAliveSlots() =>
            Enumerable.Range(0, this.slots.Length).Where(i => this.slots[i].IsAlive).ToList();

        public void  EnsureInitialized() { }
        public int[] GetHpSnapshot()     => this.slots.Select(s => s.Hp).ToArray();
        public void  RestoreHp(int[] snapshot) { }
    }
}

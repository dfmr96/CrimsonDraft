#nullable enable

using System.Collections.Generic;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class FakeCorpseAccess : ICorpseAccess
    {
        public readonly HashSet<int> Slots = new HashSet<int>();

        public static FakeCorpseAccess None => new FakeCorpseAccess();

        public bool CanAccess(int operatorSlot) => this.Slots.Contains(operatorSlot);
    }
}

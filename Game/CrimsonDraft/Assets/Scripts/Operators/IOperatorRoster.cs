#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Operators
{
    public interface IOperatorRoster
    {
        bool IsInitialized { get; }
        int Count { get; }
        OperatorRuntime this[int slotIndex] { get; }
        IReadOnlyList<int> GetAliveSlots();
        void EnsureInitialized();
    }
}

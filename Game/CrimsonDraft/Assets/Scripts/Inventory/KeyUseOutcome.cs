#nullable enable

namespace CrimsonDraft.Inventory
{
    public readonly struct KeyUseOutcome
    {
        public KeyUseResult Result    { get; }
        public int          SlotIndex { get; } // -1 when Result is NotFound

        public KeyUseOutcome(KeyUseResult result, int slotIndex)
        {
            this.Result    = result;
            this.SlotIndex = slotIndex;
        }
    }
}

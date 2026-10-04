#nullable enable

namespace CrimsonDraft.Inventory
{
    public readonly struct KeyUseOutcome
    {
        public KeyUseResult Result    { get; }
        public KeyItem?     Item      { get; }

        public KeyUseOutcome(KeyUseResult result, KeyItem? item)
        {
            this.Result    = result;
            this.Item      = item;
        }
    }
}

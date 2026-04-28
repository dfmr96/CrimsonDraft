#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class KeyItem : InventoryItem
    {
        public new KeyItemData Data        => (KeyItemData)base.Data;
        public int             UsesRemaining { get; private set; }

        public KeyItem(KeyItemData data) : base(data)
        {
            this.UsesRemaining = data.MaxUses;
        }

        /// <summary>
        /// Decrements UsesRemaining. Returns true if it reached 0 (including if already 0).
        /// </summary>
        public bool Consume()
        {
            if (this.UsesRemaining == 0) return true;
            this.UsesRemaining--;
            return this.UsesRemaining == 0;
        }
    }
}

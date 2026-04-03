#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class InventorySlot
    {
        public InventoryItem? Item     { get; internal set; }
        public int            Quantity { get; internal set; }
        public bool           IsEmpty  => this.Item == null;
    }
}

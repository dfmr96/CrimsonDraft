#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class InventorySlot
    {
        public InventoryItem? Item     { get; set; }
        public int            Quantity { get; set; }
        public bool           IsEmpty  => this.Item == null;
    }
}

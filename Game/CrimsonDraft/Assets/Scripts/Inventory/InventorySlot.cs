#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class InventorySlot
    {
        public InventoryItem? Item     { get; set; }
        public int            Quantity { get; set; }
        public int            GridCol      { get; set; } = -1;
        public int            GridRow      { get; set; } = -1;
        public int            GridRotation { get; set; } = 0;
        public bool           IsEmpty  => this.Item == null;
    }
}

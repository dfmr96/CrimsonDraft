#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class InventoryItem
    {
        public ItemData Data           { get; }
        public int      EquippedBySlot { get; internal set; } = -1;
        public bool     IsEquipped     => this.EquippedBySlot >= 0;

        public InventoryItem(ItemData data) => this.Data = data;
    }
}

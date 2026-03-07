#nullable enable

namespace CrimsonDraft.Inventory
{
    public class InventoryItem
    {
        public ItemData Data           { get; }
        public int      EquippedBySlot { get; internal set; } = -1;
        public bool     IsEquipped     => this.EquippedBySlot >= 0;

        protected internal InventoryItem(ItemData data) => this.Data = data;
    }
}

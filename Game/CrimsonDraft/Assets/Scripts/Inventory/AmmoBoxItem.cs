#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class AmmoBoxItem : InventoryItem, IHasDisplayCount
    {
        public new AmmoBoxData Data => (AmmoBoxData)base.Data;
        public int Quantity     { get; internal set; }
        public int DisplayCount => this.Quantity;

        public AmmoBoxItem(AmmoBoxData data, int quantity) : base(data)
        {
            this.Quantity = quantity > 0 ? quantity : data.DefaultQuantity;
        }
    }
}

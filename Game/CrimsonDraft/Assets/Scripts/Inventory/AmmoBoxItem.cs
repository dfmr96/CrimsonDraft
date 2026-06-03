#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class AmmoBoxItem : InventoryItem, IHasDisplayCount
    {
        public new AmmoBoxData Data => (AmmoBoxData)base.Data;
        public new int Quantity { get; internal set; }
        public int DisplayCount => this.Quantity;

        public override void AddQuantity(int amount) =>
            this.Quantity = System.Math.Clamp(this.Quantity + amount, 0, this.Data.MaxStack);

        public AmmoBoxItem(AmmoBoxData data, int quantity) : base(data)
        {
            this.Quantity = quantity > 0 ? quantity : data.DefaultQuantity;
        }
    }
}

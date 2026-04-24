#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class SocketItem : InventoryItem
    {
        public new SocketItemData Data => (SocketItemData)base.Data;

        public SocketItem(SocketItemData data) : base(data) { }
    }
}

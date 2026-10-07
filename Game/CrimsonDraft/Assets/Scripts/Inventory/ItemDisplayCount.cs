#nullable enable

namespace CrimsonDraft.Inventory
{
    public static class ItemDisplayCount
    {
        // Ammo and weapons always show their count; any other item shows it only when it stacks
        // (e.g. Ticker Tape), so single items like heals stay unlabelled.
        public static int? For(InventoryItem item)
        {
            if (item is IHasDisplayCount counted) return counted.DisplayCount;
            return item.Data.Stackable ? item.Quantity : null;
        }
    }
}

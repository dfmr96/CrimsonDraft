#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Inventory
{
    /// <summary>Walks the whole item catalog for the debug item spawner (RE 1.5 style):
    /// stepping forward from the start gives the first item, backward the last, and both
    /// directions wrap around.</summary>
    public sealed class DebugItemCatalog
    {
        private readonly IReadOnlyList<ItemData> items;
        private int index = -1;

        public DebugItemCatalog(IReadOnlyList<ItemData> items)
        {
            this.items = items;
        }

        /// <summary>Moves <paramref name="delta"/> items (+1 next, -1 previous); null when the catalog is empty.</summary>
        public ItemData? Step(int delta)
        {
            int count = this.items.Count;
            if (count == 0) return null;

            this.index = this.index < 0
                ? (delta >= 0 ? 0 : count - 1)
                : ((this.index + delta) % count + count) % count;
            return this.items[this.index];
        }

        /// <summary>Forgets the position, so the next step starts over from either end.</summary>
        public void Reset() => this.index = -1;

        /// <summary>A fresh item at its fullest: weapons loaded, stacks and ammo boxes at their max.</summary>
        public static InventoryItem CreateFull(ItemData data)
        {
            var item = InventoryItemFactory.Create(data, data.Stackable ? data.MaxStack : 0);
            if (item is WeaponItem weapon) weapon.SetAmmo(weapon.MaxAmmo);
            return item;
        }
    }
}

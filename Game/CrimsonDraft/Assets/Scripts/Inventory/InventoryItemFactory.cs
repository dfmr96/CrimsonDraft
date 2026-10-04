#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Inventory
{
    public static class InventoryItemFactory
    {
        public static InventoryItem Create(ItemData data, int quantity = 0)
        {
            InventoryItem item = data switch
            {
                WeaponData     weapon     => new WeaponItem(weapon),
                AmmoBoxData    ammo       => new AmmoBoxItem(ammo, quantity),
                ConsumableData consumable => new ConsumableItem(consumable),
                KeyItemData    key        => new KeyItem(key),
                SocketItemData socket     => new SocketItem(socket),
                _ => throw new ArgumentException($"Unknown ItemData subtype: {data.GetType().Name}", nameof(data)),
            };

            if (item is not AmmoBoxItem && data.Stackable && quantity > 0)
                item.Quantity = Mathf.Min(quantity, data.MaxStack);

            return item;
        }
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CrimsonDraft.Infrastructure.Save;

namespace CrimsonDraft.Inventory
{
    public static class InventorySerializer
    {
        public static List<InventoryItemEntry> Capture(IInventoryService inventory)
        {
            if (inventory.Held != null)
                throw new InvalidOperationException("Cannot capture the inventory while an item is held; call CancelHeld first.");

            return inventory.OperatorContainers
                .SelectMany(c => c.Placements.Select(p => ToEntry(c.Id, p.Item, p.Origin, p.Rotation)))
                .ToList();
        }

        public static List<InventoryItemEntry> FromLegacySlots(IEnumerable<InventorySlotEntry> slots) =>
            slots.Select(s => new InventoryItemEntry
            {
                containerKind        = (int)ContainerKind.Operator,
                containerIndex       = s.slotIndex / InventoryConstants.SlotsPerOperator,
                itemId               = s.itemId,
                quantity             = s.ammoBoxQuantity >= 0 ? s.ammoBoxQuantity : s.slotQuantity,
                col                  = s.gridCol,
                row                  = s.gridRow,
                rotation             = s.gridRotation,
                weaponAmmo           = s.weaponAmmo,
                keyUsesRemaining     = s.keyUsesRemaining,
                isExamined           = s.isExamined,
                equippedOperatorSlot = s.equippedOperatorSlot,
                equippedWeaponSlot   = s.equippedWeaponSlot,
            }).ToList();

        private static InventoryItemEntry ToEntry(ContainerId id, InventoryItem item, Vector2Int origin, int rotation) =>
            new InventoryItemEntry
            {
                containerKind        = (int)id.Kind,
                containerIndex       = id.Index,
                itemId               = item.Data.ItemId,
                quantity             = item.Quantity,
                col                  = origin.x,
                row                  = origin.y,
                rotation             = rotation,
                weaponAmmo           = item is WeaponItem weapon ? weapon.CurrentAmmo : -1,
                keyUsesRemaining     = item is KeyItem key ? key.UsesRemaining : -1,
                isExamined           = item.IsExamined,
                equippedOperatorSlot = item.EquippedBySlot,
                equippedWeaponSlot   = item.EquippedWeaponSlot,
            };
    }
}

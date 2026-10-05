#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CrimsonDraft.Infrastructure.Save;

namespace CrimsonDraft.Inventory
{
    public sealed partial class InventoryService
    {
        public void Restore(IReadOnlyList<InventoryItemEntry> entries, ItemDatabase database)
        {
            if (this.held != null) ClearHeld();
            foreach (var container in AllContainers()) container.Clear();
            for (int op = 0; op < this.roster.Count; op++)
                this.roster[op].SetEquippedWeapon(null, (int)WeaponSlot.Primary);

            var unplaced = new List<(InventoryItem item, InventoryItemEntry entry)>();
            foreach (var entry in entries)
            {
                if (!database.TryGetById(entry.itemId, out var data)) continue;

                var item = InventoryItemFactory.Create(data, entry.quantity);
                ApplyEntryState(item, entry);

                if (TryPlaceAtSavedPosition(item, entry)) WireEquipped(item, entry);
                else unplaced.Add((item, entry));
            }

            foreach (var (item, entry) in unplaced)
            {
                if (!TryPlaceAnywhere(item, entry))
                {
                    Debug.LogError($"Inventory restore: no room for '{entry.itemId}', item dropped.");
                    continue;
                }
                WireEquipped(item, entry);
            }

            NotifyChanged(AllContainers().ToArray());
        }

        private void WireEquipped(InventoryItem item, InventoryItemEntry entry)
        {
            if (item is not WeaponItem weapon || entry.equippedOperatorSlot < 0 || entry.equippedOperatorSlot >= this.roster.Count)
                return;
            if (FindCarriedContainerOf(weapon) == null) return;
            weapon.SetEquipped(entry.equippedOperatorSlot, entry.equippedWeaponSlot);
            this.roster[entry.equippedOperatorSlot].SetEquippedWeapon(weapon, entry.equippedWeaponSlot);
        }

        private static void ApplyEntryState(InventoryItem item, InventoryItemEntry entry)
        {
            item.IsExamined = entry.isExamined;
            if (item is WeaponItem weapon && entry.weaponAmmo >= 0) weapon.SetAmmo(entry.weaponAmmo);
            if (item is KeyItem key && entry.keyUsesRemaining >= 0) key.RestoreUses(entry.keyUsesRemaining);
        }

        private ItemContainer? PreferredContainer(InventoryItemEntry entry)
        {
            if (entry.containerKind == (int)ContainerKind.Storage) return EnsureStorage();
            var operators = EnsureContainers();
            return entry.containerIndex >= 0 && entry.containerIndex < operators.Length ? operators[entry.containerIndex] : null;
        }

        private bool TryPlaceAtSavedPosition(InventoryItem item, InventoryItemEntry entry)
        {
            var preferred = PreferredContainer(entry);
            var origin    = new Vector2Int(entry.col, entry.row);
            var footprint = ItemPlacement.FootprintOf(item.Data.GridSize, entry.rotation);
            if (preferred == null || !preferred.CanPlace(footprint, origin)) return false;

            preferred.Place(item, origin, entry.rotation);
            return true;
        }

        private bool TryPlaceAnywhere(InventoryItem item, InventoryItemEntry entry)
        {
            var preferred  = PreferredContainer(entry);
            var candidates = new List<ItemContainer>();
            if (preferred != null) candidates.Add(preferred);
            candidates.AddRange(EnsureContainers().Where(c => c != preferred));
            if (preferred != EnsureStorage()) candidates.Add(EnsureStorage());

            foreach (var container in candidates)
                for (int rotation = 0; rotation <= 1; rotation++)
                {
                    var footprint = ItemPlacement.FootprintOf(item.Data.GridSize, rotation);
                    if (!container.TryFindFreeCell(footprint, out var origin)) continue;
                    container.Place(item, origin, rotation);
                    return true;
                }

            return false;
        }
    }
}

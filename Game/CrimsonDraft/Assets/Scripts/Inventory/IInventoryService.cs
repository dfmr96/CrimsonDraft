#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Infrastructure.Save;

namespace CrimsonDraft.Inventory
{
    public interface IInventoryService
    {
        IReadOnlyList<ItemContainer> OperatorContainers { get; }
        ItemContainer GetContainer(ContainerId id);
        ContainerId? FindContainerOf(InventoryItem item);

        bool TryAdd(ItemData data, int quantity = 0);
        bool TryAdd(ItemData data, ContainerId target, int quantity = 0);
        void Remove(InventoryItem item);
        bool TryRemove(string itemId);
        bool HasItem(string itemId);

        InventoryItem? Held { get; }
        int HeldRotation { get; }
        bool HeldIsSplit { get; }
        event Action? HeldChanged;

        bool TryPickUp(InventoryItem item);
        bool CanSplit(InventoryItem stack);
        bool TrySplit(InventoryItem stack);
        void RotateHeld();
        DropResult TryDrop(ContainerId target, Vector2Int origin);
        void CancelHeld();

        bool TryCombine(InventoryItem a, InventoryItem b, out InventoryItem? result);
        bool TryUseConsumable(InventoryItem item, int operatorSlot);
        bool CanReload(InventoryItem ammo, int operatorSlot);
        bool TryReload(InventoryItem ammo, int operatorSlot);
        void Equip(WeaponItem weapon, int operatorSlot);
        void Unequip(WeaponItem weapon);
        bool HasEquippedWeapon(int operatorSlot);
        KeyUseOutcome TryUseKey(string keyItemId);

        void Restore(IReadOnlyList<InventoryItemEntry> entries, ItemDatabase database);
    }
}

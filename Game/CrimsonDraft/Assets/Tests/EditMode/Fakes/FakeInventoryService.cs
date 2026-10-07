#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class FakeInventoryService : IInventoryService
    {
        private readonly List<ItemContainer> containers = new List<ItemContainer>
        {
            new ItemContainer(ContainerId.Operator(0), 4, 4),
        };

        public readonly List<ItemData>                         Added      = new List<ItemData>();
        public readonly HashSet<string>                        OwnedIds   = new HashSet<string>();
        public readonly List<InventoryItem>                    Removed    = new List<InventoryItem>();
        public readonly List<string>                           RemovedIds = new List<string>();
        public readonly List<(InventoryItem item, int slot)>   Consumed   = new List<(InventoryItem, int)>();
        public readonly List<(InventoryItem ammo, int slot)>   Reloaded   = new List<(InventoryItem, int)>();
        public bool                                            AddResult      = true;
        public bool                                            ReloadResult   = true;
        public bool                                            HasEquipped;
        public KeyUseOutcome                                   NextKeyOutcome = new KeyUseOutcome(KeyUseResult.NotFound, (KeyItem?)null);
        public IReadOnlyList<InventoryItemEntry>?              RestoredEntries;

        public FakeInventoryService(params string[] ownedIds)
        {
            foreach (var id in ownedIds) this.OwnedIds.Add(id);
        }

        private readonly ItemContainer storage = new ItemContainer(ContainerId.Storage, 12, 4);

        public ItemContainer Operator0 => this.containers[0];
        public ItemContainer Storage   => this.storage;

        public IReadOnlyList<ItemContainer> OperatorContainers => this.containers;
        public ItemContainer GetContainer(ContainerId id) => id == ContainerId.Storage ? this.storage : this.containers.First(c => c.Id == id);
        public ContainerId? FindContainerOf(InventoryItem item) =>
            this.storage.Contains(item) ? ContainerId.Storage : this.containers.FirstOrDefault(c => c.Contains(item))?.Id;

        public bool TryAdd(ItemData data, int quantity = 0)
        {
            this.Added.Add(data);
            return this.AddResult;
        }

        public bool TryAdd(ItemData data, ContainerId target, int quantity = 0) => TryAdd(data, quantity);
        public void Remove(InventoryItem item) => this.Removed.Add(item);

        public bool TryRemove(string itemId)
        {
            this.RemovedIds.Add(itemId);
            return this.OwnedIds.Remove(itemId);
        }

        public bool HasItem(string itemId) => this.OwnedIds.Contains(itemId);

        public readonly HashSet<ContainerId> Inaccessible = new HashSet<ContainerId>();

        public bool IsCarried(ContainerId id)    => id != ContainerId.Storage && !this.Inaccessible.Contains(id);
        public bool IsAccessible(ContainerId id) => !this.Inaccessible.Contains(id);

        public InventoryItem? Held         => null;
        public int            HeldRotation => 0;
        public bool           HeldIsSplit  => false;
        public event Action? HeldChanged { add { } remove { } }

        public bool       TryPickUp(InventoryItem item)                     => false;
        public bool       CanSplit(InventoryItem stack)                     => false;
        public bool       TrySplit(InventoryItem stack)                     => false;
        public void       RotateHeld()                                      { }
        public DropResult TryDrop(ContainerId target, Vector2Int origin)    => DropResult.Rejected;
        public void       CancelHeld()                                      { }

        public bool TryCombine(InventoryItem a, InventoryItem b, out InventoryItem? result)
        {
            result = null;
            return false;
        }

        public bool TryUseConsumable(InventoryItem item, int operatorSlot)
        {
            this.Consumed.Add((item, operatorSlot));
            return true;
        }

        public bool CanReload(InventoryItem ammo, int operatorSlot) => this.ReloadResult;

        public bool TryReload(InventoryItem ammo, int operatorSlot)
        {
            this.Reloaded.Add((ammo, operatorSlot));
            return this.ReloadResult;
        }

        public readonly List<(IWeaponSlot Weapon, int Shots)> SpentAmmo = new();
        public void SpendAmmo(IWeaponSlot weapon, int shots)
        {
            this.SpentAmmo.Add((weapon, shots));
            weapon.SetAmmo(weapon.CurrentAmmo - shots);
        }

        public void Equip(WeaponItem weapon, int operatorSlot) { }
        public void Unequip(WeaponItem weapon)                 { }
        public bool HasEquippedWeapon(int operatorSlot)        => this.HasEquipped;
        public KeyUseOutcome TryUseKey(string keyItemId)       => this.NextKeyOutcome;

        public int  ReleaseCalls;
        public void ReleaseDeadOperatorWeapons() => this.ReleaseCalls++;

        public void Restore(IReadOnlyList<InventoryItemEntry> entries, ItemDatabase database) => this.RestoredEntries = entries;
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    public sealed partial class InventoryService : IInventoryService
    {
        private readonly IOperatorRoster roster;
        private readonly ICombineService combineService;
        private readonly ICorpseAccess   corpseAccess;
        private ItemContainer[]? operatorContainers;
        private ItemContainer?   storageContainer;

        [Preserve]
        public InventoryService(IOperatorRoster roster, ICombineService combineService, ICorpseAccess corpseAccess)
        {
            this.roster         = roster;
            this.combineService = combineService;
            this.corpseAccess   = corpseAccess;
        }

        public IReadOnlyList<ItemContainer> OperatorContainers => EnsureContainers();

        public ItemContainer GetContainer(ContainerId id)
        {
            if (id == ContainerId.Storage) return EnsureStorage();
            var containers = EnsureContainers();
            if (id.Kind == ContainerKind.Operator && id.Index >= 0 && id.Index < containers.Length)
                return containers[id.Index];
            throw new ArgumentOutOfRangeException(nameof(id), id, "No inventory container with this id.");
        }

        public ContainerId? FindContainerOf(InventoryItem item) => FindContainerObjectOf(item)?.Id;

        public bool TryAdd(ItemData data, int quantity = 0) =>
            (data is AmmoBoxData ammo && TryAddToArmedOperators(ammo, quantity))
            || TryAddTo(CarriedContainers().ToArray(), data, quantity);

        public bool TryAdd(ItemData data, ContainerId target, int quantity = 0) =>
            TryAddTo(new[] { GetContainer(target) }, data, quantity);

        public void Remove(InventoryItem item)
        {
            var container = FindContainerObjectOf(item)
                ?? throw new ArgumentException($"Item '{item.Data.ItemId}' is not in the inventory.", nameof(item));
            if (item is WeaponItem weapon && weapon.IsEquipped)
                UnequipInternal(weapon);
            container.Remove(item);
            container.NotifyChanged();
        }

        public bool TryRemove(string itemId)
        {
            var placement = AllPlacements().FirstOrDefault(p => p.Item.Data.ItemId == itemId);
            if (placement == null) return false;
            Remove(placement.Item);
            return true;
        }

        public bool HasItem(string itemId) => AllPlacements().Any(p => p.Item.Data.ItemId == itemId);

        // Spends a single unit of a carried stack (TryRemove drops the whole item) -- e.g. one
        // Ticker Tape per save. Storage and dead operators' containers don't count.
        public bool TryConsumeOne(ItemData data)
        {
            var placement = AllPlacements().FirstOrDefault(p => p.Item.Data.ItemId == data.ItemId);
            if (placement == null) return false;

            var item      = placement.Item;
            var container = FindContainerObjectOf(item);
            item.Quantity -= 1;
            if (item.Quantity <= 0) container?.Remove(item);
            container?.NotifyChanged();
            return true;
        }

        public bool IsCarried(ContainerId id) =>
            id.Kind == ContainerKind.Operator && id.Index >= 0 && id.Index < this.roster.Count && this.roster[id.Index].IsAlive;

        public bool IsAccessible(ContainerId id) =>
            id == ContainerId.Storage
            || IsCarried(id)
            || (id.Kind == ContainerKind.Operator && this.corpseAccess.CanAccess(id.Index));

        // Picked-up ammo goes, whole, to a living operator with a weapon of that calibre equipped,
        // fewest rounds of that calibre first (loaded + boxed), roster order on ties. Returns false
        // when nobody armed can take the whole box, so the caller falls back to any carried grid.
        private bool TryAddToArmedOperators(AmmoBoxData ammo, int quantity)
        {
            var containers = EnsureContainers();

            IEnumerable<WeaponItem> ArmedWith(int slot) => containers[slot].Placements
                .Select(p => p.Item).OfType<WeaponItem>()
                .Where(w => w.EquippedBySlot == slot && w.Caliber == ammo.Caliber);

            int RoundsHeld(int slot) =>
                ArmedWith(slot).Sum(w => w.CurrentAmmo)
                + containers[slot].Placements.Select(p => p.Item).OfType<AmmoBoxItem>()
                    .Where(b => b.Data.Caliber == ammo.Caliber).Sum(b => b.Quantity);

            var candidates = Enumerable.Range(0, containers.Length)
                .Where(slot => IsCarried(ContainerId.Operator(slot)) && ArmedWith(slot).Any())
                .OrderBy(RoundsHeld)
                .ThenBy(slot => slot);

            foreach (int slot in candidates)
                if (TryAddTo(new[] { containers[slot] }, ammo, quantity)) return true;
            return false;
        }

        private bool TryAddTo(IReadOnlyList<ItemContainer> candidates, ItemData data, int quantity)
        {
            if (!data.Stackable)
            {
                if (!TryFindFreeCell(candidates, data.GridSize, out var target, out var origin)) return false;
                target!.Place(InventoryItemFactory.Create(data), origin, 0);
                target.NotifyChanged();
                return true;
            }

            int remaining = quantity > 0 ? quantity : data is AmmoBoxData ammo ? ammo.DefaultQuantity : 1;
            var stacks = candidates
                .SelectMany(c => c.Placements.Select(p => (container: c, item: p.Item)))
                .Where(s => s.item.Data.ItemId == data.ItemId && s.item.Quantity < data.MaxStack)
                .ToList();

            int stackSpace = stacks.Sum(s => data.MaxStack - s.item.Quantity);
            ItemContainer? overflowTarget = null;
            var overflowOrigin = Vector2Int.zero;
            if (remaining > stackSpace && !TryFindFreeCell(candidates, data.GridSize, out overflowTarget, out overflowOrigin))
                return false;

            var touched = new List<ItemContainer>();
            foreach (var (container, item) in stacks)
            {
                if (remaining == 0) break;
                int added = Math.Min(remaining, data.MaxStack - item.Quantity);
                item.Quantity += added;
                remaining     -= added;
                touched.Add(container);
            }

            if (remaining > 0)
            {
                overflowTarget!.Place(InventoryItemFactory.Create(data, remaining), overflowOrigin, 0);
                touched.Add(overflowTarget);
            }

            NotifyChanged(touched.ToArray());
            return true;
        }

        private ItemContainer[] EnsureContainers()
        {
            if (this.operatorContainers != null) return this.operatorContainers;
            this.roster.EnsureInitialized();
            this.operatorContainers = Enumerable.Range(0, this.roster.Count)
                .Select(i => new ItemContainer(
                    ContainerId.Operator(i),
                    InventoryConstants.OperatorGridWidth,
                    InventoryConstants.OperatorGridHeight))
                .ToArray();
            return this.operatorContainers;
        }

        private ItemContainer EnsureStorage() =>
            this.storageContainer ??= new ItemContainer(
                ContainerId.Storage,
                InventoryConstants.StorageGridWidth,
                InventoryConstants.StorageGridHeight);

        private IEnumerable<ItemContainer> AllContainers() => EnsureContainers().Append(EnsureStorage());

        private ItemContainer? FindContainerObjectOf(InventoryItem item) =>
            AllContainers().FirstOrDefault(c => c.Contains(item));

        private IEnumerable<ItemContainer> CarriedContainers() => EnsureContainers().Where(c => IsCarried(c.Id));

        private ItemContainer? FindCarriedContainerOf(InventoryItem item) =>
            CarriedContainers().FirstOrDefault(c => c.Contains(item));

        private IEnumerable<ItemPlacement> AllPlacements() => CarriedContainers().SelectMany(c => c.Placements);

        private void UnequipInternal(WeaponItem weapon)
        {
            this.roster[weapon.EquippedBySlot].SetEquippedWeapon(null, weapon.EquippedWeaponSlot);
            weapon.ClearEquipped();
        }

        private static bool TryFindFreeCell(
            IEnumerable<ItemContainer> candidates, Vector2Int footprint,
            out ItemContainer? container, out Vector2Int origin)
        {
            foreach (var candidate in candidates)
            {
                if (!candidate.TryFindFreeCell(footprint, out origin)) continue;
                container = candidate;
                return true;
            }

            container = null;
            origin    = Vector2Int.zero;
            return false;
        }

        private static void NotifyChanged(params ItemContainer?[] containers)
        {
            foreach (var container in containers.Where(c => c != null).Distinct())
                container!.NotifyChanged();
        }
    }
}

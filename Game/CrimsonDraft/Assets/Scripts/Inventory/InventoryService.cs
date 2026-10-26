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

        public AddPreview? PreviewAdd(ItemData data, int quantity = 0)
        {
            if (data is AmmoBoxData ammo)
            {
                var containers = EnsureContainers();
                foreach (int slot in ArmedOperatorSlots(ammo))
                    if (PreviewAddTo(new[] { containers[slot] }, data, quantity) is { } armed) return armed;
            }
            return PreviewAddTo(CarriedContainers().ToArray(), data, quantity);
        }

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

        // Swaps a carried item for another (empty flask -> filled flask). The replacement takes the
        // original's spot and rotation when it fits there -- TryAdd only tries the unrotated
        // footprint, so a flask laid flat in an otherwise full grid would find no room -- else any
        // free cell. With no room at all the original goes back untouched and nothing is lost.
        public bool TryReplace(string itemId, ItemData replacement)
        {
            var placement = AllPlacements().FirstOrDefault(p => p.Item.Data.ItemId == itemId);
            if (placement == null || placement.Item.IsEquipped) return false;

            var original  = placement.Item;
            var container = FindContainerObjectOf(original)!;
            container.Remove(original);

            var footprint = ItemPlacement.FootprintOf(replacement.GridSize, placement.Rotation);
            if (container.CanPlace(footprint, placement.Origin))
            {
                container.Place(InventoryItemFactory.Create(replacement), placement.Origin, placement.Rotation);
                container.NotifyChanged();
                return true;
            }

            if (TryFindFreeCell(CarriedContainers(), replacement.GridSize, out var target, out var origin))
            {
                target!.Place(InventoryItemFactory.Create(replacement), origin, 0);
                NotifyChanged(container, target);
                return true;
            }

            container.Place(original, placement.Origin, placement.Rotation);
            return false;
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
            foreach (int slot in ArmedOperatorSlots(ammo))
                if (TryAddTo(new[] { containers[slot] }, ammo, quantity)) return true;
            return false;
        }

        private IEnumerable<int> ArmedOperatorSlots(AmmoBoxData ammo)
        {
            var containers = EnsureContainers();

            IEnumerable<WeaponItem> ArmedWith(int slot) => containers[slot].Placements
                .Select(p => p.Item).OfType<WeaponItem>()
                .Where(w => w.EquippedBySlot == slot && w.Caliber == ammo.Caliber);

            int RoundsHeld(int slot) =>
                ArmedWith(slot).Sum(w => w.CurrentAmmo)
                + containers[slot].Placements.Select(p => p.Item).OfType<AmmoBoxItem>()
                    .Where(b => b.Data.Caliber == ammo.Caliber).Sum(b => b.Quantity);

            return Enumerable.Range(0, containers.Length)
                .Where(slot => IsCarried(ContainerId.Operator(slot)) && ArmedWith(slot).Any())
                .OrderBy(RoundsHeld)
                .ThenBy(slot => slot);
        }

        // Same decisions as TryAddTo, minus the writes.
        private static AddPreview? PreviewAddTo(IReadOnlyList<ItemContainer> candidates, ItemData data, int quantity)
        {
            if (!data.Stackable)
                return TryFindFreeCell(candidates, data.GridSize, out var target, out var origin)
                    ? new AddPreview(target!.Id, origin)
                    : (AddPreview?)null;

            int remaining = quantity > 0 ? quantity : data is AmmoBoxData ammo ? ammo.DefaultQuantity : 1;
            var stacks = candidates
                .SelectMany(c => c.Placements.Select(p => (container: c, placement: p)))
                .Where(s => s.placement.Item.Data.ItemId == data.ItemId && s.placement.Item.Quantity < data.MaxStack)
                .ToList();

            int stackSpace = stacks.Sum(s => data.MaxStack - s.placement.Item.Quantity);
            if (remaining > stackSpace)
                return TryFindFreeCell(candidates, data.GridSize, out var overflowTarget, out var overflowOrigin)
                    ? new AddPreview(overflowTarget!.Id, overflowOrigin)
                    : (AddPreview?)null;

            var first = stacks[0];
            return new AddPreview(first.container.Id, first.placement.Origin, first.placement.Item);
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

        public event Action? ContentsChanged;

        private void RaiseContentsChanged() => ContentsChanged?.Invoke();

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
            foreach (var container in this.operatorContainers)
                container.Changed += RaiseContentsChanged;
            return this.operatorContainers;
        }

        private ItemContainer EnsureStorage()
        {
            if (this.storageContainer != null) return this.storageContainer;
            this.storageContainer = new ItemContainer(
                ContainerId.Storage,
                InventoryConstants.StorageGridWidth,
                InventoryConstants.StorageGridHeight);
            this.storageContainer.Changed += RaiseContentsChanged;
            return this.storageContainer;
        }

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

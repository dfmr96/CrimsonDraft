#nullable enable

using System;
using System.Linq;
using UnityEngine;

namespace CrimsonDraft.Inventory
{
    public sealed partial class InventoryService
    {
        private InventoryItem? held;
        private HeldReturn?    heldReturn;

        public event Action? HeldChanged;

        public InventoryItem? Held         => this.held;
        public int            HeldRotation { get; private set; }
        public bool           HeldIsSplit  => this.heldReturn?.SplitSource != null;

        public bool TryPickUp(InventoryItem item)
        {
            if (this.held != null || item.IsEquipped) return false;
            var container = FindContainerObjectOf(item);
            var placement = container?.GetPlacement(item);
            if (container == null || placement == null) return false;

            container.Remove(item);
            SetHeld(item, placement.Rotation, new HeldReturn(container.Id, placement.Origin, placement.Rotation, null));
            container.NotifyChanged();
            return true;
        }

        public bool CanSplit(InventoryItem stack)
        {
            if (this.held != null || !stack.Data.Stackable || stack.Quantity <= 1 || FindCarriedContainerOf(stack) == null)
                return false;
            var size = stack.Data.GridSize;
            return EnsureContainers().Any(c =>
                c.TryFindFreeCell(size, out _) || c.TryFindFreeCell(ItemPlacement.FootprintOf(size, 1), out _));
        }

        public bool TrySplit(InventoryItem stack)
        {
            if (!CanSplit(stack)) return false;
            var container = FindContainerObjectOf(stack);
            var placement = container?.GetPlacement(stack);
            if (container == null || placement == null) return false;

            int half  = stack.Quantity / 2;
            var split = InventoryItemFactory.Create(stack.Data, half);
            stack.Quantity -= half;
            SetHeld(split, 0, new HeldReturn(container.Id, placement.Origin, 0, stack));
            container.NotifyChanged();
            return true;
        }

        public void RotateHeld()
        {
            if (this.held == null) return;
            this.HeldRotation = this.HeldRotation == 0 ? 1 : 0;
            this.HeldChanged?.Invoke();
        }

        public DropResult TryDrop(ContainerId target, Vector2Int origin)
        {
            if (this.held == null || this.heldReturn == null) return DropResult.Rejected;

            var container = GetContainer(target);
            var footprint = ItemPlacement.FootprintOf(this.held.Data.GridSize, this.HeldRotation);
            if (!container.IsWithinBounds(footprint, origin)) return DropResult.Rejected;

            var overlapping = container.GetOverlapping(footprint, origin);
            if (overlapping.Count == 0)
            {
                container.Place(this.held, origin, this.HeldRotation);
                ClearHeld();
                container.NotifyChanged();
                return DropResult.Placed;
            }

            if (overlapping.Count > 1 || this.heldReturn.SplitSource != null) return DropResult.Rejected;
            var displaced = overlapping.First();
            if (displaced.IsEquipped) return DropResult.Rejected;

            return TrySwap(container, origin, displaced) ? DropResult.Swapped : DropResult.Rejected;
        }

        public void CancelHeld()
        {
            if (this.held == null || this.heldReturn == null) return;
            var item = this.held;
            var spot = this.heldReturn;

            if (spot.SplitSource != null && FindContainerObjectOf(spot.SplitSource) is { } sourceContainer)
            {
                spot.SplitSource.Quantity += item.Quantity;
                ClearHeld();
                sourceContainer.NotifyChanged();
                return;
            }

            var container = GetContainer(spot.Container);
            var footprint = ItemPlacement.FootprintOf(item.Data.GridSize, spot.Rotation);
            var origin    = spot.Origin;
            if (!container.CanPlace(footprint, origin) && !container.TryFindFreeCell(footprint, out origin))
                throw new InvalidOperationException($"No room to return held item '{item.Data.ItemId}' to {spot.Container}.");

            container.Place(item, origin, spot.Rotation);
            ClearHeld();
            container.NotifyChanged();
        }

        private bool TrySwap(ItemContainer target, Vector2Int origin, InventoryItem displaced)
        {
            var displacedPlacement = target.GetPlacement(displaced)!;
            var returnContainer    = GetContainer(this.heldReturn!.Container);

            target.Remove(displaced);
            target.Place(this.held!, origin, this.HeldRotation);

            var returnOrigin = this.heldReturn.Origin;
            if (!returnContainer.CanPlace(displacedPlacement.Footprint, returnOrigin)
                && !returnContainer.TryFindFreeCell(displacedPlacement.Footprint, out returnOrigin))
            {
                target.Remove(this.held!);
                target.Place(displaced, displacedPlacement.Origin, displacedPlacement.Rotation);
                return false;
            }

            SetHeld(displaced, displacedPlacement.Rotation,
                new HeldReturn(returnContainer.Id, returnOrigin, displacedPlacement.Rotation, null));
            target.NotifyChanged();
            return true;
        }

        private void SetHeld(InventoryItem item, int rotation, HeldReturn spot)
        {
            this.held         = item;
            this.HeldRotation = rotation;
            this.heldReturn   = spot;
            this.HeldChanged?.Invoke();
        }

        private void ClearHeld()
        {
            this.held         = null;
            this.heldReturn   = null;
            this.HeldRotation = 0;
            this.HeldChanged?.Invoke();
        }

        private sealed class HeldReturn
        {
            public HeldReturn(ContainerId container, Vector2Int origin, int rotation, InventoryItem? splitSource)
            {
                this.Container   = container;
                this.Origin      = origin;
                this.Rotation    = rotation;
                this.SplitSource = splitSource;
            }

            public ContainerId    Container   { get; }
            public Vector2Int     Origin      { get; }
            public int            Rotation    { get; }
            public InventoryItem? SplitSource { get; }
        }
    }
}

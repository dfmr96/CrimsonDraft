#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Inventory
{
    public sealed class ItemContainer
    {
        private readonly InventoryItem?[,] cells;
        private readonly Dictionary<InventoryItem, ItemPlacement> placements = new Dictionary<InventoryItem, ItemPlacement>();

        public ItemContainer(ContainerId id, int width, int height)
        {
            this.Id    = id;
            this.cells = new InventoryItem?[width, height];
        }

        public event Action? Changed;

        public ContainerId                Id         { get; }
        public int                        Width      => this.cells.GetLength(0);
        public int                        Height     => this.cells.GetLength(1);
        public int                        Count      => this.placements.Count;
        public IEnumerable<ItemPlacement> Placements => this.placements.Values;

        public InventoryItem? GetItemAt(Vector2Int cell) =>
            IsInside(cell) ? this.cells[cell.x, cell.y] : null;

        public ItemPlacement? GetPlacement(InventoryItem item) =>
            this.placements.TryGetValue(item, out var placement) ? placement : null;

        public bool Contains(InventoryItem item) => this.placements.ContainsKey(item);

        public bool IsWithinBounds(Vector2Int footprint, Vector2Int origin) =>
            origin.x >= 0 && origin.y >= 0
            && origin.x + footprint.x <= this.Width
            && origin.y + footprint.y <= this.Height;

        public bool CanPlace(Vector2Int footprint, Vector2Int origin, InventoryItem? ignore = null)
        {
            if (!IsWithinBounds(footprint, origin)) return false;
            for (int c = origin.x; c < origin.x + footprint.x; c++)
                for (int r = origin.y; r < origin.y + footprint.y; r++)
                {
                    var occupant = this.cells[c, r];
                    if (occupant != null && occupant != ignore) return false;
                }
            return true;
        }

        public IReadOnlyCollection<InventoryItem> GetOverlapping(Vector2Int footprint, Vector2Int origin)
        {
            var found = new HashSet<InventoryItem>();
            for (int c = origin.x; c < origin.x + footprint.x; c++)
                for (int r = origin.y; r < origin.y + footprint.y; r++)
                    if (GetItemAt(new Vector2Int(c, r)) is { } occupant)
                        found.Add(occupant);
            return found;
        }

        public bool TryFindFreeCell(Vector2Int footprint, out Vector2Int origin)
        {
            for (int row = 0; row + footprint.y <= this.Height; row++)
                for (int col = 0; col + footprint.x <= this.Width; col++)
                {
                    var candidate = new Vector2Int(col, row);
                    if (!CanPlace(footprint, candidate)) continue;
                    origin = candidate;
                    return true;
                }

            origin = default;
            return false;
        }

        internal void Place(InventoryItem item, Vector2Int origin, int rotation)
        {
            if (this.placements.ContainsKey(item))
                throw new InvalidOperationException($"Item '{item.Data.ItemId}' is already in {this.Id}.");

            var placement = new ItemPlacement(item, origin, rotation);
            if (!CanPlace(placement.Footprint, origin))
                throw new InvalidOperationException($"Item '{item.Data.ItemId}' does not fit at {origin} in {this.Id}.");

            Fill(placement, item);
            this.placements[item] = placement;
        }

        internal void Remove(InventoryItem item)
        {
            if (!this.placements.TryGetValue(item, out var placement))
                throw new InvalidOperationException($"Item '{item.Data.ItemId}' is not in {this.Id}.");

            Fill(placement, null);
            this.placements.Remove(item);
        }

        internal void Clear()
        {
            Array.Clear(this.cells, 0, this.cells.Length);
            this.placements.Clear();
        }

        internal void NotifyChanged() => this.Changed?.Invoke();

        private void Fill(ItemPlacement placement, InventoryItem? value)
        {
            var footprint = placement.Footprint;
            for (int c = placement.Origin.x; c < placement.Origin.x + footprint.x; c++)
                for (int r = placement.Origin.y; r < placement.Origin.y + footprint.y; r++)
                    this.cells[c, r] = value;
        }

        private bool IsInside(Vector2Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < this.Width && cell.y < this.Height;
    }
}

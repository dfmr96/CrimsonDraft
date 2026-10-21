#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    public sealed class ItemPlacement
    {
        public ItemPlacement(InventoryItem item, Vector2Int origin, int rotation)
        {
            this.Item     = item;
            this.Origin   = origin;
            this.Rotation = rotation;
        }

        public InventoryItem Item     { get; }
        public Vector2Int    Origin   { get; }
        public int           Rotation { get; }
        public Vector2Int    Footprint => FootprintOf(this.Item.Data.GridSize, this.Rotation);

        public static Vector2Int FootprintOf(Vector2Int gridSize, int rotation) =>
            rotation == 1 ? new Vector2Int(gridSize.y, gridSize.x) : gridSize;
    }
}

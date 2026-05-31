using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class InventoryGrid : MonoBehaviour
    {
        [Header("Grid Config")]
        [SerializeField] private int columns = 4;
        [SerializeField] private int rows = 4;

        [Header("Visual")]
        [SerializeField] private Image gridBackground;

        // Derived from the sprite at runtime — not set manually
        private float cellSize;

        // Each cell stores a reference to the item occupying it (null = empty).
        // Multi-cell items fill all their cells with the same reference.
        private InventoryItem[,] itemGrid;
        private RectTransform rectTransform;

        public int Columns => columns;
        public int Rows => rows;
        public float CellSize => cellSize;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            itemGrid = new InventoryItem[columns, rows];
            // Derive cellSize from the RectTransform size the designer set in editor
            cellSize = rectTransform.rect.width / columns;

            if (gridBackground != null)
            {
                gridBackground.type = Image.Type.Tiled;
                gridBackground.pixelsPerUnitMultiplier = 1f;
            }
        }

        // Converts a pointer world position to a grid cell coordinate.
        // Returns false if outside the grid.
        public bool WorldToCell(Vector2 worldPos, out Vector2Int cell)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, worldPos, null, out local);

            // RectTransform pivot is center by default; shift to top-left origin
            local += new Vector2(columns * cellSize * 0.5f, rows * cellSize * 0.5f);

            int col = Mathf.FloorToInt(local.x / cellSize);
            int row = Mathf.FloorToInt((rows * cellSize - local.y) / cellSize);

            cell = new Vector2Int(col, row);
            return col >= 0 && col < columns && row >= 0 && row < rows;
        }

        // Returns the local anchor position (top-left corner) of a cell.
        public Vector2 CellToLocal(Vector2Int cell)
        {
            float x = cell.x * cellSize - columns * cellSize * 0.5f;
            float y = -(cell.y * cellSize) + rows * cellSize * 0.5f;
            return new Vector2(x, y);
        }

        // Check if a rect of itemSize starting at origin fits and is unoccupied.
        public bool CanPlace(Vector2Int origin, Vector2Int itemSize)
        {
            for (int c = origin.x; c < origin.x + itemSize.x; c++)
                for (int r = origin.y; r < origin.y + itemSize.y; r++)
                {
                    if (c < 0 || c >= columns || r < 0 || r >= rows) return false;
                    if (itemGrid[c, r] != null) return false;
                }
            return true;
        }

        public void PlaceItem(InventoryItem item)
        {
            for (int c = item.GridOrigin.x; c < item.GridOrigin.x + item.GridSize.x; c++)
                for (int r = item.GridOrigin.y; r < item.GridOrigin.y + item.GridSize.y; r++)
                    if (c >= 0 && c < columns && r >= 0 && r < rows)
                        itemGrid[c, r] = item;
        }

        public void RemoveItem(InventoryItem item)
        {
            for (int c = item.GridOrigin.x; c < item.GridOrigin.x + item.GridSize.x; c++)
                for (int r = item.GridOrigin.y; r < item.GridOrigin.y + item.GridSize.y; r++)
                    if (c >= 0 && c < columns && r >= 0 && r < rows)
                        if (itemGrid[c, r] == item) itemGrid[c, r] = null;
        }

        // Returns the item at a cell, or null if empty.
        public InventoryItem GetItemAt(Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= columns || cell.y < 0 || cell.y >= rows)
                return null;
            return itemGrid[cell.x, cell.y];
        }

        public bool IsCellOccupied(Vector2Int cell)
        {
            return GetItemAt(cell) != null;
        }

    }
}

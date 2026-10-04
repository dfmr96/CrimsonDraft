#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class InventoryGrid : MonoBehaviour
    {
        [Header("Grid Config")]
        [SerializeField] private int columns = 4;
        [SerializeField] private int rows    = 4;

        [Header("Visual")]
        [SerializeField] private Image? gridBackground;

        [Header("Container Binding")]
        [SerializeField] private ContainerKind containerKind = ContainerKind.Operator;
        [SerializeField] private int           containerIndex;

        [Header("Neighbors")]
        [SerializeField] private InventoryGrid? left;
        [SerializeField] private InventoryGrid? right;
        [SerializeField] private InventoryGrid? up;
        [SerializeField] private InventoryGrid? down;

        private RectTransform? rectTransform;
        private float          cellSize;

        public event Action? Enabled;

        public int   Columns  => this.columns;
        public int   Rows     => this.rows;
        public float CellSize => this.cellSize;

        public ContainerId ContainerId => this.containerKind == ContainerKind.Operator
            ? ContainerId.Operator(this.containerIndex)
            : ContainerId.Storage;

        public InventoryGrid? Left  => this.left;
        public InventoryGrid? Right => this.right;
        public InventoryGrid? Up    => this.up;
        public InventoryGrid? Down  => this.down;

        void Awake()
        {
            this.rectTransform = GetComponent<RectTransform>();
            this.cellSize      = this.rectTransform.rect.width / this.columns;

            if (this.gridBackground == null) return;
            this.gridBackground.type                    = Image.Type.Tiled;
            this.gridBackground.pixelsPerUnitMultiplier = 1f;
        }

        void OnEnable() => this.Enabled?.Invoke();

        public Vector2 CellToLocal(Vector2Int cell)
        {
            float x = cell.x * this.cellSize - this.columns * this.cellSize * 0.5f;
            float y = -(cell.y * this.cellSize) + this.rows * this.cellSize * 0.5f;
            return new Vector2(x, y);
        }

        public Vector2 ItemLocalPosition(Vector2Int origin, int rotation, Vector2 baseSize)
        {
            var position = CellToLocal(origin);
            if (rotation == 1) position.x += baseSize.y;
            return position;
        }
    }
}

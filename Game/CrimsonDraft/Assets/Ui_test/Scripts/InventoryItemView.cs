#nullable enable

using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class InventoryItemView : MonoBehaviour
    {
        private Inventory.InventoryItem boundItem  = null!;
        private Vector2Int              gridOrigin;
        private int                     rotationState; // 0=0°  1=90°CW
        private bool                    inspected;
        private Image                   icon         = null!;
        private RectTransform           rectTransform = null!;

        public InventoryItem BoundItem  => this.boundItem;
        public ItemData      Data       => this.boundItem.Data;
        public Vector2Int    GridOrigin => this.gridOrigin;
        public bool        IsInspected => this.inspected;
        public int         Rotation    => this.rotationState;
        public int         SlotIndex   { get; private set; } = -1;

        // GridSize swaps X/Y when vertical (rotationState == 1)
        public Vector2Int GridSize
        {
            get
            {
                if (this.boundItem == null) return Vector2Int.one;
                return this.rotationState == 0
                    ? this.boundItem.Data.GridSize
                    : new Vector2Int(this.boundItem.Data.GridSize.y, this.boundItem.Data.GridSize.x);
            }
        }

        public void SetInspected(bool value)    => this.inspected  = value;
        public void SetGridOrigin(Vector2Int o) => this.gridOrigin = o;
        public void SetSlotIndex(int index)     => this.SlotIndex  = index;

        public void SetEquippedTint(bool equipped)
        {
            if (this.icon == null) this.icon = GetComponent<Image>();
            this.icon.color = equipped ? new Color(0.85f, 0.65f, 0f, 1f) : Color.white;
        }

        public void Bind(Inventory.InventoryItem item)
        {
            this.boundItem = item;

            if (this.icon == null) this.icon = GetComponent<Image>();
            if (item.Data.Icon != null)
            {
                this.icon.sprite         = item.Data.Icon;
                this.icon.preserveAspect = true;
            }

            gameObject.name = item.Data.DisplayName;
        }

        public void Initialize(Inventory.InventoryItem item, Vector2Int origin, float cellSize)
        {
            this.gridOrigin    = origin;
            this.rotationState = 0;

            this.rectTransform = GetComponent<RectTransform>();
            this.icon          = GetComponent<Image>();

            this.rectTransform.pivot     = new Vector2(0f, 1f);
            this.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            this.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            // sizeDelta always stores the BASE (unrotated) size
            this.rectTransform.sizeDelta = new Vector2(
                item.Data.GridSize.x * cellSize,
                item.Data.GridSize.y * cellSize);

            Bind(item);
        }

        // Toggle between horizontal (0°) and vertical (90°).
        public void Rotate()
        {
            this.rotationState = this.rotationState == 0 ? 1 : 0;
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            // 0 = horizontal (0°), 1 = vertical (-90° so item extends right+down from pivot)
            this.rectTransform.localEulerAngles = new Vector3(0f, 0f, -this.rotationState * 90f);
        }
    }
}

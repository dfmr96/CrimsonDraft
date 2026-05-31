using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class InventoryItem : MonoBehaviour
    {
        private ItemData data;
        private Vector2Int gridOrigin;
        private bool inspected;
        private Image icon;
        private RectTransform rectTransform;

        public ItemData    Data        => data;
        public Vector2Int  GridSize    => data != null ? data.gridSize : Vector2Int.one;
        public Vector2Int  GridOrigin  => gridOrigin;
        public bool        IsInspected => inspected;

        public void SetInspected(bool value) => inspected = value;

        public void Initialize(ItemData itemData, Vector2Int origin, float cellSize)
        {
            data        = itemData;
            gridOrigin  = origin;

            rectTransform = GetComponent<RectTransform>();
            icon          = GetComponent<Image>();

            // Pivot top-left so position = top-left corner of the item
            rectTransform.pivot     = new Vector2(0f, 1f);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            // Size = footprint in grid units
            rectTransform.sizeDelta = new Vector2(
                data.gridSize.x * cellSize,
                data.gridSize.y * cellSize);

            if (data.icon != null)
            {
                icon.sprite = data.icon;
                icon.preserveAspect = true;
            }

            gameObject.name = data.primaryName;
        }

        public void SetGridOrigin(Vector2Int origin)
        {
            gridOrigin = origin;
        }
    }
}

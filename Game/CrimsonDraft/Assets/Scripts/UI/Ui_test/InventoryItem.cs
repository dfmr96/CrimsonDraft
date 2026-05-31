using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class InventoryItem : MonoBehaviour
    {
        private ItemData      data;
        private Vector2Int    gridOrigin;
        private int           rotationState;   // 0=0°  1=90°CW  2=180°  3=270°CW
        private bool          inspected;
        private Image         icon;
        private RectTransform rectTransform;

        public ItemData    Data        => data;
        public Vector2Int  GridOrigin  => gridOrigin;
        public bool        IsInspected => inspected;
        public int         Rotation    => rotationState;

        // GridSize swaps X/Y when vertical (rotationState == 1)
        public Vector2Int GridSize
        {
            get
            {
                if (data == null) return Vector2Int.one;
                return rotationState == 0
                    ? data.gridSize
                    : new Vector2Int(data.gridSize.y, data.gridSize.x);
            }
        }

        public void SetInspected(bool value)  => inspected = value;
        public void SetGridOrigin(Vector2Int o) => gridOrigin = o;

        public void Initialize(ItemData itemData, Vector2Int origin, float cellSize)
        {
            data          = itemData;
            gridOrigin    = origin;
            rotationState = 0;

            rectTransform = GetComponent<RectTransform>();
            icon          = GetComponent<Image>();

            rectTransform.pivot     = new Vector2(0f, 1f);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            // sizeDelta always stores the BASE (unrotated) size
            rectTransform.sizeDelta = new Vector2(
                data.gridSize.x * cellSize,
                data.gridSize.y * cellSize);

            if (data.icon != null)
            {
                icon.sprite         = data.icon;
                icon.preserveAspect = true;
            }

            gameObject.name = data.primaryName;
        }

        // Toggle between horizontal (0°) and vertical (90°).
        public void Rotate()
        {
            rotationState = rotationState == 0 ? 1 : 0;
            ApplyRotation();
        }

        void ApplyRotation()
        {
            // 0 = horizontal (0°), 1 = vertical (-90° so item extends right+down from pivot)
            rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotationState * 90f);
        }
    }
}

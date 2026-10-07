#nullable enable

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class InventoryItemView : MonoBehaviour
    {
        private static readonly Color EquippedTint = new Color(0.6039216f, 0.62352943f, 0.36078432f, 1f);

        [SerializeField] private TMP_Text? quantityLabel;

        private ItemPlacement  placement     = null!;
        private Image          icon          = null!;
        private RectTransform  rectTransform = null!;

        public InventoryItem BoundItem    => this.placement.Item;
        public ItemData      Data         => this.placement.Item.Data;
        public Vector2Int    GridOrigin   => this.placement.Origin;
        public Vector2Int    GridSize     => this.placement.Footprint;
        public int           Rotation     => this.placement.Rotation;
        public bool          IsInspected  => this.placement.Item.IsExamined;
        public Vector2       RestPosition { get; private set; }

        public void SetInspected(bool value) => this.placement.Item.IsExamined = value;

        public void Bind(ItemPlacement placement, InventoryGrid grid)
        {
            this.placement     = placement;
            this.rectTransform = GetComponent<RectTransform>();
            this.icon          = GetComponent<Image>();

            this.rectTransform.pivot     = new Vector2(0f, 1f);
            this.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            this.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            var baseSize = placement.Item.Data.GridSize;
            this.rectTransform.sizeDelta        = new Vector2(baseSize.x * grid.CellSize, baseSize.y * grid.CellSize);
            this.rectTransform.localEulerAngles = new Vector3(0f, 0f, -placement.Rotation * 90f);
            this.RestPosition                   = grid.ItemLocalPosition(placement.Origin, placement.Rotation, this.rectTransform.sizeDelta);
            this.rectTransform.anchoredPosition = this.RestPosition;
            this.rectTransform.localScale       = Vector3.one;

            if (this.Data.Icon != null)
            {
                this.icon.sprite         = this.Data.Icon;
                this.icon.preserveAspect = true;
            }

            this.gameObject.name = this.Data.DisplayName;
            ResetTint();
            RefreshQuantity();
        }

        public void SetTint(Color color) => this.icon.color = color;

        public void ResetTint() => SetTint(this.BoundItem.IsEquipped ? EquippedTint : Color.white);

        private void RefreshQuantity()
        {
            if (this.quantityLabel == null) return;
            var count = ItemDisplayCount.For(this.BoundItem);
            this.quantityLabel.gameObject.SetActive(count.HasValue);
            if (count.HasValue) this.quantityLabel.text = count.Value.ToString();
        }
    }
}

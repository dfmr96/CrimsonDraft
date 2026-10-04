#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    public sealed class SelectorView : MonoBehaviour
    {
        private static readonly Color ColorSelectorNormal = Color.white;
        private static readonly Color ColorSelectorOnItem = Color.yellow;

        [SerializeField] private RectTransform selectorRect = null!;
        [SerializeField] private ItemTooltip?  tooltip;
        [SerializeField] private float         selectorPadding      = 1f;
        [SerializeField] private float         hoverScaleMultiplier = 1.07f;
        [SerializeField] private Sprite?       selectorSpriteNormal;
        [SerializeField] private Sprite?       selectorSpriteHold;

        private Image?             selectorImage;
        private InventoryItemView? hoveredItem;
        private RectTransform?     hoveredMeleeIcon;

        public bool IsVisible => this.selectorRect.gameObject.activeSelf;

        public void SetVisible(bool visible)
        {
            this.selectorRect.gameObject.SetActive(visible);
            if (!visible) HideTooltip();
        }

        public void HideTooltip()
        {
            if (this.tooltip != null) this.tooltip.Hide();
        }

        public void ShowTooltipAboveSelector()
        {
            if (this.tooltip != null) this.tooltip.ShowAboveSelector(this.selectorRect);
        }

        public void ShowAtCell(InventoryGrid grid, Vector2Int cell, InventoryItemView? item, Vector2Int? heldFootprint, bool holdStyle, bool showTooltip)
        {
            AttachTo(grid);
            ClearMeleeHover();
            this.selectorRect.SetAsLastSibling();

            bool holding = heldFootprint.HasValue || holdStyle;
            var  image   = Image();
            image.color  = holding ? Color.white : item != null ? ColorSelectorOnItem : ColorSelectorNormal;
            if (this.selectorSpriteNormal != null && this.selectorSpriteHold != null)
                image.sprite = holding ? this.selectorSpriteHold : this.selectorSpriteNormal;

            var size   = heldFootprint ?? (item != null ? item.GridSize : Vector2Int.one);
            var origin = heldFootprint.HasValue || item == null ? cell : item.GridOrigin;

            this.selectorRect.sizeDelta = new Vector2(
                size.x * grid.CellSize - this.selectorPadding * 2f,
                size.y * grid.CellSize - this.selectorPadding * 2f);

            bool highlight = !holding && item != null;
            if (this.hoveredItem == null || this.hoveredItem != item)
            {
                if (this.hoveredItem != null) ResetHover(this.hoveredItem);
                this.hoveredItem = null;
            }
            if (highlight)
            {
                ApplyHover(item!);
                this.hoveredItem = item;
            }

            float scale   = highlight ? this.hoverScaleMultiplier : 1f;
            var   basePos = grid.CellToLocal(origin) + new Vector2(this.selectorPadding, -this.selectorPadding);
            this.selectorRect.anchoredPosition = basePos + (1f - scale) * this.selectorRect.rect.center;
            this.selectorRect.localScale       = Vector3.one * scale;

            if (!showTooltip || this.tooltip == null) return;
            if (item == null)
            {
                this.tooltip.Hide();
                return;
            }

            bool hasSecondary = !string.IsNullOrEmpty(item.Data.SecondaryName);
            string label      = !item.IsInspected && hasSecondary ? item.Data.SecondaryName : item.Data.DisplayName;
            this.tooltip.ShowAtItem(label, item.GetComponent<RectTransform>());
        }

        public void ShowOnMelee(OperatorWidgetView widget)
        {
            if (this.hoveredItem != null) ResetHover(this.hoveredItem);
            this.hoveredItem = null;

            RectTransform slot = widget.MeleeSlotRoot;
            this.selectorRect.SetParent(slot.parent, false);
            this.selectorRect.pivot     = slot.pivot;
            this.selectorRect.anchorMin = slot.anchorMin;
            this.selectorRect.anchorMax = slot.anchorMax;
            this.selectorRect.sizeDelta = slot.sizeDelta + new Vector2(this.selectorPadding, this.selectorPadding) * 2f;
            this.selectorRect.anchoredPosition = slot.anchoredPosition + (1f - this.hoverScaleMultiplier) * this.selectorRect.rect.center;
            this.selectorRect.localScale       = Vector3.one * this.hoverScaleMultiplier;

            var image = Image();
            image.color = ColorSelectorOnItem;
            if (this.selectorSpriteNormal != null) image.sprite = this.selectorSpriteNormal;

            if (this.tooltip != null && widget.MeleeData != null)
                this.tooltip.ShowAtItem(widget.MeleeData.DisplayName, slot);

            RectTransform? icon = widget.MeleeIconRect;
            if (icon == null) return;
            icon.anchoredPosition = (1f - this.hoverScaleMultiplier) * icon.rect.center;
            icon.localScale       = Vector3.one * this.hoverScaleMultiplier;
            this.hoveredMeleeIcon = icon;
        }

        public void ClearMeleeHover()
        {
            if (this.hoveredMeleeIcon == null) return;
            this.hoveredMeleeIcon.anchoredPosition = Vector2.zero;
            this.hoveredMeleeIcon.localScale       = Vector3.one;
            this.hoveredMeleeIcon = null;
        }

        private void AttachTo(InventoryGrid grid)
        {
            if (this.selectorRect.parent != grid.transform)
                this.selectorRect.SetParent(grid.transform, false);
            this.selectorRect.anchorMin = new Vector2(0.5f, 0.5f);
            this.selectorRect.anchorMax = new Vector2(0.5f, 0.5f);
            this.selectorRect.pivot     = new Vector2(0f, 1f);
        }

        private void ApplyHover(InventoryItemView item)
        {
            var rt = item.GetComponent<RectTransform>();
            rt.anchoredPosition = item.RestPosition + (1f - this.hoverScaleMultiplier) * rt.rect.center;
            rt.localScale       = Vector3.one * this.hoverScaleMultiplier;
        }

        private static void ResetHover(InventoryItemView item)
        {
            var rt = item.GetComponent<RectTransform>();
            rt.anchoredPosition = item.RestPosition;
            rt.localScale       = Vector3.one;
        }

        private Image Image() => this.selectorImage ??= this.selectorRect.GetComponent<Image>();
    }
}

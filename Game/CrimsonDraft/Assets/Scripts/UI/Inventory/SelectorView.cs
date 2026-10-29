#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    public sealed class SelectorView : MonoBehaviour
    {
        // Resting cursor: the UI's olive. The "hover" child lights up olive over an item and green while
        // an item is held.
        internal static readonly Color ColorSelector = new Color(154f / 255f, 159f / 255f, 92f / 255f, 1f);
        private static readonly Color ColorHold     = ColorSelector; // holding / picking a combine target: same olive

        internal static readonly Color ColorCombineSource = new Color(1f, 0.15f, 0.15f, 1f);

        private const string HoverChildName   = "hover";
        private const string CombineMarkerName = "combineSource";

        [SerializeField] private RectTransform selectorRect = null!;
        [SerializeField] private ItemTooltip?  tooltip;
        [SerializeField] private float         selectorPadding      = 0f;
        [SerializeField] private Sprite?       selectorSpriteNormal;
        [SerializeField] private Sprite?       selectorSpriteHold;

        private Image?             selectorImage;
        private Image?             hoverImage;

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

        public void ShowTooltipAtSelector(string label)
        {
            if (this.tooltip != null) this.tooltip.ShowAtItem(label, this.selectorRect);
        }

        public void ShowAtCell(InventoryGrid grid, Vector2Int cell, InventoryItemView? item, Vector2Int? heldFootprint, bool holdStyle, bool showTooltip)
        {
            AttachTo(grid);
            this.selectorRect.SetAsLastSibling();

            bool holding = heldFootprint.HasValue || holdStyle;
            var  image   = Image();
            // Picking a combine target: the cursor itself turns green (holdStyle is only set for that).
            image.color  = holdStyle ? ColorHold : ColorSelector;
            if (this.selectorSpriteNormal != null) image.sprite = this.selectorSpriteNormal;

            var size   = heldFootprint ?? (item != null ? item.GridSize : Vector2Int.one);
            var origin = heldFootprint.HasValue || item == null ? cell : item.GridOrigin;

            this.selectorRect.sizeDelta = new Vector2(
                size.x * grid.CellSize - this.selectorPadding * 2f,
                size.y * grid.CellSize - this.selectorPadding * 2f);

            this.selectorRect.anchoredPosition = grid.CellToLocal(origin) + new Vector2(this.selectorPadding, -this.selectorPadding);
            this.selectorRect.localScale       = Vector3.one;

            // Olive over an item, green while holding one.
            SetHover(holding || item != null, holding ? ColorHold : ColorSelector);

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

        public void ShowOnLogo(OperatorWidgetView widget)
        {
            RectTransform? logo = widget.LogoRect;
            if (logo == null) return;

            PlaceOnRect(logo);
            HideTooltip();
        }

        // Parks the selector over a fixed (non-grid) widget element: the logo.
        private void PlaceOnRect(RectTransform slot)
        {
            SetHover(false, ColorSelector);

            this.selectorRect.SetParent(slot.parent, false);
            this.selectorRect.SetAsLastSibling();
            this.selectorRect.pivot     = slot.pivot;
            this.selectorRect.anchorMin = slot.anchorMin;
            this.selectorRect.anchorMax = slot.anchorMax;
            this.selectorRect.sizeDelta = slot.sizeDelta + new Vector2(this.selectorPadding, this.selectorPadding) * 2f;
            this.selectorRect.anchoredPosition = slot.anchoredPosition;
            this.selectorRect.localScale       = Vector3.one;

            var image = Image();
            image.color = ColorSelector;
            if (this.selectorSpriteNormal != null) image.sprite = this.selectorSpriteNormal;
        }


        private void AttachTo(InventoryGrid grid)
        {
            if (this.selectorRect.parent != grid.transform)
                this.selectorRect.SetParent(grid.transform, false);
            this.selectorRect.anchorMin = new Vector2(0.5f, 0.5f);
            this.selectorRect.anchorMax = new Vector2(0.5f, 0.5f);
            this.selectorRect.pivot     = new Vector2(0f, 1f);
        }

        // The "hover" child of the selector: a frame drawn over the cursor while it is on an item.
        // The hover's alpha is whatever its Image colour authors; only the RGB changes here.
        private void SetHover(bool active, Color color)
        {
            if (this.hoverImage == null)
            {
                var child = this.selectorRect.Find(HoverChildName);
                if (child == null) return;
                this.hoverImage = child.GetComponent<Image>();
            }
            if (this.hoverImage == null) return;

            // Size and position are authored on the hover's RectTransform (it stretches over the cursor).
            color.a = this.hoverImage.color.a;
            this.hoverImage.color = color;
            this.hoverImage.gameObject.SetActive(active);
        }

        /// <summary>
        /// Draws a red cursor-style frame around the item that started a combine. It is a child of the
        /// item's view, so it follows it and disappears with it when the grid re-renders.
        /// </summary>
        public void MarkCombineSource(InventoryItemView view) => MarkCombineSource(view, this.selectorSpriteNormal);

        public static void MarkCombineSource(InventoryItemView view, Sprite? sprite)
        {
            var rt     = (RectTransform)view.transform;
            var marker = rt.Find(CombineMarkerName) as RectTransform;
            if (marker == null)
            {
                var go = new GameObject(CombineMarkerName, typeof(RectTransform), typeof(Image));
                marker = (RectTransform)go.transform;
                marker.SetParent(rt, false);

                var img            = go.GetComponent<Image>();
                img.sprite         = sprite;
                img.type           = UnityEngine.UI.Image.Type.Sliced;
                img.color          = ColorCombineSource;
                img.raycastTarget  = false;
            }

            // Same footprint as the cursor: the item art is ItemCellSize per cell, the cursor a full cell.
            var   grid = view.GetComponentInParent<InventoryGrid>();
            float pad  = grid != null ? (grid.CellSize - InventoryGrid.ItemCellSize) * 0.5f : 0f;
            var   cells = view.Data.GridSize;

            marker.anchorMin = Vector2.zero;
            marker.anchorMax = Vector2.one;
            marker.offsetMin = new Vector2(-pad * cells.x, -pad * cells.y);
            marker.offsetMax = new Vector2(pad * cells.x, pad * cells.y);
            marker.SetAsLastSibling();
        }

        public void ClearCombineSource(InventoryItemView? view) => ClearCombineMarker(view);

        public static void ClearCombineMarker(InventoryItemView? view)
        {
            if (view == null) return;
            var marker = view.transform.Find(CombineMarkerName);
            if (marker != null) Object.Destroy(marker.gameObject);
        }

        private Image Image() => this.selectorImage ??= this.selectorRect.GetComponent<Image>();
    }
}

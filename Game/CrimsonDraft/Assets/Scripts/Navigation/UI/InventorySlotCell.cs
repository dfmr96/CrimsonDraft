#nullable enable

using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>
    /// Grid position marker for one inventory slot.
    /// Only shows occupied vs empty state — cursor, item info,
    /// and lifted icon are handled externally by InventoryView.
    /// </summary>
    public sealed class InventorySlotCell : MonoBehaviour
    {
        [SerializeField] private Image background = null!;
        [SerializeField] private Image iconImage   = null!;

        [SerializeField] private Color emptyColor         = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private Color occupiedColor      = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] private Color combineSourceColor = new Color(1f, 0.5f, 0f, 0.6f);

        public RectTransform RectTransform => (RectTransform)transform;

        public void Setup(InventorySlot slot, bool isCombineSource = false)
        {
            Color bgColor = isCombineSource  ? this.combineSourceColor
                          : slot.IsEmpty      ? this.emptyColor
                          :                     this.occupiedColor;
            this.background.color  = bgColor;
            this.iconImage.sprite  = slot.IsEmpty ? null : slot.Item!.Data.Icon;
            this.iconImage.enabled = !slot.IsEmpty && slot.Item!.Data.Icon != null;
        }
    }
}

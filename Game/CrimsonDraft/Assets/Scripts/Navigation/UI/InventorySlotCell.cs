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

        [SerializeField] private Color emptyColor    = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private Color occupiedColor = new Color(1f, 1f, 1f, 0.4f);

        public RectTransform RectTransform => (RectTransform)transform;

        public void Setup(InventorySlot slot)
        {
            this.background.color = slot.IsEmpty ? this.emptyColor : this.occupiedColor;
        }
    }
}

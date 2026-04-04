#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventorySlotCell : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel     = null!;
        [SerializeField] private TextMeshProUGUI detailLabel   = null!; // quantity / ammo count
        [SerializeField] private TextMeshProUGUI equippedLabel = null!;
        [SerializeField] private Image           cursorImage   = null!;
        [SerializeField] private Image           liftedImage   = null!; // shown when item is "held" in Reorder

        public void Setup(InventorySlot slot, bool isCursor, bool isLifted)
        {
            if (slot.IsEmpty)
            {
                this.nameLabel.text     = string.Empty;
                this.detailLabel.text   = string.Empty;
                this.equippedLabel.text = string.Empty;
            }
            else
            {
                this.nameLabel.text = slot.Item!.Data.DisplayName;

                if (slot.Item is AmmoBoxItem box)
                    this.detailLabel.text = $"\u00d7{box.Quantity}";
                else if (slot.Quantity > 1)
                    this.detailLabel.text = $"\u00d7{slot.Quantity}";
                else
                    this.detailLabel.text = string.Empty;

                this.equippedLabel.text = slot.Item.IsEquipped ? "[Eq]" : string.Empty;
            }

            this.cursorImage.enabled = isCursor;
            this.liftedImage.enabled = isLifted;
        }
    }
}

#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryItemRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel     = null!;
        [SerializeField] private TextMeshProUGUI equippedLabel = null!;
        [SerializeField] private Image           cursorImage   = null!;

        public void Setup(string displayName, string equippedBy, bool isCursor)
        {
            this.nameLabel.text     = displayName;
            this.equippedLabel.text = equippedBy.Length > 0 ? $"[Eq: {equippedBy}]" : string.Empty;
            this.cursorImage.enabled = isCursor;
        }
    }
}

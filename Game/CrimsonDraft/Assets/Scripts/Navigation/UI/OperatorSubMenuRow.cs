#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class OperatorSubMenuRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel   = null!;
        [SerializeField] private TextMeshProUGUI weaponLabel = null!;
        [SerializeField] private Image           cursorImage = null!;
        [SerializeField] private CanvasGroup     group       = null!;

        public int    SlotIndex    { get; private set; }
        public string OperatorName { get; private set; } = string.Empty;
        public string EquippedWeapon { get; private set; } = string.Empty;
        public bool   IsValid      { get; private set; }

        public void Setup(OperatorSubMenuEntry entry, bool isCursor)
        {
            this.SlotIndex           = entry.SlotIndex;
            this.OperatorName        = entry.OperatorName;
            this.EquippedWeapon      = entry.EquippedWeapon;
            this.IsValid             = entry.IsValid;
            this.nameLabel.text      = entry.OperatorName;
            this.weaponLabel.text    = entry.EquippedWeapon;
            this.cursorImage.enabled = isCursor;
            this.group.alpha         = entry.IsValid ? 1f : 0.4f;
            this.group.interactable  = entry.IsValid;
        }
    }
}

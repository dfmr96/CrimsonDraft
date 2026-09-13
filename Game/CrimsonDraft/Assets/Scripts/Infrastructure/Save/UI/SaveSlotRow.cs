#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    public sealed class SaveSlotRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label = null!;

        public void Bind(SaveSlotSummary summary, bool isSelected)
        {
            string prefix = isSelected ? "> " : "  ";
            this.label.text = summary.isEmpty
                ? $"{prefix}Slot {summary.slot + 1} — empty"
                : $"{prefix}{SaveSlotFormat.FormatOccupied(summary)}";

            gameObject.SetActive(true);
        }
    }
}

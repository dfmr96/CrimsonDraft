#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    /// <summary>
    /// One row of the world-space Load Game slot list in LoadGame_canva/Save_Games/Saves_data.
    /// Cursor is shown via ManualSelectScale (same look as MenuButtonSelectScale elsewhere in
    /// the main menu), driven manually by LoadGameSaveListView instead of Unity's EventSystem.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ManualSelectScale))]
    public sealed class LoadGameSlotRow : MonoBehaviour
    {
        private const string EmptyText = "#------------------------------------";

        private TextMeshProUGUI label = null!;
        private ManualSelectScale scaler = null!;

        private void Awake()
        {
            this.label  = GetComponent<TextMeshProUGUI>();
            this.scaler = GetComponent<ManualSelectScale>();
        }

        public void Bind(SaveSlotSummary summary, bool isSelected)
        {
            this.label.text = summary.isEmpty ? EmptyText : SaveSlotFormat.FormatOccupied(summary);
            this.scaler.SetSelected(isSelected);
        }
    }
}

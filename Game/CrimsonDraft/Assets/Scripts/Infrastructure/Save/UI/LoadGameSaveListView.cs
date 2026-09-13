#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    /// <summary>
    /// World-space Load Game panel view, living under MainMenu.unity's LoadGame_canva. Binds
    /// the 11 pre-placed <see cref="LoadGameSlotRow"/> rows in Saves_data and the Confirm_section
    /// sub-panel (Save_Name + Yes/No) to a <see cref="SaveSlotNavigator"/> via <see cref="ISaveSlotListView"/>.
    /// Only the first <c>rows.Length</c> save slots are shown (11 today) -- see LoadGameSlotRow.
    ///
    /// Yes/No aren't driven by SaveSlotNavigator's own Confirm/Cancel mapping -- MainMenuController
    /// reads <see cref="IsYesSelected"/> and toggles it via <see cref="SetConfirmSelection"/> on
    /// horizontal input, then dispatches to navigator.HandleConfirm()/HandleBack() accordingly, so
    /// the two options are genuinely selectable rather than implied by which physical button was pressed.
    /// </summary>
    public sealed class LoadGameSaveListView : MonoBehaviour, ISaveSlotListView
    {
        [SerializeField] private GameObject savesDataRoot = null!;
        [SerializeField] private LoadGameSlotRow[] rows = null!;
        [SerializeField] private GameObject confirmSection = null!;
        [SerializeField] private TextMeshProUGUI confirmSaveNameLabel = null!;
        [SerializeField] private ManualSelectScale yesOption = null!;
        [SerializeField] private ManualSelectScale noOption = null!;

        public bool IsYesSelected { get; private set; } = true;

        public void Show(IReadOnlyList<SaveSlotSummary> slots, int cursorIndex)
        {
            int count = Mathf.Min(slots.Count, this.rows.Length);
            for (int i = 0; i < count; i++)
                this.rows[i].Bind(slots[i], isSelected: i == cursorIndex);

            this.confirmSection.SetActive(false);
            this.savesDataRoot.SetActive(true);
        }

        public void ShowConfirm(SaveSlotSummary summary, string confirmVerb)
        {
            this.confirmSaveNameLabel.text = SaveSlotFormat.FormatOccupied(summary);
            SetConfirmSelection(yesSelected: true);

            this.savesDataRoot.SetActive(false);
            this.confirmSection.SetActive(true);
        }

        public void SetConfirmSelection(bool yesSelected)
        {
            this.IsYesSelected = yesSelected;
            this.yesOption.SetSelected(yesSelected);
            this.noOption.SetSelected(!yesSelected);
        }

        public void Hide()
        {
            this.savesDataRoot.SetActive(false);
            this.confirmSection.SetActive(false);
        }
    }
}

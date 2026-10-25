#nullable enable

using System;
using System.Collections.Generic;
using CrimsonDraft.Infrastructure.UI;
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

        [Header("Selection strip (optional -- unset in the save room)")]
        [Tooltip("Una sola tira que cicla entre slots y entre Yes/No. Vacio = solo la escala de ManualSelectScale.")]
        [SerializeField] private SelectionCursor? selector;
        [SerializeField] private RectTransform? yesAnchor;
        [SerializeField] private RectTransform? noAnchor;

        [Header("Save room (optional -- unset on the main menu)")]
        [Tooltip("Confirm_section/Title. When set, it asks 'Save to this slot?' / 'Overwrite this save?' instead of keeping its authored text.")]
        [SerializeField] private TextMeshProUGUI? confirmTitleLabel;
        [SerializeField] private float revealCharsPerSecond = 28f;
        [Tooltip("The canvas stays off until the panel is used: Show turns this GameObject on and Hide turns it off. Leave unticked on the main menu, where the camera travel owns the canvas.")]
        [SerializeField] private bool  toggleOwnGameObject;

        public bool IsYesSelected { get; private set; } = true;

        /// <summary>How many slots the panel has rows for -- callers hand in at most this many.</summary>
        public int SlotCapacity => this.rows.Length;

        // The save room's canvas is active in the scene from the start, so everything has to start
        // hidden; on the main menu the whole canvas is inactive until the camera arrives.
        private void Awake() => HideSections();

        public void Show(IReadOnlyList<SaveSlotSummary> slots, int cursorIndex)
        {
            if (this.toggleOwnGameObject) gameObject.SetActive(true);

            // Activate first: rows and their scalers only run Awake (and grab their components)
            // once their hierarchy is active, and Bind needs those.
            this.confirmSection.SetActive(false);
            this.savesDataRoot.SetActive(true);

            int count = Mathf.Min(slots.Count, this.rows.Length);
            for (int i = 0; i < count; i++)
                this.rows[i].Bind(slots[i], isSelected: i == cursorIndex);

            if (this.selector != null && cursorIndex < count)
                this.selector.FollowOrHide(this.rows[cursorIndex].SelectorAnchor);
        }

        public void ShowConfirm(SaveSlotSummary summary, string confirmVerb)
        {
            this.savesDataRoot.SetActive(false);
            this.confirmSection.SetActive(true);

            this.confirmSaveNameLabel.text = summary.isEmpty ? SaveSlotFormat.EmptyText : SaveSlotFormat.FormatOccupied(summary);
            if (this.confirmTitleLabel != null)
                this.confirmTitleLabel.text = summary.isEmpty ? "Save to this slot?" : "Overwrite this save?";
            SetConfirmSelection(yesSelected: true);
        }

        /// <summary>
        /// Back to the slot list after a save, with the written slot's name typed out
        /// character by character. <paramref name="onRevealed"/> runs once it has finished.
        /// </summary>
        /// <param name="revealSeconds">When above zero, the typing takes exactly this long instead of following
        /// the chars-per-second speed (the save room slows it to the pace of its Morse message).</param>
        public void ShowSaved(IReadOnlyList<SaveSlotSummary> slots, int savedIndex, Action onRevealed, float revealSeconds = 0f)
        {
            Show(slots, savedIndex);
            this.rows[savedIndex].Reveal(this.revealCharsPerSecond, onRevealed, revealSeconds);
        }

        public void SetConfirmSelection(bool yesSelected)
        {
            this.IsYesSelected = yesSelected;
            this.yesOption.SetSelected(yesSelected);
            this.noOption.SetSelected(!yesSelected);
            this.selector?.FollowOrHide(yesSelected ? this.yesAnchor : this.noAnchor);
        }

        public void Hide()
        {
            HideSections();
            this.selector?.SetVisible(false);
            if (this.toggleOwnGameObject) gameObject.SetActive(false);
        }

        private void HideSections()
        {
            this.savesDataRoot.SetActive(false);
            this.confirmSection.SetActive(false);
        }
    }
}

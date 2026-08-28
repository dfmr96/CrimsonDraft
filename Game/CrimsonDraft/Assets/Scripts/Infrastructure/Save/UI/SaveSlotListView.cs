#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    public sealed class SaveSlotListView : MonoBehaviour, ISaveSlotListView
    {
        [SerializeField] private GameObject      panel          = null!;
        [SerializeField] private Transform       slotListParent = null!;
        [SerializeField] private SaveSlotRow     slotRowPrefab  = null!;
        [SerializeField] private GameObject      confirmPanel   = null!;
        [SerializeField] private TextMeshProUGUI confirmLabel   = null!;

        private readonly List<SaveSlotRow> rows = new();

        public void Show(IReadOnlyList<SaveSlotSummary> slots, int cursorIndex)
        {
            while (this.rows.Count < slots.Count)
                this.rows.Add(Instantiate(this.slotRowPrefab, this.slotListParent));

            for (int i = 0; i < slots.Count; i++)
                this.rows[i].Bind(slots[i], isSelected: i == cursorIndex);

            for (int i = slots.Count; i < this.rows.Count; i++)
                this.rows[i].gameObject.SetActive(false);

            this.confirmPanel.SetActive(false);
            this.panel.SetActive(true);
        }

        public void ShowConfirm(SaveSlotSummary summary, string confirmVerb)
        {
            this.confirmLabel.text = $"{confirmVerb} slot {summary.slot + 1}? (Confirm / Cancel)";
            this.confirmPanel.SetActive(true);
        }

        public void Hide()
        {
            this.panel.SetActive(false);
            this.confirmPanel.SetActive(false);
        }
    }
}

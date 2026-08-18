#nullable enable

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    public sealed class SaveSlotListView : MonoBehaviour
    {
        [SerializeField] private GameObject      panel          = null!;
        [SerializeField] private Transform       slotListParent = null!;
        [SerializeField] private SaveSlotRow     slotRowPrefab  = null!;
        [SerializeField] private GameObject      confirmPanel      = null!;
        [SerializeField] private TextMeshProUGUI confirmLabel      = null!;
        [SerializeField] private Button          confirmYesButton  = null!;
        [SerializeField] private Button          confirmNoButton   = null!;

        private readonly List<SaveSlotRow> rows = new();

        public void Show(IReadOnlyList<SaveSlotSummary> slots, Action<SaveSlotSummary> onSlotClicked)
        {
            while (this.rows.Count < slots.Count)
                this.rows.Add(Instantiate(this.slotRowPrefab, this.slotListParent));

            for (int i = 0; i < slots.Count; i++)
            {
                var summary = slots[i];
                this.rows[i].Bind(summary, () => onSlotClicked(summary));
            }

            for (int i = slots.Count; i < this.rows.Count; i++)
                this.rows[i].gameObject.SetActive(false);

            this.confirmPanel.SetActive(false);
            this.panel.SetActive(true);
        }

        public void ShowConfirm(string message, Action onConfirmed)
        {
            this.confirmLabel.text = message;

            this.confirmYesButton.onClick.RemoveAllListeners();
            this.confirmNoButton.onClick.RemoveAllListeners();
            this.confirmYesButton.onClick.AddListener(() => onConfirmed());
            this.confirmNoButton.onClick.AddListener(() => this.confirmPanel.SetActive(false));

            this.confirmPanel.SetActive(true);
        }

        public void Hide()
        {
            this.panel.SetActive(false);
            this.confirmPanel.SetActive(false);
        }
    }
}

#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class ContainerView : MonoBehaviour
    {
        [SerializeField] private GameObject      panel          = null!;
        [SerializeField] private Transform       itemListParent = null!;
        [SerializeField] private TextMeshProUGUI itemRowPrefab  = null!;

        private readonly List<TextMeshProUGUI> rows = new();

        public void Show(IReadOnlyList<ItemData> items, int cursorIndex)
        {
            while (this.rows.Count < items.Count)
                this.rows.Add(Instantiate(this.itemRowPrefab, this.itemListParent));

            for (int i = items.Count; i < this.rows.Count; i++)
                this.rows[i].gameObject.SetActive(false);

            for (int i = 0; i < items.Count; i++)
            {
                this.rows[i].text = i == cursorIndex
                    ? $"> {items[i].DisplayName}"
                    : $"  {items[i].DisplayName}";
                this.rows[i].gameObject.SetActive(true);
            }

            this.panel.SetActive(true);
        }

        public void Hide() => this.panel.SetActive(false);
    }
}

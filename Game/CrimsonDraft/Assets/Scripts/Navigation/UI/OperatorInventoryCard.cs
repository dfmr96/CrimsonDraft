#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>
    /// One card per operator in the inventory screen.
    /// Shows portrait, name, and the operator's 4 inventory slots.
    /// Extend with equippedWeaponSlot, specialItemSlot, etc. as needed.
    /// </summary>
    public sealed class OperatorInventoryCard : MonoBehaviour
    {
        [Header("Operator Identity")]
        [SerializeField] private Image           portrait      = null!;
        [SerializeField] private TextMeshProUGUI nameLabel     = null!;

        [Header("Inventory Slots")]
        [SerializeField] private Transform        slotsContainer = null!;
        [SerializeField] private InventorySlotCell cellPrefab   = null!;

        private readonly List<InventorySlotCell> cells = new();
        private int operatorSlotIndex;

        public void Setup(OperatorRuntime op, int slotIndex)
        {
            this.operatorSlotIndex = slotIndex;
            this.nameLabel.text    = op.Data?.DisplayName ?? $"Operator {slotIndex}";

            bool hasPortrait        = op.Data?.Sprite != null;
            this.portrait.sprite    = hasPortrait ? op.Data!.Sprite : null;
            this.portrait.enabled   = hasPortrait;
        }

        public void RefreshSlots(IReadOnlyList<InventorySlot> allSlots, int cursorSlot, int liftedSlot)
        {
            int start = this.operatorSlotIndex * 4;
            int count = Mathf.Min(4, allSlots.Count - start);
            if (count <= 0) return;

            while (this.cells.Count < count)
                this.cells.Add(Instantiate(this.cellPrefab, this.slotsContainer));

            for (int i = 0; i < this.cells.Count; i++)
                this.cells[i].gameObject.SetActive(i < count);

            for (int i = 0; i < count; i++)
            {
                int idx = start + i;
                this.cells[i].Setup(allSlots[idx], isCursor: idx == cursorSlot, isLifted: idx == liftedSlot);
            }
        }
    }
}

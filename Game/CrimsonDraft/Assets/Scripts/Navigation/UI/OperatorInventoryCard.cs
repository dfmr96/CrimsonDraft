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
    /// Shows portrait and name. Each card owns 4 InventorySlotCells
    /// which are purely positional — cursor and item info live in InventoryView.
    /// Extend with equippedWeaponSlot, specialItemSlot, etc. as needed.
    /// </summary>
    public sealed class OperatorInventoryCard : MonoBehaviour
    {
        [Header("Operator Identity")]
        [SerializeField] private Image           portrait  = null!;
        [SerializeField] private TextMeshProUGUI nameLabel = null!;

        [Header("Inventory Slots")]
        [SerializeField] private Transform         slotsContainer = null!;
        [SerializeField] private InventorySlotCell cellPrefab     = null!;

        [Header("Equipped Weapon")]
        [SerializeField] private GameObject        equippedWeaponRoot  = null!;
        [SerializeField] private Image             equippedWeaponIcon  = null!;
        [SerializeField] private TextMeshProUGUI   equippedWeaponLabel = null!;

        private readonly List<InventorySlotCell> cells = new();
        private int             operatorSlotIndex;
        private OperatorRuntime? cachedOp;

        public void Setup(OperatorRuntime op, int slotIndex)
        {
            this.operatorSlotIndex = slotIndex;
            this.cachedOp          = op;
            this.nameLabel.text    = op.Data?.DisplayName ?? $"Operator {slotIndex}";

            bool hasPortrait      = op.Data?.Portrait != null;
            this.portrait.sprite  = hasPortrait ? op.Data!.Portrait : null;
            this.portrait.enabled = hasPortrait;

            RefreshEquippedWeapon();
        }

        public void RefreshSlots(IReadOnlyList<InventorySlot> allSlots, int combineSourceSlot = -1)
        {
            int start = this.operatorSlotIndex * 4;
            int count = Mathf.Min(4, allSlots.Count - start);
            if (count <= 0) return;

            while (this.cells.Count < count)
                this.cells.Add(Instantiate(this.cellPrefab, this.slotsContainer));

            for (int i = 0; i < this.cells.Count; i++)
                this.cells[i].gameObject.SetActive(i < count);

            for (int i = 0; i < count; i++)
                this.cells[i].Setup(allSlots[start + i], isCombineSource: (start + i) == combineSourceSlot);

            RefreshEquippedWeapon();
        }

        private void RefreshEquippedWeapon()
        {
            var weapon = this.cachedOp?.PrimaryWeapon as WeaponItem;
            this.equippedWeaponRoot.SetActive(weapon != null);
            if (weapon == null) return;

            this.equippedWeaponIcon.sprite  = weapon.Data.Icon;
            this.equippedWeaponIcon.enabled = weapon.Data.Icon != null;
            this.equippedWeaponLabel.text   = weapon.Data.DisplayName;
        }

        public RectTransform? GetCellRect(int localIndex) =>
            localIndex < this.cells.Count ? this.cells[localIndex].RectTransform : null;
    }
}

#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryView : MonoBehaviour
    {
        [Header("Slot Grid")]
        [SerializeField] private Transform          slotGridContainer    = null!;
        [SerializeField] private InventorySlotCell  cellPrefab           = null!;
        [SerializeField] private TextMeshProUGUI    operatorHeaderPrefab = null!;

        [Header("Roster Panel")]
        [SerializeField] private Transform         rosterContainer = null!;
        [SerializeField] private RosterOperatorRow rosterRowPrefab = null!;

        [Header("Context Menu")]
        [SerializeField] private GameObject         contextMenuRoot      = null!;
        [SerializeField] private Transform          contextMenuContainer = null!;
        [SerializeField] private ContextMenuItemRow contextMenuRowPrefab = null!;

        [Header("Examine Overlay")]
        [SerializeField] private GameObject      examineOverlayRoot = null!;
        [SerializeField] private TextMeshProUGUI examineText        = null!;

        private readonly List<InventorySlotCell>  cells       = new();
        private readonly List<TextMeshProUGUI>    headers     = new();
        private readonly List<RosterOperatorRow>  rosterRows  = new();
        private readonly List<ContextMenuItemRow> contextRows = new();

        public int ContextMenuActionCount => this.contextRows.Count;

        // ── Show / Hide ────────────────────────────────────────────────────────

        public void Show()  => gameObject.SetActive(true);
        public void Hide()  => gameObject.SetActive(false);

        // ── Slot grid ──────────────────────────────────────────────────────────

        public void RefreshSlots(IReadOnlyList<InventorySlot> slots, int cursorSlot, int liftedSlot = -1)
        {
            while (this.cells.Count < slots.Count)
                this.cells.Add(Instantiate(this.cellPrefab, this.slotGridContainer));

            for (int i = 0; i < this.cells.Count; i++)
                this.cells[i].gameObject.SetActive(i < slots.Count);

            for (int i = 0; i < slots.Count; i++)
                this.cells[i].Setup(slots[i], isCursor: i == cursorSlot, isLifted: i == liftedSlot);
        }

        public void SetOperatorHeaders(string[] names)
        {
            while (this.headers.Count < names.Length)
                this.headers.Add(Instantiate(this.operatorHeaderPrefab, this.slotGridContainer));

            for (int i = 0; i < this.headers.Count; i++)
                this.headers[i].gameObject.SetActive(i < names.Length);

            for (int i = 0; i < names.Length; i++)
                this.headers[i].text = names[i];
        }

        // ── Roster panel ───────────────────────────────────────────────────────

        public void RefreshRosterPanel(IOperatorRoster roster, IInventoryService inventory)
        {
            roster.EnsureInitialized();

            int presentCount = 0;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].IsPresent) presentCount++;

            while (this.rosterRows.Count < presentCount)
                this.rosterRows.Add(Instantiate(this.rosterRowPrefab, this.rosterContainer));

            for (int i = presentCount; i < this.rosterRows.Count; i++)
                this.rosterRows[i].gameObject.SetActive(false);

            int rowIdx = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var op = roster[i];
                if (!op.IsPresent) continue;

                string rawName = op.Data?.DisplayName ?? string.Empty;
                string name    = rawName.Length > 0 ? rawName : $"Slot {i}";
                int    wIdx    = inventory.GetEquippedWeaponIndex(i);
                string wpnName;
                if (wIdx >= 0)
                {
                    string dn     = inventory.Slots[wIdx].Item?.Data.DisplayName ?? "---";
                    var    weapon = op.EquippedWeapon;
                    wpnName = weapon != null ? $"{dn} ({weapon.CurrentAmmo}/{weapon.MaxAmmo})" : dn;
                }
                else
                {
                    wpnName = "---";
                }

                this.rosterRows[rowIdx].Setup(name, wpnName);
                this.rosterRows[rowIdx].gameObject.SetActive(true);
                rowIdx++;
            }
        }

        // ── Context menu ───────────────────────────────────────────────────────

        public void ShowContextMenu(InventoryItem item, int slotIndex)
        {
            this.contextMenuRoot.SetActive(true);

            foreach (var r in this.contextRows) Destroy(r.gameObject);
            this.contextRows.Clear();

            var actions = GetActionsForItem(item);
            for (int i = 0; i < actions.Count; i++)
            {
                var row = Instantiate(this.contextMenuRowPrefab, this.contextMenuContainer);
                row.Setup(actions[i], isCursor: i == 0, isEnabled: true);
                this.contextRows.Add(row);
            }
        }

        public void HideContextMenu() => this.contextMenuRoot.SetActive(false);

        public void SetContextMenuCursor(int index)
        {
            for (int i = 0; i < this.contextRows.Count; i++)
                this.contextRows[i].Setup(this.contextRows[i].Action, isCursor: i == index, isEnabled: true);
        }

        public ContextMenuAction GetContextMenuAction(int index) => this.contextRows[index].Action;

        // ── Examine overlay ────────────────────────────────────────────────────

        public void ShowExamineOverlay(InventoryItem item)
        {
            this.examineOverlayRoot.SetActive(true);
            this.examineText.text = $"{item.Data.DisplayName}\n\n{item.Data.ItemId}";
        }

        public void HideExamineOverlay() => this.examineOverlayRoot.SetActive(false);

        // ── Private helpers ────────────────────────────────────────────────────

        private static List<ContextMenuAction> GetActionsForItem(InventoryItem item) =>
            item.Data.ItemType switch
            {
                ItemType.Weapon     => item.IsEquipped
                                        ? new List<ContextMenuAction> { ContextMenuAction.Unequip, ContextMenuAction.Examine }
                                        : new List<ContextMenuAction> { ContextMenuAction.Equip,   ContextMenuAction.Examine },
                ItemType.AmmoBox    => new List<ContextMenuAction> { ContextMenuAction.Reload,  ContextMenuAction.Examine },
                ItemType.Consumable => new List<ContextMenuAction> { ContextMenuAction.Use,     ContextMenuAction.Examine },
                _                   => new List<ContextMenuAction> { ContextMenuAction.Examine }
            };
    }
}

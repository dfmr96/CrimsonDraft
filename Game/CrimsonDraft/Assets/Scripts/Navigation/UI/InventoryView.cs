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
        // ── Serialized references ──────────────────────────────────────────────

        [Header("Item List")]
        [SerializeField] private Transform        itemListContainer = null!;
        [SerializeField] private InventoryItemRow itemRowPrefab     = null!;

        [Header("Roster Panel")]
        [SerializeField] private Transform         rosterContainer  = null!;
        [SerializeField] private RosterOperatorRow rosterRowPrefab  = null!;

        [Header("Context Menu")]
        [SerializeField] private GameObject         contextMenuRoot      = null!;
        [SerializeField] private Transform          contextMenuContainer = null!;
        [SerializeField] private ContextMenuItemRow contextMenuRowPrefab = null!;

        [Header("Operator Sub-Menu")]
        [SerializeField] private GameObject        operatorSubMenuRoot = null!;
        [SerializeField] private Transform         subMenuContainer    = null!;
        [SerializeField] private OperatorSubMenuRow subMenuRowPrefab   = null!;

        [Header("Examine Overlay")]
        [SerializeField] private GameObject    examineOverlayRoot = null!;
        [SerializeField] private TextMeshProUGUI examineText      = null!;

        // ── Runtime state ──────────────────────────────────────────────────────

        private readonly List<InventoryItemRow>   itemRows    = new();
        private readonly List<RosterOperatorRow>  rosterRows  = new();
        private readonly List<ContextMenuItemRow> contextRows = new();
        private readonly List<OperatorSubMenuRow> subMenuRows = new();

        private int subMenuCursor;

        public ContextMenuAction CurrentSubMenuAction { get; private set; }
        public int               ContextMenuActionCount => this.contextRows.Count;

        // ── Show / Hide ────────────────────────────────────────────────────────

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        // ── Item list ──────────────────────────────────────────────────────────

        public void RefreshItemList(
            IReadOnlyList<InventoryItem> items,
            int cursorIndex,
            Dictionary<int, string> operatorNames)
        {
            // Grow pool
            while (this.itemRows.Count < items.Count)
                this.itemRows.Add(Instantiate(this.itemRowPrefab, this.itemListContainer));

            // Hide extras
            for (int i = items.Count; i < this.itemRows.Count; i++)
                this.itemRows[i].gameObject.SetActive(false);

            // Setup visible rows
            for (int i = 0; i < items.Count; i++)
            {
                var    item  = items[i];
                string eqBy  = string.Empty;
                if (item.IsEquipped && operatorNames.TryGetValue(item.EquippedBySlot, out var n))
                    eqBy = n;

                this.itemRows[i].Setup(item.Data.DisplayName, eqBy, isCursor: i == cursorIndex);
                this.itemRows[i].gameObject.SetActive(true);
            }
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

                string rawId    = op.Data?.OperatorId ?? string.Empty;
                string name     = rawId.Length > 0 ? rawId : $"Slot {i}";
                int    wIdx     = inventory.GetEquippedWeaponIndex(i);
                string wpnName  = wIdx >= 0 ? inventory.Items[wIdx].Data.DisplayName : "---";

                this.rosterRows[rowIdx].Setup(name, wpnName);
                this.rosterRows[rowIdx].gameObject.SetActive(true);
                rowIdx++;
            }
        }

        // ── Context menu ───────────────────────────────────────────────────────

        public void ShowContextMenu(InventoryItem item, int itemIndex)
        {
            this.contextMenuRoot.SetActive(true);
            this.contextActionIndex = 0; // keep field for cursor tracking handled by controller

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

        // ── Operator sub-menu ──────────────────────────────────────────────────

        public void ShowOperatorSubMenu(List<OperatorSubMenuEntry> entries, ContextMenuAction action)
        {
            this.CurrentSubMenuAction = action;
            this.subMenuCursor        = 0;
            this.operatorSubMenuRoot.SetActive(true);

            foreach (var r in this.subMenuRows) Destroy(r.gameObject);
            this.subMenuRows.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                var row = Instantiate(this.subMenuRowPrefab, this.subMenuContainer);
                row.Setup(entries[i], isCursor: i == 0);
                this.subMenuRows.Add(row);
            }
        }

        public void HideOperatorSubMenu() => this.operatorSubMenuRoot.SetActive(false);

        public void MoveOperatorSubMenuCursor(int delta)
        {
            if (this.subMenuRows.Count == 0) return;
            this.subMenuCursor = (this.subMenuCursor + delta + this.subMenuRows.Count) % this.subMenuRows.Count;
            for (int i = 0; i < this.subMenuRows.Count; i++)
                this.subMenuRows[i].Setup(
                    new OperatorSubMenuEntry(
                        this.subMenuRows[i].SlotIndex,
                        this.subMenuRows[i].OperatorName,
                        this.subMenuRows[i].EquippedWeapon,
                        this.subMenuRows[i].IsValid),
                    isCursor: i == this.subMenuCursor);
        }

        public int GetSelectedOperatorSlot() =>
            this.subMenuRows.Count > 0 ? this.subMenuRows[this.subMenuCursor].SlotIndex : -1;

        // ── Examine overlay ────────────────────────────────────────────────────

        public void ShowExamineOverlay(InventoryItem item)
        {
            this.examineOverlayRoot.SetActive(true);
            this.examineText.text = $"{item.Data.DisplayName}\n\n{item.Data.ItemId}";
        }

        public void HideExamineOverlay() => this.examineOverlayRoot.SetActive(false);

        // ── Private helpers ────────────────────────────────────────────────────

        private int contextActionIndex; // tracks current index for redraw

        private static List<ContextMenuAction> GetActionsForItem(InventoryItem item) =>
            item.Data.ItemType switch
            {
                ItemType.Weapon     => item.IsEquipped
                                        ? new List<ContextMenuAction> { ContextMenuAction.Unequip, ContextMenuAction.Examine }
                                        : new List<ContextMenuAction> { ContextMenuAction.Equip,   ContextMenuAction.Examine },
                ItemType.AmmoBox    => new List<ContextMenuAction> { ContextMenuAction.Reload, ContextMenuAction.Examine },
                ItemType.Consumable => new List<ContextMenuAction> { ContextMenuAction.Use,    ContextMenuAction.Examine },
                _                   => new List<ContextMenuAction> { ContextMenuAction.Examine }
            };
    }
}

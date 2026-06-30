#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryView : MonoBehaviour
    {
        [Header("Operator Cards")]
        [SerializeField] private Transform             cardsContainer = null!;
        [SerializeField] private OperatorInventoryCard cardPrefab     = null!;

        [Header("Cursor")]
        [SerializeField] private RectTransform cursorRect        = null!;
        [SerializeField] private Image         cursorIcon        = null!;
        [SerializeField] private Image         cursorHighlight   = null!;
        [SerializeField] private Color         normalCursorColor = Color.white;
        [SerializeField] private Color         combineColor      = new Color(1f, 0.8f, 0f, 1f);

        [Header("Item Info Panel")]
        [SerializeField] private GameObject      infoPanelRoot = null!;
        [SerializeField] private TextMeshProUGUI infoName      = null!;
        [SerializeField] private TextMeshProUGUI infoDetail    = null!;

        [Header("Context Menu")]
        [SerializeField] private GameObject            contextMenuRoot      = null!;
        [SerializeField] private Transform             contextMenuContainer = null!;
        [SerializeField] private InventoryActionButton actionButtonPrefab   = null!;

        [Header("Examine Overlay")]
        [SerializeField] private GameObject      examineOverlayRoot = null!;
        [SerializeField] private TextMeshProUGUI examineText        = null!;

        private readonly List<OperatorInventoryCard>  cards         = new();
        private readonly List<InventoryActionButton>  actionButtons = new();

        public int ContextMenuActionCount => this.actionButtons.Count;

        // ── Show / Hide ────────────────────────────────────────────────────────

        public void Show()
        {
            this.examineOverlayRoot.SetActive(false);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        // ── Operator cards ─────────────────────────────────────────────────────

        public void SetupCards(IOperatorRoster roster)
        {
            roster.EnsureInitialized();

            while (this.cards.Count < roster.Count)
                this.cards.Add(Instantiate(this.cardPrefab, this.cardsContainer));

            for (int i = 0; i < this.cards.Count; i++)
            {
                bool active = i < roster.Count;
                this.cards[i].gameObject.SetActive(active);
                if (active) this.cards[i].Setup(roster[i], i);
            }
        }

        // ── Slot refresh ───────────────────────────────────────────────────────

        public void RefreshSlots(IReadOnlyList<InventorySlot> slots, int cursorSlot, int liftedSlot = -1, int combineSourceSlot = -1)
        {
            bool inCombineMode = combineSourceSlot >= 0;
            foreach (var card in this.cards)
                card.RefreshSlots(slots, combineSourceSlot);

            MoveCursor(cursorSlot, inCombineMode);
            UpdateLiftedIcon(slots, liftedSlot);
            UpdateInfoPanel(slots, cursorSlot);
        }

        // ── Cursor ─────────────────────────────────────────────────────────────

        private void MoveCursor(int slotIndex, bool combineMode = false)
        {
            var cellRect = GetCellRect(slotIndex);
            if (cellRect == null) return;
            this.cursorRect.position   = cellRect.position;
            this.cursorRect.sizeDelta  = cellRect.sizeDelta;
            if (this.cursorHighlight != null)
                this.cursorHighlight.color = combineMode ? this.combineColor : this.normalCursorColor;
        }

        private void UpdateLiftedIcon(IReadOnlyList<InventorySlot> slots, int liftedSlot)
        {
            bool isLifting = liftedSlot >= 0
                          && liftedSlot < slots.Count
                          && !slots[liftedSlot].IsEmpty;

            this.cursorIcon.enabled = isLifting;
            if (isLifting)
                this.cursorIcon.sprite = slots[liftedSlot].Item!.Data.Icon;
        }

        // ── Item info panel ────────────────────────────────────────────────────

        private void UpdateInfoPanel(IReadOnlyList<InventorySlot> slots, int cursorSlot)
        {
            if (cursorSlot >= slots.Count || slots[cursorSlot].IsEmpty)
            {
                this.infoPanelRoot.SetActive(false);
                return;
            }

            this.infoPanelRoot.SetActive(true);
            var item = slots[cursorSlot].Item!;
            this.infoName.text = item.Data.DisplayName;
            this.infoDetail.text = item switch
            {
                AmmoBoxItem box => $"\u00d7{box.Quantity}",
                _               => slots[cursorSlot].Quantity > 1 ? $"\u00d7{slots[cursorSlot].Quantity}" : string.Empty
            };
        }

        // ── Context menu ───────────────────────────────────────────────────────

        public void ShowContextMenu(InventoryItem item, int slotIndex)
        {
            foreach (var b in this.actionButtons) Destroy(b.gameObject);
            this.actionButtons.Clear();

            var actions = GetActionsForItem(item);
            for (int i = 0; i < actions.Count; i++)
            {
                var btn = Instantiate(this.actionButtonPrefab, this.contextMenuContainer);
                btn.Setup(actions[i], isCursor: i == 0);
                this.actionButtons.Add(btn);
            }

            PositionContextMenuAboveSlot(slotIndex);
            this.contextMenuRoot.SetActive(true);
        }

        public void HideContextMenu() => this.contextMenuRoot.SetActive(false);

        public void SetContextMenuCursor(int index)
        {
            for (int i = 0; i < this.actionButtons.Count; i++)
                this.actionButtons[i].Setup(this.actionButtons[i].Action, isCursor: i == index);
        }

        public ContextMenuAction GetContextMenuAction(int index) => this.actionButtons[index].Action;

        // ── Examine overlay ────────────────────────────────────────────────────

        public void ShowExamineOverlay(InventoryItem item)
        {
            this.examineOverlayRoot.SetActive(true);
            this.examineText.text = $"{item.Data.DisplayName}\n\n{item.Data.ItemId}";
        }

        public void HideExamineOverlay() => this.examineOverlayRoot.SetActive(false);

        // ── Private helpers ────────────────────────────────────────────────────

        private RectTransform? GetCellRect(int slotIndex)
        {
            int cardIndex  = slotIndex / 4;
            int localIndex = slotIndex % 4;
            return cardIndex < this.cards.Count ? this.cards[cardIndex].GetCellRect(localIndex) : null;
        }

        private void PositionContextMenuAboveSlot(int slotIndex)
        {
            var cell = GetCellRect(slotIndex);
            if (cell == null) return;
            var ctxRect   = (RectTransform)this.contextMenuRoot.transform;
            ctxRect.pivot = new Vector2(0.5f, 0f);
            // TransformPoint converts cell-local top-center to world space
            ctxRect.position = cell.TransformPoint(new Vector3(0f, cell.rect.yMax + 8f, 0f));
        }

        private static List<ContextMenuAction> GetActionsForItem(InventoryItem item) =>
            item.Data.ItemType switch
            {
                ItemType.Weapon     => item.IsEquipped
                                        ? new List<ContextMenuAction> { ContextMenuAction.Unequip, ContextMenuAction.Combine, ContextMenuAction.Examine }
                                        : new List<ContextMenuAction> { ContextMenuAction.Equip,   ContextMenuAction.Combine, ContextMenuAction.Examine },
                ItemType.AmmoBox    => new List<ContextMenuAction> { ContextMenuAction.Combine, ContextMenuAction.Examine },
                ItemType.Consumable => item.Data is ConsumableData cd && cd.HealAmount > 0
                                        ? new List<ContextMenuAction> { ContextMenuAction.Use, ContextMenuAction.Combine, ContextMenuAction.Examine }
                                        : new List<ContextMenuAction> { ContextMenuAction.Combine, ContextMenuAction.Examine },
                ItemType.KeyItem    => new List<ContextMenuAction> { ContextMenuAction.Combine, ContextMenuAction.Examine },
                ItemType.SocketItem => new List<ContextMenuAction> { ContextMenuAction.Use, ContextMenuAction.Combine, ContextMenuAction.Examine },
                _                   => new List<ContextMenuAction> { ContextMenuAction.Combine, ContextMenuAction.Examine }
            };
    }
}

#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryController : IInitializable, IDisposable
    {
        private enum State { Closed, List, Reorder, ContextMenu, Combine }

        private readonly IInputService      inputService;
        private readonly IInventoryService  inventoryService;
        private readonly IOperatorRoster    roster;
        private readonly InventoryView      view;
        private readonly IInteractionCaster interactionCaster;

        private State state              = State.Closed;
        private int   cursorSlotIndex;
        private int   liftedSlotIndex    = -1;
        private int   combineSourceSlot  = -1;
        private int   contextActionIndex;

        [Preserve]
        public InventoryController(
            IInputService      inputService,
            IInventoryService  inventoryService,
            IOperatorRoster    roster,
            InventoryView      view,
            IInteractionCaster interactionCaster)
        {
            this.inputService      = inputService;
            this.inventoryService  = inventoryService;
            this.roster            = roster;
            this.view              = view;
            this.interactionCaster = interactionCaster;
        }

        void IInitializable.Initialize()
        {
            this.inputService.OpenInventory.performed += OnOpenInventory;
            this.inputService.UINavigate.performed    += OnUINavigate;
            this.inputService.UIConfirm.performed     += OnUIConfirm;
            this.inputService.UICancel.performed      += OnUICancel;
            this.inputService.UIBack.performed        += OnUIBack;
        }

        // ── Open / Close ───────────────────────────────────────────────────────

        private void OnOpenInventory(InputAction.CallbackContext _)
        {
            if (this.state != State.Closed) return;

            this.state           = State.List;
            this.cursorSlotIndex = 0;
            this.liftedSlotIndex = -1;
            Time.timeScale       = 0f;
            this.inputService.SwitchToUI();
            this.view.SetupCards(this.roster);
            RefreshView();
            this.view.Show();
        }

        private void Close()
        {
            this.state           = State.Closed;
            this.liftedSlotIndex = -1;
            this.view.HideContextMenu();
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        // ── Navigation ─────────────────────────────────────────────────────────

        private void OnUINavigate(InputAction.CallbackContext ctx)
        {
            var dir = ctx.ReadValue<Vector2>();
            int dx  = dir.x > 0.5f ? 1 : dir.x < -0.5f ? -1 : 0;
            int dy  = dir.y < -0.5f ? 1 : dir.y > 0.5f ? -1 : 0;

            switch (this.state)
            {
                case State.List:
                case State.Reorder:
                case State.Combine:
                {
                    if (dx == 0 && dy == 0) return;
                    // Total columns = rosterCount * 2. Each operator occupies 2 columns.
                    int totalCols = this.inventoryService.SlotCount / 2;
                    var (col, row) = SlotToColRow(this.cursorSlotIndex);
                    col = Mathf.Clamp(col + dx, 0, totalCols - 1);
                    row = Mathf.Clamp(row + dy, 0, 1);
                    this.cursorSlotIndex = ColRowToSlot(col, row);
                    RefreshView();
                    break;
                }
                case State.ContextMenu:
                {
                    int delta = dy;
                    if (delta == 0) return;
                    int count = this.view.ContextMenuActionCount;
                    if (count == 0) return;
                    this.contextActionIndex = (this.contextActionIndex + delta + count) % count;
                    this.view.SetContextMenuCursor(this.contextActionIndex);
                    break;
                }
            }
        }

        private void OnUIConfirm(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case State.List:
                    OpenContextMenuOrIgnore();
                    break;
                case State.Reorder:
                    DropItem();
                    break;
                case State.ContextMenu:
                    ExecuteContextMenuAction();
                    break;
                case State.Combine:
                    AttemptCombination();
                    break;
            }
        }

        private void OnUICancel(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case State.List:
                    Close();
                    break;
                case State.Reorder:
                    CancelReorder();
                    break;
                case State.ContextMenu:
                    this.state = State.List;
                    this.view.HideContextMenu();
                    RefreshView();
                    break;
                case State.Combine:
                    this.combineSourceSlot = -1;
                    this.state             = State.List;
                    RefreshView();
                    break;
            }
        }

        // UIBack = Y button — lifts item for Reorder
        private void OnUIBack(InputAction.CallbackContext _)
        {
            if (this.state != State.List) return;
            if (this.inventoryService.Slots[this.cursorSlotIndex].IsEmpty) return;
            this.liftedSlotIndex = this.cursorSlotIndex;
            this.state           = State.Reorder;
            RefreshView();
        }

        // ── Reorder ────────────────────────────────────────────────────────────

        private void DropItem()
        {
            this.inventoryService.MoveItem(this.liftedSlotIndex, this.cursorSlotIndex);
            this.liftedSlotIndex = -1;
            this.state           = State.List;
            RefreshView();
        }

        private void CancelReorder()
        {
            this.liftedSlotIndex = -1;
            this.state           = State.List;
            RefreshView();
        }

        // ── Combine ────────────────────────────────────────────────────────────

        private void AttemptCombination()
        {
            if (this.cursorSlotIndex == this.combineSourceSlot) return;
            var slot = this.inventoryService.Slots[this.cursorSlotIndex];
            if (slot.IsEmpty) return;
            if (!this.inventoryService.TryCombine(this.combineSourceSlot, this.cursorSlotIndex)) return;
            this.combineSourceSlot = -1;
            this.state             = State.List;
            RefreshView();
        }

        // ── Context menu ───────────────────────────────────────────────────────

        private void OpenContextMenuOrIgnore()
        {
            var slot = this.inventoryService.Slots[this.cursorSlotIndex];
            if (slot.IsEmpty) return;
            this.contextActionIndex = 0;
            this.view.ShowContextMenu(slot.Item!, this.cursorSlotIndex);
            this.state = State.ContextMenu;
        }

        private void ExecuteContextMenuAction()
        {
            var action  = this.view.GetContextMenuAction(this.contextActionIndex);
            int ownerOp = this.cursorSlotIndex / 4;

            this.view.HideContextMenu();

            if (action == ContextMenuAction.Combine)
            {
                this.combineSourceSlot = this.cursorSlotIndex;
                this.state             = State.Combine;
                RefreshView();
                return;
            }

            this.state = State.List;

            switch (action)
            {
                case ContextMenuAction.Equip:
                    this.inventoryService.EquipWeapon(this.cursorSlotIndex, ownerOp);
                    break;

                case ContextMenuAction.Unequip:
                    this.inventoryService.UnequipWeapon(this.cursorSlotIndex);
                    break;

                case ContextMenuAction.Use:
                {
                    var item = this.inventoryService.Slots[this.cursorSlotIndex].Item;
                    if (item?.Data.ItemType == ItemType.SocketItem)
                    {
                        int slotIndex = this.cursorSlotIndex;
                        var itemData  = item.Data;
                        if (!this.interactionCaster.CanUseItem(itemData)) break;
                        Close();
                        this.interactionCaster.TryUseItem(itemData);
                        this.inventoryService.RemoveItem(slotIndex);
                        return;
                    }
                    break;
                }

                case ContextMenuAction.Examine:
                {
                    var item = this.inventoryService.Slots[this.cursorSlotIndex].Item;
                    if (item != null) this.view.ShowExamineOverlay(item);
                    return;
                }
            }

            RefreshView();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void RefreshView() =>
            this.view.RefreshSlots(this.inventoryService.Slots, this.cursorSlotIndex, this.liftedSlotIndex, this.combineSourceSlot);

        // ── Grid index math ────────────────────────────────────────────────────
        //
        // Grid layout: 2 rows × (rosterCount * 2) columns.
        // Each operator owns a 2×2 block. Example with 2 operators:
        //
        //   col:   0    1  |  2    3
        //   row 0: [0]  [1] | [4]  [5]
        //   row 1: [2]  [3] | [6]  [7]
        //           Op 0        Op 1
        //
        // slotIndex → (col, row):
        //   operatorSlot = slotIndex / 4
        //   posInBlock   = slotIndex % 4
        //   row          = posInBlock / 2
        //   colWithinOp  = posInBlock % 2
        //   globalCol    = operatorSlot * 2 + colWithinOp
        //
        // (col, row) → slotIndex:
        //   operatorSlot = col / 2
        //   colWithinOp  = col % 2
        //   slotIndex    = operatorSlot * 4 + row * 2 + colWithinOp

        private static int ColRowToSlot(int col, int row)
        {
            int operatorSlot = col / 2;
            int colWithinOp  = col % 2;
            return operatorSlot * 4 + row * 2 + colWithinOp;
        }

        private static (int col, int row) SlotToColRow(int slotIndex)
        {
            int operatorSlot = slotIndex / 4;
            int posInBlock   = slotIndex % 4;
            int row          = posInBlock / 2;
            int colWithinOp  = posInBlock % 2;
            return (operatorSlot * 2 + colWithinOp, row);
        }

        void IDisposable.Dispose()
        {
            this.inputService.OpenInventory.performed -= OnOpenInventory;
            this.inputService.UINavigate.performed    -= OnUINavigate;
            this.inputService.UIConfirm.performed     -= OnUIConfirm;
            this.inputService.UICancel.performed      -= OnUICancel;
            this.inputService.UIBack.performed        -= OnUIBack;
        }
    }
}

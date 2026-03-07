#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryController : IInitializable, IDisposable
    {
        private enum State { Closed, List, ContextMenu, OperatorSubMenu }

        private readonly IInputService     inputService;
        private readonly IInventoryService inventoryService;
        private readonly IOperatorRoster   roster;
        private readonly InventoryView     view;

        private State             state              = State.Closed;
        private int               cursorIndex;
        private int               contextActionIndex;
        private ContextMenuAction pendingSubMenuAction;

        [Preserve]
        public InventoryController(
            IInputService     inputService,
            IInventoryService inventoryService,
            IOperatorRoster   roster,
            InventoryView     view)
        {
            this.inputService     = inputService;
            this.inventoryService = inventoryService;
            this.roster           = roster;
            this.view             = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.OpenInventory.performed += OnOpenInventory;
            this.inputService.UINavigate.performed    += OnUINavigate;
            this.inputService.UIConfirm.performed     += OnUIConfirm;
            this.inputService.UICancel.performed      += OnUICancel;
        }

        // ── Open / Close ───────────────────────────────────────────────────────

        private void OnOpenInventory(InputAction.CallbackContext _)
        {
            if (this.state != State.Closed) return;

            this.state       = State.List;
            this.cursorIndex = 0;
            this.inputService.SwitchToUI();
            RefreshView();
            this.view.Show();
        }

        private void Close()
        {
            this.state = State.Closed;
            this.view.HideContextMenu();
            this.view.HideOperatorSubMenu();
            this.view.Hide();
            this.inputService.SwitchToGameplay();
        }

        // ── Navigation ─────────────────────────────────────────────────────────

        private void OnUINavigate(InputAction.CallbackContext ctx)
        {
            var dir   = ctx.ReadValue<Vector2>();
            int delta = dir.y > 0.5f ? -1 : dir.y < -0.5f ? 1 : 0;
            if (delta == 0) return;

            switch (this.state)
            {
                case State.List:
                {
                    int count = this.inventoryService.Items.Count;
                    if (count == 0) return;
                    this.cursorIndex = (this.cursorIndex + delta + count) % count;
                    RefreshView();
                    break;
                }
                case State.ContextMenu:
                {
                    int count = this.view.ContextMenuActionCount;
                    if (count == 0) return;
                    this.contextActionIndex = (this.contextActionIndex + delta + count) % count;
                    this.view.SetContextMenuCursor(this.contextActionIndex);
                    break;
                }
                case State.OperatorSubMenu:
                    this.view.MoveOperatorSubMenuCursor(delta);
                    break;
            }
        }

        private void OnUIConfirm(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case State.List:
                    OpenContextMenu();
                    break;
                case State.ContextMenu:
                    ExecuteContextMenuAction();
                    break;
                case State.OperatorSubMenu:
                    ExecuteOperatorSubMenuAction();
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
                case State.ContextMenu:
                    this.state = State.List;
                    this.view.HideContextMenu();
                    break;
                case State.OperatorSubMenu:
                    this.state = State.ContextMenu;
                    this.view.HideOperatorSubMenu();
                    break;
            }
        }

        // ── Context menu ───────────────────────────────────────────────────────

        private void OpenContextMenu()
        {
            if (this.inventoryService.Items.Count == 0) return;

            this.contextActionIndex = 0;
            var item = this.inventoryService.Items[this.cursorIndex];
            this.view.ShowContextMenu(item, this.cursorIndex);
            this.state = State.ContextMenu;
        }

        private void ExecuteContextMenuAction()
        {
            var action = this.view.GetContextMenuAction(this.contextActionIndex);

            switch (action)
            {
                case ContextMenuAction.Equip:
                case ContextMenuAction.Unequip:
                case ContextMenuAction.Reload:
                    OpenOperatorSubMenu(action);
                    break;

                case ContextMenuAction.Use:
                    // TODO: implement consumable use effects
                    this.state = State.List;
                    this.view.HideContextMenu();
                    RefreshView();
                    break;

                case ContextMenuAction.Examine:
                    this.view.ShowExamineOverlay(this.inventoryService.Items[this.cursorIndex]);
                    break;
            }
        }

        private void OpenOperatorSubMenu(ContextMenuAction action)
        {
            this.pendingSubMenuAction = action;
            var entries = BuildOperatorSubMenuEntries(action);
            this.view.ShowOperatorSubMenu(entries, action);
            this.state = State.OperatorSubMenu;
        }

        private List<OperatorSubMenuEntry> BuildOperatorSubMenuEntries(ContextMenuAction action)
        {
            var entries = new List<OperatorSubMenuEntry>();
            this.roster.EnsureInitialized();

            for (int i = 0; i < this.roster.Count; i++)
            {
                var op = this.roster[i];
                if (!op.IsPresent) continue;

                bool isValid = action switch
                {
                    ContextMenuAction.Equip   => true,
                    ContextMenuAction.Unequip => this.inventoryService.Items[this.cursorIndex].EquippedBySlot == i,
                    ContextMenuAction.Reload  => this.inventoryService.CanReload(this.cursorIndex, i),
                    _                         => false
                };

                string rawId       = op.Data?.OperatorId ?? string.Empty;
                string name        = rawId.Length > 0 ? rawId : $"Slot {i}";
                int    equippedIdx = this.inventoryService.GetEquippedWeaponIndex(i);
                string equippedWpn = equippedIdx >= 0
                    ? this.inventoryService.Items[equippedIdx].Data.DisplayName
                    : "---";

                entries.Add(new OperatorSubMenuEntry(i, name, equippedWpn, isValid));
            }

            return entries;
        }

        private void ExecuteOperatorSubMenuAction()
        {
            int operatorSlot = this.view.GetSelectedOperatorSlot();
            if (operatorSlot < 0) return;

            switch (this.pendingSubMenuAction)
            {
                case ContextMenuAction.Equip:
                    this.inventoryService.EquipWeapon(this.cursorIndex, operatorSlot);
                    break;
                case ContextMenuAction.Unequip:
                    this.inventoryService.UnequipWeapon(this.cursorIndex);
                    break;
                case ContextMenuAction.Reload:
                    this.inventoryService.ReloadOperator(this.cursorIndex, operatorSlot);
                    if (this.cursorIndex >= this.inventoryService.Items.Count)
                        this.cursorIndex = Mathf.Max(0, this.inventoryService.Items.Count - 1);
                    break;
            }

            this.state = State.List;
            this.view.HideOperatorSubMenu();
            this.view.HideContextMenu();
            RefreshView();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void RefreshView()
        {
            var names = BuildOperatorNameMap();
            this.view.RefreshItemList(this.inventoryService.Items, this.cursorIndex, names);
            this.view.RefreshRosterPanel(this.roster, this.inventoryService);
        }

        private Dictionary<int, string> BuildOperatorNameMap()
        {
            var map = new Dictionary<int, string>();
            this.roster.EnsureInitialized();
            for (int i = 0; i < this.roster.Count; i++)
            {
                var op = this.roster[i];
                if (op.IsPresent)
                {
                    string id = op.Data?.OperatorId ?? string.Empty;
                    map[i] = id.Length > 0 ? id : $"Slot {i}";
                }
            }
            return map;
        }

        void IDisposable.Dispose()
        {
            this.inputService.OpenInventory.performed -= OnOpenInventory;
            this.inputService.UINavigate.performed    -= OnUINavigate;
            this.inputService.UIConfirm.performed     -= OnUIConfirm;
            this.inputService.UICancel.performed      -= OnUICancel;
        }
    }
}

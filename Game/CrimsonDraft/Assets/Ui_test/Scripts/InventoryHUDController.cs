#nullable enable

using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    public sealed class InventoryHUDController : IInitializable, System.IDisposable
    {
        private readonly IInventoryService inventoryService;
        private readonly ICombineService   combineService;
        private readonly IOperatorRoster   roster;
        private readonly GridCursor        cursor;
        private readonly ItemContextMenu   contextMenu;
        private readonly PartyPanelView    partyPanel;

        private InventoryItemView? combineSourceItem;

        [Preserve]
        public InventoryHUDController(
            IInventoryService inventoryService,
            ICombineService   combineService,
            IOperatorRoster   roster,
            GridCursor        cursor,
            ItemContextMenu   contextMenu,
            PartyPanelView    partyPanel)
        {
            this.inventoryService = inventoryService;
            this.combineService   = combineService;
            this.roster           = roster;
            this.cursor           = cursor;
            this.contextMenu      = contextMenu;
            this.partyPanel       = partyPanel;
        }

        public void Initialize()
        {
            this.contextMenu.OnUseRequested      += HandleUse;
            this.contextMenu.OnCombineRequested  += EnterCombineMode;
            this.cursor.OnCellConfirmed          += OnCellConfirmed;
            this.cursor.OnCombineTargetConfirmed += HandleCombineConfirm;
            this.cursor.OnItemMovedToNewGrid     += HandleItemMovedToNewGrid;
            this.cursor.OnCombineCancelled       += ExitCombineMode;
        }

        public void Dispose()
        {
            this.contextMenu.OnUseRequested      -= HandleUse;
            this.contextMenu.OnCombineRequested  -= EnterCombineMode;
            this.cursor.OnCellConfirmed          -= OnCellConfirmed;
            this.cursor.OnCombineTargetConfirmed -= HandleCombineConfirm;
            this.cursor.OnItemMovedToNewGrid     -= HandleItemMovedToNewGrid;
            this.cursor.OnCombineCancelled       -= ExitCombineMode;
        }

        // ── Context menu ────────────────────────────────────────────────────

        private void OnCellConfirmed(InventoryItemView view)
        {
            var options = new ContextMenuOptions
            {
                CanCombine = view.Data.Combinable,
                CanEquip   = view.Data.ItemType == ItemType.Weapon,
                CanUse     = view.Data.ItemType == ItemType.Consumable
                          || view.Data.ItemType == ItemType.KeyItem,
            };
            this.contextMenu.Open(view, options);
        }

        // ── Use / Equip ─────────────────────────────────────────────────────

        private void HandleUse(InventoryItemView view)
        {
            if (view.Data.ItemType == ItemType.Weapon)
            {
                HandleEquipWeapon(view);
                return;
            }
            this.inventoryService.TryUseKey(view.Data.ItemId);
        }

        private void HandleEquipWeapon(InventoryItemView view)
        {
            var weaponItem = view.BoundItem as WeaponItem;
            if (weaponItem == null) return;

            if (weaponItem.IsEquipped)
            {
                int opSlot  = weaponItem.EquippedBySlot;
                int wepSlot = weaponItem.EquippedWeaponSlot;
                weaponItem.ClearEquipped();
                this.partyPanel.GetWidget(opSlot)?.SetEquippedWeapon(null, wepSlot);
                this.roster[opSlot].SetEquippedWeapon(null, wepSlot);
            }
            else
            {
                int operatorSlot     = view.SlotIndex / 4;
                int targetWeaponSlot = (int)weaponItem.Data.WeaponSlot;

                IWeaponSlot? prev = targetWeaponSlot == 0
                    ? this.roster[operatorSlot].PrimaryWeapon
                    : this.roster[operatorSlot].SecondaryWeapon;
                (prev as InventoryItem)?.ClearEquipped();

                weaponItem.SetEquipped(operatorSlot, targetWeaponSlot);
                this.partyPanel.GetWidget(operatorSlot)?.SetEquippedWeapon(weaponItem, targetWeaponSlot);
                this.roster[operatorSlot].SetEquippedWeapon(weaponItem, targetWeaponSlot);
            }
        }

        // ── Combine ─────────────────────────────────────────────────────────

        private void EnterCombineMode(InventoryItemView source)
        {
            this.combineSourceItem   = source;
            this.cursor.IsCombineMode = true;
            source.GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 0.8f, 0f, 0.9f);
        }

        private void ExitCombineMode()
        {
            if (this.combineSourceItem != null)
                this.combineSourceItem.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            this.combineSourceItem    = null;
            this.cursor.IsCombineMode = false;
        }

        private void HandleCombineConfirm(InventoryItemView target)
        {
            if (this.combineSourceItem == null || target == this.combineSourceItem) return;

            int slotA = FindSlotIndex(this.combineSourceItem);
            int slotB = FindSlotIndex(target);

            this.inventoryService.TryCombine(slotA, slotB);
            ExitCombineMode();
        }

        // ── Grid movement ───────────────────────────────────────────────────

        private void HandleItemMovedToNewGrid(InventoryItemView item, InventoryGrid fromGrid)
        {
            var weapon = item.BoundItem as WeaponItem;
            if (weapon == null || !weapon.IsEquipped) return;

            int opSlot  = weapon.EquippedBySlot;
            int wepSlot = weapon.EquippedWeaponSlot;
            weapon.ClearEquipped();
            this.partyPanel.GetWidget(opSlot)?.SetEquippedWeapon(null, wepSlot);
            this.roster[opSlot].SetEquippedWeapon(null, wepSlot);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private int FindSlotIndex(InventoryItemView view)
        {
            var slots = this.inventoryService.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty && slots[i].Item?.Data == view.Data)
                    return i;
            return -1;
        }
    }
}

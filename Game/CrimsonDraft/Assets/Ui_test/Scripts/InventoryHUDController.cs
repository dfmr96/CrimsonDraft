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
        private readonly IItemSpawner      itemSpawner;
        private readonly IOperatorRoster   roster;
        private readonly GridCursor        cursor;
        private readonly ItemContextMenu   contextMenu;
        private readonly PartyPanelView    partyPanel;

        private InventoryItemView? combineSourceItem;

        [Preserve]
        public InventoryHUDController(
            IInventoryService inventoryService,
            ICombineService   combineService,
            IItemSpawner      itemSpawner,
            IOperatorRoster   roster,
            GridCursor        cursor,
            ItemContextMenu   contextMenu,
            PartyPanelView    partyPanel)
        {
            this.inventoryService = inventoryService;
            this.combineService   = combineService;
            this.itemSpawner      = itemSpawner;
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
                view.SetEquippedTint(false);
            }
            else
            {
                int operatorSlot = this.cursor.GetOperatorOf(view);
                if (operatorSlot < 0) return;
                int targetWeaponSlot = (int)weaponItem.Data.WeaponSlot;

                IWeaponSlot? prev = targetWeaponSlot == 0
                    ? this.roster[operatorSlot].PrimaryWeapon
                    : this.roster[operatorSlot].SecondaryWeapon;
                if (prev is InventoryItem prevItem)
                {
                    prevItem.ClearEquipped();
                    this.cursor.FindView(prevItem)?.SetEquippedTint(false);
                }

                weaponItem.SetEquipped(operatorSlot, targetWeaponSlot);
                this.partyPanel.GetWidget(operatorSlot)?.SetEquippedWeapon(weaponItem, targetWeaponSlot);
                this.roster[operatorSlot].SetEquippedWeapon(weaponItem, targetWeaponSlot);
                view.SetEquippedTint(true);
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

            InventoryItemView source = this.combineSourceItem;

            // Same stackable type — merge quantities (respect MaxStack)
            if (source.Data == target.Data && source.Data.Stackable)
            {
                int srcQty    = source.BoundItem is IHasDisplayCount ds ? ds.DisplayCount : 1;
                int tgtQty    = target.BoundItem is IHasDisplayCount dt ? dt.DisplayCount : 0;
                int canAbsorb = target.Data.MaxStack - tgtQty;
                int transfer  = Mathf.Min(srcQty, canAbsorb);

                if (transfer <= 0) { ExitCombineMode(); return; }

                target.BoundItem.AddQuantity(transfer);
                target.RefreshQuantity();

                int remaining = srcQty - transfer;
                if (remaining > 0)
                {
                    // Partial transfer — reduce source and keep it alive
                    source.BoundItem.AddQuantity(-transfer);
                    source.RefreshQuantity();
                }
                else
                {
                    source.OwnerGrid?.RemoveItem(source);
                    Object.Destroy(source.gameObject);
                }

                ExitCombineMode();
                return;
            }

            // AmmoBox + Weapon: reload
            var ammoItem   = (source.BoundItem as AmmoBoxItem) ?? (target.BoundItem as AmmoBoxItem);
            var weaponItem = (source.BoundItem as WeaponItem)  ?? (target.BoundItem as WeaponItem);

            if (ammoItem != null && weaponItem != null)
            {
                if (ammoItem.Data.Caliber != weaponItem.Data.Caliber ||
                    weaponItem.CurrentAmmo >= weaponItem.MaxAmmo)
                {
                    ExitCombineMode();
                    return;
                }

                var ammoView   = source.BoundItem is AmmoBoxItem ? source : target;
                var weaponView = source.BoundItem is WeaponItem  ? source : target;

                int needed = weaponItem.MaxAmmo - weaponItem.CurrentAmmo;
                int taken  = Mathf.Min(needed, ammoItem.Quantity);
                weaponItem.SetAmmo(weaponItem.CurrentAmmo + taken);
                ammoItem.AddQuantity(-taken);

                if (ammoItem.Quantity <= 0)
                {
                    ammoView.OwnerGrid?.RemoveItem(ammoView);
                    Object.Destroy(ammoView.gameObject);
                }
                else
                {
                    ammoView.RefreshQuantity();
                }

                weaponView.RefreshQuantity();
                ExitCombineMode();
                return;
            }

            // Recipe combine — visual layer only
            var resultData = this.combineService.TryGetResult(source.Data, target.Data);
            if (resultData == null) { ExitCombineMode(); return; }

            // Free cells first so HasSpace sees the space A and B would release
            InventoryGrid? preferredGrid = source.OwnerGrid;
            InventoryGrid? sourceGrid    = source.OwnerGrid;
            InventoryGrid? targetGrid    = target.OwnerGrid;

            sourceGrid?.RemoveItem(source);
            targetGrid?.RemoveItem(target);

            if (!this.itemSpawner.HasSpace(resultData))
            {
                // No space even after freeing — restore both items and cancel
                sourceGrid?.PlaceItem(source);
                targetGrid?.PlaceItem(target);
                ExitCombineMode();
                return;
            }

            Object.Destroy(source.gameObject);
            Object.Destroy(target.gameObject);
            this.itemSpawner.Spawn(resultData, preferredGrid);
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
            item.SetEquippedTint(false);
        }

    }
}

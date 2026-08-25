#nullable enable

using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;
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
        private readonly IInteractionCaster interactionCaster;
        private readonly InventorySfxData  sfx;

        private InventoryItemView? combineSourceItem;
        private InventoryItemView? splitSourceItem;
        private InventoryItemView? pendingSplitPhantom;

        private static readonly Color ColorCombineSourceTint = new Color(154f / 255f, 159f / 255f, 92f / 255f, 0.9f); // #9A9F5C

        [Preserve]
        public InventoryHUDController(
            IInventoryService  inventoryService,
            ICombineService    combineService,
            IItemSpawner       itemSpawner,
            IOperatorRoster    roster,
            GridCursor         cursor,
            ItemContextMenu    contextMenu,
            PartyPanelView     partyPanel,
            IInteractionCaster interactionCaster,
            InventorySfxData   sfx)
        {
            this.inventoryService  = inventoryService;
            this.combineService    = combineService;
            this.itemSpawner       = itemSpawner;
            this.roster            = roster;
            this.cursor            = cursor;
            this.contextMenu       = contextMenu;
            this.partyPanel        = partyPanel;
            this.interactionCaster = interactionCaster;
            this.sfx               = sfx;
        }

        public void Initialize()
        {
            this.contextMenu.OnUseRequested      += HandleUse;
            this.contextMenu.OnCombineRequested  += EnterCombineMode;
            this.contextMenu.OnSplitRequested    += HandleSplitRequested;
            this.cursor.OnCellConfirmed          += OnCellConfirmed;
            this.cursor.OnCombineTargetConfirmed += HandleCombineConfirm;
            this.cursor.OnItemPlaced             += HandleItemPlaced;
            this.cursor.OnCombineCancelled       += ExitCombineMode;
            this.cursor.OnSplitCancelled         += HandleSplitCancelled;
        }

        public void Dispose()
        {
            this.contextMenu.OnUseRequested      -= HandleUse;
            this.contextMenu.OnCombineRequested  -= EnterCombineMode;
            this.contextMenu.OnSplitRequested    -= HandleSplitRequested;
            this.cursor.OnCellConfirmed          -= OnCellConfirmed;
            this.cursor.OnCombineTargetConfirmed -= HandleCombineConfirm;
            this.cursor.OnItemPlaced             -= HandleItemPlaced;
            this.cursor.OnCombineCancelled       -= ExitCombineMode;
            this.cursor.OnSplitCancelled         -= HandleSplitCancelled;
        }

        // ── Context menu ────────────────────────────────────────────────────

        private void OnCellConfirmed(InventoryItemView view)
        {
            var options = new ContextMenuOptions
            {
                CanCombine = view.Data.Combinable,
                CanEquip   = view.Data.ItemType == ItemType.Weapon,
                CanUse     = (view.Data is ConsumableData cd && cd.HealAmount > 0)
                          || view.Data.ItemType == ItemType.KeyItem
                          || view.Data.ItemType == ItemType.SocketItem,
                CanSplit   = view.Data.ItemType == ItemType.AmmoBox
                          && view.BoundItem is AmmoBoxItem ammo
                          && ammo.Quantity > 1
                          && HasSpaceForSplit(view),
                CanInspect = true,
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

            if (view.Data.ItemType == ItemType.SocketItem)
            {
                if (!this.interactionCaster.CanUseItem(view.Data)) return;
                view.OwnerGrid?.RemoveItem(view);
                Object.Destroy(view.gameObject);
                this.cursor.RequestClose();
                this.interactionCaster.TryUseItem(view.Data);
                return;
            }

            if (view.Data.ItemType == ItemType.Consumable && view.Data is ConsumableData consumable)
            {
                HandleUseConsumable(view, consumable);
                return;
            }

            this.inventoryService.TryUseKey(view.Data.ItemId);
        }

        private void HandleUseConsumable(InventoryItemView view, ConsumableData consumable)
        {
            int operatorSlot = this.cursor.GetOperatorOf(view);
            if (operatorSlot < 0) return;

            if (this.roster[operatorSlot].IsAlive)
                this.roster[operatorSlot].Heal(consumable.HealAmount);

            for (int i = 0; i < this.inventoryService.SlotCount; i++)
            {
                if (this.inventoryService.Slots[i].Item == view.BoundItem)
                {
                    this.inventoryService.RemoveItem(i);
                    break;
                }
            }

            view.OwnerGrid?.RemoveItem(view);
            Object.Destroy(view.gameObject);
            this.partyPanel.Refresh();
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

        // ── Split ───────────────────────────────────────────────────────────

        private void HandleSplitRequested(InventoryItemView view)
        {
            if (view.BoundItem is not AmmoBoxItem sourceAmmo || sourceAmmo.Quantity <= 1)
            {
                this.sfx.PlayInvalidAction(this.cursor.gameObject);
                return;
            }

            int sourceOperator = this.cursor.GetOperatorOf(view);
            if (sourceOperator < 0 || !TryFindOperatorSlotForSplit(sourceOperator, out int targetOperator))
            {
                this.sfx.PlayInvalidAction(this.cursor.gameObject);
                return;
            }

            int splitOff = sourceAmmo.Quantity / 2;
            var newItem  = new AmmoBoxItem(sourceAmmo.Data, splitOff);

            if (!this.inventoryService.AddExistingItem(newItem, targetOperator))
            {
                this.sfx.PlayInvalidAction(this.cursor.gameObject);
                return;
            }

            sourceAmmo.AddQuantity(-splitOff);
            view.RefreshQuantity();

            InventoryGrid sourceGrid = view.OwnerGrid!;
            InventoryItemView newView = this.itemSpawner.SpawnFloating(newItem, sourceGrid, this.cursor.CurrentCell);

            this.splitSourceItem     = view;
            this.pendingSplitPhantom = newView;
            this.sfx.PlayDecide(this.cursor.gameObject);
            this.cursor.BeginHoldingSplitItem(newView, sourceGrid);
        }

        private void HandleSplitCancelled(InventoryItemView phantomView)
        {
            RemoveFromInventoryData(phantomView.BoundItem);

            if (this.splitSourceItem != null && phantomView.BoundItem is AmmoBoxItem phantomAmmo)
            {
                this.splitSourceItem.BoundItem.AddQuantity(phantomAmmo.Quantity);
                this.splitSourceItem.RefreshQuantity();
            }

            this.splitSourceItem     = null;
            this.pendingSplitPhantom = null;
            Object.Destroy(phantomView.gameObject);
        }

        private bool HasSpaceForSplit(InventoryItemView view)
        {
            int sourceOperator = this.cursor.GetOperatorOf(view);
            return sourceOperator >= 0 && TryFindOperatorSlotForSplit(sourceOperator, out _);
        }

        // Checks preferredOperator first, then every other living operator, for a free logical
        // inventory slot — so splitting isn't blocked just because the source operator is full
        // when a teammate still has room.
        private bool TryFindOperatorSlotForSplit(int preferredOperator, out int operatorSlot)
        {
            int slotsPerOp = this.roster.Count > 0
                ? this.inventoryService.SlotCount / this.roster.Count
                : 4;

            if (HasFreeSlotForOperator(preferredOperator, slotsPerOp))
            {
                operatorSlot = preferredOperator;
                return true;
            }

            for (int op = 0; op < this.roster.Count; op++)
            {
                if (op == preferredOperator) continue;
                if (HasFreeSlotForOperator(op, slotsPerOp))
                {
                    operatorSlot = op;
                    return true;
                }
            }

            operatorSlot = -1;
            return false;
        }

        private bool HasFreeSlotForOperator(int operatorSlot, int slotsPerOp)
        {
            if (operatorSlot < 0 || operatorSlot >= this.roster.Count) return false;
            if (!this.roster[operatorSlot].IsAlive) return false;

            int start = operatorSlot * slotsPerOp;
            for (int i = start; i < start + slotsPerOp; i++)
                if (this.inventoryService.Slots[i].IsEmpty) return true;
            return false;
        }

        // ── Combine ─────────────────────────────────────────────────────────

        private void EnterCombineMode(InventoryItemView source)
        {
            this.combineSourceItem   = source;
            this.cursor.IsCombineMode = true;
            source.GetComponent<UnityEngine.UI.Image>().color = ColorCombineSourceTint;
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
                    RemoveFromInventoryData(source.BoundItem);
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
                    this.sfx.PlayInvalidAction(this.cursor.gameObject);
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
                    RemoveFromInventoryData(ammoView.BoundItem);
                    Object.Destroy(ammoView.gameObject);
                }
                else
                {
                    ammoView.RefreshQuantity();
                }

                weaponView.RefreshQuantity();
                if (weaponItem.IsEquipped)
                    this.partyPanel.GetWidget(weaponItem.EquippedBySlot)?.SetEquippedWeapon(weaponItem, weaponItem.EquippedWeaponSlot);
                ExitCombineMode();
                return;
            }

            // Recipe combine — visual layer only
            var resultData = this.combineService.TryGetResult(source.Data, target.Data);
            if (resultData == null)
            {
                this.sfx.PlayInvalidAction(this.cursor.gameObject);
                ExitCombineMode();
                return;
            }

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
            this.sfx.PlayDecide(this.cursor.gameObject);
            ExitCombineMode();
        }

        // ── Grid movement ───────────────────────────────────────────────────

        private void HandleItemPlaced(InventoryItemView item)
        {
            if (item == this.pendingSplitPhantom)
            {
                this.splitSourceItem     = null;
                this.pendingSplitPhantom = null;
            }

            // Unequip if equipped weapon moved to a different operator's grid
            if (item.BoundItem is WeaponItem weapon && weapon.IsEquipped)
            {
                int newOpIndex = this.cursor.GetOperatorOf(item);
                if (newOpIndex >= 0 && newOpIndex != weapon.EquippedBySlot)
                {
                    int opSlot  = weapon.EquippedBySlot;
                    int wepSlot = weapon.EquippedWeaponSlot;
                    weapon.ClearEquipped();
                    this.partyPanel.GetWidget(opSlot)?.SetEquippedWeapon(null, wepSlot);
                    this.roster[opSlot].SetEquippedWeapon(null, wepSlot);
                    item.SetEquippedTint(false);
                }
            }

            // Sync item to correct operator block, then record 2D position
            int fromSlot = FindSlotIndex(item.BoundItem);
            if (fromSlot >= 0 && !TrySyncItemToOperatorSlot(item, fromSlot))
            {
                // Destination operator's 4 inventory slots are already full of distinct stacks —
                // the visual grid still had room (it's larger than the logical slot cap), so the
                // drop looked like it worked. Bounce it back so data and visuals don't diverge
                // (a diverged item would look moved here but still belong to its old operator
                // everywhere that reads from IInventoryService, e.g. the combat inventory panel).
                RevertPlacementToSlot(item, fromSlot);
                this.sfx.PlayInvalidAction(this.cursor.gameObject);
                return;
            }

            var origin = item.GridOrigin;
            for (int i = 0; i < this.inventoryService.SlotCount; i++)
            {
                if (this.inventoryService.Slots[i].Item == item.BoundItem)
                {
                    this.inventoryService.SetSlotPosition(i, origin.x, origin.y, item.Rotation);
                    break;
                }
            }
        }

        private int FindSlotIndex(InventoryItem item)
        {
            for (int i = 0; i < this.inventoryService.SlotCount; i++)
                if (this.inventoryService.Slots[i].Item == item) return i;
            return -1;
        }

        private void RemoveFromInventoryData(InventoryItem item)
        {
            int slot = FindSlotIndex(item);
            if (slot >= 0) this.inventoryService.RemoveItem(slot);
        }

        // Returns false if the target operator's 4 logical slots are already full of distinct
        // stacks — caller is responsible for reverting the visual placement in that case.
        private bool TrySyncItemToOperatorSlot(InventoryItemView item, int fromSlot)
        {
            int toOpIndex = this.cursor.GetOperatorOf(item);
            if (toOpIndex < 0) return true;

            int slotsPerOp = this.roster.Count > 0
                ? this.inventoryService.SlotCount / this.roster.Count
                : 4;

            int blockStart = toOpIndex * slotsPerOp;
            if (fromSlot >= blockStart && fromSlot < blockStart + slotsPerOp) return true;

            InventoryGrid toGrid = item.OwnerGrid!;

            for (int i = blockStart; i < blockStart + slotsPerOp; i++)
            {
                if (this.inventoryService.Slots[i].IsEmpty)
                {
                    this.inventoryService.MoveItem(fromSlot, i);
                    return true;
                }

                var occupantItem = this.inventoryService.Slots[i].Item!;
                var occupantView = this.cursor.FindView(occupantItem);
                if (occupantView == null || occupantView.OwnerGrid != toGrid)
                {
                    // Occupant displaced (held or moved elsewhere) — swap slots
                    this.inventoryService.MoveItem(fromSlot, i);
                    return true;
                }
            }

            return false;
        }

        private void RevertPlacementToSlot(InventoryItemView item, int slotIndex)
        {
            var saved = this.inventoryService.Slots[slotIndex];
            if (saved.GridCol < 0 || saved.GridRow < 0) return; // no prior position to revert to

            int slotsPerOp = this.roster.Count > 0
                ? this.inventoryService.SlotCount / this.roster.Count
                : 4;
            InventoryGrid? oldGrid = this.cursor.GetGridForOperator(slotIndex / slotsPerOp);
            if (oldGrid == null) return;

            item.OwnerGrid?.RemoveItem(item);

            while (item.Rotation != saved.GridRotation)
                item.Rotate();

            item.SetGridOrigin(new Vector2Int(saved.GridCol, saved.GridRow));
            item.SetOwnerGrid(oldGrid);

            var rt  = item.GetComponent<RectTransform>();
            rt.SetParent(oldGrid.transform, false);
            Vector2 pos = oldGrid.CellToLocal(item.GridOrigin);
            if (item.Rotation == 1) pos.x += rt.sizeDelta.y;
            rt.anchoredPosition = pos;

            oldGrid.PlaceItem(item);
        }

    }
}

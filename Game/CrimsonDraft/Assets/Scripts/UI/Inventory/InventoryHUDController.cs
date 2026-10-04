#nullable enable

using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.UI
{
    public sealed class InventoryHUDController : IInitializable, System.IDisposable
    {
        private static readonly Color ColorCombineSourceTint = new Color(154f / 255f, 159f / 255f, 92f / 255f, 0.9f);

        private readonly IInventoryService   inventory;
        private readonly InventoryPresenters presenters;
        private readonly GridCursor          cursor;
        private readonly ItemContextMenu     contextMenu;
        private readonly PartyPanelView      partyPanel;
        private readonly IInteractionCaster  interactionCaster;
        private readonly InventorySfxData    sfx;

        private InventoryItem? combineSource;

        [Preserve]
        public InventoryHUDController(
            IInventoryService   inventory,
            InventoryPresenters presenters,
            GridCursor          cursor,
            ItemContextMenu     contextMenu,
            PartyPanelView      partyPanel,
            IInteractionCaster  interactionCaster,
            InventorySfxData    sfx)
        {
            this.inventory         = inventory;
            this.presenters        = presenters;
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
            this.contextMenu.OnSplitRequested    += HandleSplit;
            this.cursor.OnCellConfirmed          += OpenContextMenu;
            this.cursor.OnCombineTargetConfirmed += HandleCombineConfirm;
            this.cursor.OnCombineCancelled       += ExitCombineMode;
            this.presenters.Rendered             += ReapplyCombineTint;
        }

        public void Dispose()
        {
            this.contextMenu.OnUseRequested      -= HandleUse;
            this.contextMenu.OnCombineRequested  -= EnterCombineMode;
            this.contextMenu.OnSplitRequested    -= HandleSplit;
            this.cursor.OnCellConfirmed          -= OpenContextMenu;
            this.cursor.OnCombineTargetConfirmed -= HandleCombineConfirm;
            this.cursor.OnCombineCancelled       -= ExitCombineMode;
            this.presenters.Rendered             -= ReapplyCombineTint;
        }

        private void OpenContextMenu(InventoryItemView view)
        {
            var data = view.Data;
            var options = new ContextMenuOptions
            {
                CanCombine = data.Combinable,
                CanEquip   = data.ItemType == ItemType.Weapon,
                CanUse     = (data is ConsumableData consumable && consumable.HealAmount > 0)
                          || data.ItemType == ItemType.KeyItem
                          || data.ItemType == ItemType.SocketItem,
                CanSplit   = data.ItemType == ItemType.AmmoBox && this.inventory.CanSplit(view.BoundItem),
                CanInspect = true,
            };
            this.contextMenu.Open(view, options);
        }

        private void HandleUse(InventoryItemView view)
        {
            var item = view.BoundItem;

            if (item is WeaponItem weapon)
            {
                ToggleEquip(weapon);
                return;
            }

            if (item.Data.ItemType == ItemType.SocketItem)
            {
                if (!this.interactionCaster.CanUseItem(item.Data)) return;
                this.inventory.Remove(item);
                this.cursor.RequestClose();
                this.interactionCaster.TryUseItem(item.Data);
                return;
            }

            if (item.Data is ConsumableData)
            {
                if (OperatorOf(item) is not int operatorSlot) return;
                this.inventory.TryUseConsumable(item, operatorSlot);
                this.partyPanel.Refresh();
                return;
            }

            this.inventory.TryUseKey(item.Data.ItemId);
        }

        private void ToggleEquip(WeaponItem weapon)
        {
            if (weapon.IsEquipped)
            {
                int previousOperator   = weapon.EquippedBySlot;
                int previousWeaponSlot = weapon.EquippedWeaponSlot;
                this.inventory.Unequip(weapon);
                this.partyPanel.GetWidget(previousOperator)?.SetEquippedWeapon(null, previousWeaponSlot);
                return;
            }

            if (OperatorOf(weapon) is not int operatorSlot) return;
            this.inventory.Equip(weapon, operatorSlot);
            this.partyPanel.GetWidget(operatorSlot)?.SetEquippedWeapon(weapon, weapon.EquippedWeaponSlot);
        }

        private void HandleSplit(InventoryItemView view)
        {
            if (this.inventory.TrySplit(view.BoundItem)) this.sfx.PlayDecide(this.cursor.gameObject);
            else                                         this.sfx.PlayInvalidAction(this.cursor.gameObject);
        }

        private void EnterCombineMode(InventoryItemView source)
        {
            this.combineSource        = source.BoundItem;
            this.cursor.IsCombineMode = true;
            source.SetTint(ColorCombineSourceTint);
        }

        private void ExitCombineMode()
        {
            if (this.combineSource != null)
            {
                var view = this.presenters.FindView(this.combineSource);
                if (view != null) view.ResetTint();
            }
            this.combineSource        = null;
            this.cursor.IsCombineMode = false;
        }

        private void ReapplyCombineTint(ContainerId _)
        {
            if (this.combineSource == null) return;
            var view = this.presenters.FindView(this.combineSource);
            if (view != null) view.SetTint(ColorCombineSourceTint);
        }

        private void HandleCombineConfirm(InventoryItemView target)
        {
            if (this.combineSource == null || target.BoundItem == this.combineSource) return;

            var source = this.combineSource;
            ExitCombineMode();

            if (!this.inventory.TryCombine(source, target.BoundItem, out var result))
            {
                this.sfx.PlayInvalidAction(this.cursor.gameObject);
                return;
            }

            this.sfx.PlayDecide(this.cursor.gameObject);
            if (result is WeaponItem weapon && weapon.IsEquipped)
                this.partyPanel.GetWidget(weapon.EquippedBySlot)?.SetEquippedWeapon(weapon, weapon.EquippedWeaponSlot);
        }

        private int? OperatorOf(InventoryItem item)
        {
            var id = this.inventory.FindContainerOf(item);
            return id.HasValue && id.Value.Kind == ContainerKind.Operator ? id.Value.Index : (int?)null;
        }
    }
}

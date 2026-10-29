#nullable enable

using System.Collections.Generic;
using System.Linq;
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
        private readonly IInventoryService   inventory;
        private readonly IOperatorRoster     roster;
        private readonly InventoryPresenters presenters;
        private readonly GridCursor          cursor;
        private readonly ItemContextMenu     contextMenu;
        private readonly PartyPanelView      partyPanel;
        private readonly IInteractionCaster  interactionCaster;
        private readonly InventorySfxData    sfx;
        private readonly InventoryOpenCloseController openClose;

        private InventoryItem? combineSource;
        private InventoryItem? pendingReveal; // a combine's result, hidden until the flying copy lands

        [Preserve]
        public InventoryHUDController(
            IInventoryService   inventory,
            IOperatorRoster     roster,
            InventoryPresenters presenters,
            GridCursor          cursor,
            ItemContextMenu     contextMenu,
            PartyPanelView      partyPanel,
            IInteractionCaster  interactionCaster,
            InventorySfxData    sfx,
            InventoryOpenCloseController openClose)
        {
            this.inventory         = inventory;
            this.roster            = roster;
            this.presenters        = presenters;
            this.cursor            = cursor;
            this.contextMenu       = contextMenu;
            this.partyPanel        = partyPanel;
            this.interactionCaster = interactionCaster;
            this.sfx               = sfx;
            this.openClose         = openClose;
        }

        public void Initialize()
        {
            this.contextMenu.OnUseRequested      += HandleUse;
            this.contextMenu.OnUseOnRequested    += HandleUseOn;
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
            this.contextMenu.OnUseOnRequested    -= HandleUseOn;
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
            var healTargets = data is ConsumableData ? BuildHealTargets() : null;
            var options = new ContextMenuOptions
            {
                CanCombine = data.Combinable,
                CanEquip   = data is WeaponData,
                CanUse     = (data is ConsumableData consumable && consumable.HealAmount > 0 && healTargets!.Any(t => t.Enabled))
                          || data.ItemType == ItemType.SocketItem,
                CanSplit   = data.ItemType == ItemType.AmmoBox && this.inventory.CanSplit(view.BoundItem),
                CanInspect = true,
                UseTargets = healTargets,
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

                // Insert only once the close animation is done: its FinishClose switches input
                // back to Gameplay, which would otherwise cut the socket's insert dialogue off
                // from its dialogue input and leave it stuck on screen. The item leaves the
                // inventory only once the insert succeeded; a returned item (the empty flask)
                // takes its exact spot so it can't be lost to a full grid.
                var data = item.Data;
                void InsertAfterClose()
                {
                    this.openClose.Closed -= InsertAfterClose;
                    if (!this.interactionCaster.TryUseItem(data)) return;
                    if (data is SocketItemData { ReturnedOnUse: { } returned }
                        && this.inventory.TryReplace(data.ItemId, returned)) return;
                    if (this.inventory.FindContainerOf(item) != null) this.inventory.Remove(item);
                }
                this.openClose.Closed += InsertAfterClose;
                this.cursor.RequestClose();
                return;
            }

            if (item.Data is ConsumableData)
            {
                if (OperatorOf(item) is not int operatorSlot) return;
                UseConsumableOn(item, operatorSlot);
                return;
            }

            this.inventory.TryUseKey(item.Data.ItemId);
        }

        // The context menu's operator list: every present operator is listed; only a living one can be picked,
        // a dead one shows up dimmed.
        private IReadOnlyList<UseTarget> BuildHealTargets()
        {
            var targets = new List<UseTarget>();
            for (int i = 0; i < this.roster.Count; i++)
            {
                var op = this.roster[i];
                if (!op.IsPresent) continue;
                targets.Add(new UseTarget(i, op.Data?.CombatName ?? $"OP {i + 1}", op.IsAlive));
            }
            return targets;
        }

        private void HandleUseOn(InventoryItemView view, int operatorSlot) => UseConsumableOn(view.BoundItem, operatorSlot);

        private void UseConsumableOn(InventoryItem item, int operatorSlot)
        {
            if (!this.inventory.TryUseConsumable(item, operatorSlot))
            {
                this.sfx.PlayInvalidAction(this.cursor.gameObject);
                return;
            }

            this.partyPanel.Refresh();
            this.partyPanel.GetWidget(operatorSlot)?.FlashHeal();
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
            this.cursor.Selector.MarkCombineSource(source);
        }

        private void ExitCombineMode()
        {
            if (this.combineSource != null)
                this.cursor.Selector.ClearCombineSource(this.presenters.FindView(this.combineSource));
            this.combineSource        = null;
            this.cursor.IsCombineMode = false;
        }

        // The grid re-renders its item views on every change: the red frame lives on the old view,
        // and a combine's result stays hidden until the flying copy has reached it.
        private void ReapplyCombineTint(ContainerId _)
        {
            if (this.pendingReveal != null)
                this.presenters.FindView(this.pendingReveal)?.SetShown(false);

            if (this.combineSource == null) return;
            var view = this.presenters.FindView(this.combineSource);
            if (view != null) this.cursor.Selector.MarkCombineSource(view);
        }

        private void RevealCombineResult(InventoryItem result)
        {
            if (this.pendingReveal != result) return;
            this.pendingReveal = null;
            this.presenters.FindView(result)?.SetShown(true);
        }

        private void HandleCombineConfirm(InventoryItemView target)
        {
            if (this.combineSource == null || target.BoundItem == this.combineSource) return;

            var source     = this.combineSource;
            var sourceView = this.presenters.FindView(source);
            // The copy that flies onto the target is made now, while the source's view still exists.
            var flight = sourceView != null ? CombineFlyVisual.Begin(sourceView, target) : null;
            ExitCombineMode();

            if (!this.inventory.TryCombine(source, target.BoundItem, out var result))
            {
                flight?.Cancel();
                this.sfx.PlayInvalidAction(this.cursor.gameObject);
                return;
            }

            // A recipe's brand-new item shows up only once the flying copy has reached the target.
            if (flight != null)
            {
                bool isNew = result != null && result != source && result != target.BoundItem;
                if (isNew)
                {
                    var created        = result!;
                    this.pendingReveal = created;
                    this.presenters.FindView(created)?.SetShown(false);
                    flight.Launch(() => RevealCombineResult(created));
                }
                else flight.Launch();
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

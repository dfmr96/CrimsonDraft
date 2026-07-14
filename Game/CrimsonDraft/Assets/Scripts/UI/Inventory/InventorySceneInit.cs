#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    public class InventorySceneInit
    {
        private readonly IInventoryService inventoryService;
        private readonly IItemSpawner      itemSpawner;
        private readonly InventoryGridGroup gridGroup;
        private readonly PartyPanelView    partyPanel;
        private readonly GridCursor        cursor;
        private readonly IOperatorRoster   roster;

        private bool synced;

        [Preserve]
        public InventorySceneInit(
            IInventoryService inventoryService,
            IItemSpawner      itemSpawner,
            InventoryGridGroup gridGroup,
            PartyPanelView    partyPanel,
            GridCursor        cursor,
            IOperatorRoster   roster)
        {
            this.inventoryService = inventoryService;
            this.itemSpawner      = itemSpawner;
            this.gridGroup        = gridGroup;
            this.partyPanel       = partyPanel;
            this.cursor           = cursor;
            this.roster           = roster;
        }

        public void EnsureSynced()
        {
            int slotsPerOperator = this.roster.Count > 0
                ? this.inventoryService.SlotCount / this.roster.Count
                : 4;

            // Remove views left over from a previous open whose backing item no longer
            // exists (e.g. a consumable fully used up in combat while the UI was in memory).
            for (int g = 0; g < this.gridGroup.Count; g++)
            {
                InventoryGrid grid = this.gridGroup.GetGrid(g);
                if (grid == null) continue;

                foreach (var view in grid.GetComponentsInChildren<InventoryItemView>(true))
                {
                    if (ItemStillInInventory(view.BoundItem)) continue;
                    grid.RemoveItem(view);
                    Object.Destroy(view.gameObject);
                }
            }

            // Refresh displayed quantities on views that survived from a previous open
            // (e.g. ammo consumed in combat while the UI was already in memory).
            for (int i = 0; i < this.inventoryService.SlotCount; i++)
            {
                var slot = this.inventoryService.Slots[i];
                if (slot.IsEmpty) continue;
                this.cursor.FindView(slot.Item!)?.RefreshQuantity();
            }

            // Pass 1: Restore items that have a saved 2D position to their exact cell.
            for (int i = 0; i < this.inventoryService.SlotCount; i++)
            {
                var slot = this.inventoryService.Slots[i];
                if (slot.IsEmpty) continue;
                if (this.cursor.FindView(slot.Item!) != null) continue;
                if (slot.GridCol < 0 || slot.GridRow < 0) continue;

                int opIndex        = i / slotsPerOperator;
                InventoryGrid grid = this.gridGroup.GetGrid(opIndex);
                if (!this.itemSpawner.SpawnAt(slot.Item!, grid, slot.GridCol, slot.GridRow, slot.GridRotation))
                    this.inventoryService.SetSlotPosition(i, -1, -1, 0); // cell blocked — handle in pass 2
            }

            // Pass 2: Spawn remaining items (new or position-blocked) using first-available slot.
            for (int i = 0; i < this.inventoryService.SlotCount; i++)
            {
                var slot = this.inventoryService.Slots[i];
                if (slot.IsEmpty) continue;
                if (this.cursor.FindView(slot.Item!) != null) continue;

                int opIndex        = i / slotsPerOperator;
                InventoryGrid grid = this.gridGroup.GetGrid(opIndex);
                this.itemSpawner.SpawnExisting(slot.Item!, grid);

                var view = this.cursor.FindView(slot.Item!);
                if (view != null)
                    this.inventoryService.SetSlotPosition(i, view.GridOrigin.x, view.GridOrigin.y, view.Rotation);
            }

            if (this.synced) return;
            this.synced = true;

            // Apply equipped-weapon visuals once on first open
            for (int opIndex = 0; opIndex < this.roster.Count; opIndex++)
            {
                var op = this.roster[opIndex];
                if (!op.IsPresent) continue;

                if (op.PrimaryWeapon is WeaponItem w0)
                {
                    this.cursor.FindView(w0)?.SetEquippedTint(true);
                    this.partyPanel.GetWidget(opIndex)?.SetEquippedWeapon(w0, 0);
                }
                if (op.SecondaryWeapon is WeaponItem w1)
                {
                    this.cursor.FindView(w1)?.SetEquippedTint(true);
                    this.partyPanel.GetWidget(opIndex)?.SetEquippedWeapon(w1, 1);
                }
            }
        }

        private bool ItemStillInInventory(InventoryItem item)
        {
            for (int i = 0; i < this.inventoryService.SlotCount; i++)
                if (this.inventoryService.Slots[i].Item == item) return true;
            return false;
        }
    }
}

#nullable enable

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
            if (this.synced) return;
            this.synced = true;

            // Spawn a view for every item already in the service (loaded by InventoryBootstrap)
            for (int i = 0; i < this.inventoryService.SlotCount; i++)
            {
                var slot = this.inventoryService.Slots[i];
                if (slot.IsEmpty) continue;

                int opIndex        = i / 4;
                InventoryGrid grid = this.gridGroup.GetGrid(opIndex);
                this.itemSpawner.SpawnExisting(slot.Item!, grid);
            }

            // Apply equipped-weapon visuals
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
    }
}

#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation
{
    public sealed class InventoryBootstrap : IInitializable
    {
        private readonly StartingLoadout   loadout;
        private readonly IInventoryService inventory;

        [Preserve]
        public InventoryBootstrap(StartingLoadout loadout, IInventoryService inventory)
        {
            this.loadout   = loadout;
            this.inventory = inventory;
        }

        private bool initialized;

        public void Initialize()
        {
            if (this.initialized) return;
            this.initialized = true;

            foreach (var entry in this.loadout.Items)
                this.inventory.AddItem(entry.item, entry.quantity);

            for (int slot = 0; slot < this.loadout.DefaultWeapons.Length; slot++)
            {
                var weaponData = this.loadout.DefaultWeapons[slot];
                if (weaponData == null) continue;

                for (int i = 0; i < this.inventory.Items.Count; i++)
                {
                    if (this.inventory.Items[i].Data == weaponData && this.inventory.Items[i].EquippedBySlot < 0)
                    {
                        this.inventory.EquipWeapon(i, slot);
                        break;
                    }
                }
            }
        }
    }
}

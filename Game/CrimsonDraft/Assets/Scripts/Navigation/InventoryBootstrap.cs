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
                this.inventory.AddItem(entry.item, entry.operatorSlot, entry.quantity);

            for (int slot = 0; slot < this.loadout.DefaultWeapons.Length; slot++)
            {
                var weaponData = this.loadout.DefaultWeapons[slot];
                if (weaponData == null) continue;

                this.inventory.AddItem(weaponData, operatorSlot: slot);

                // Find the slot index we just added and equip it
                int start = slot * 4;
                for (int i = start; i < start + 4; i++)
                {
                    if (this.inventory.Slots[i].Item?.Data == weaponData
                        && this.inventory.Slots[i].Item!.EquippedBySlot < 0)
                    {
                        this.inventory.EquipWeapon(i, slot);
                        if (this.inventory.Slots[i].Item is WeaponItem w)
                            w.SetAmmo(w.MaxAmmo);
                        break;
                    }
                }
            }
        }
    }
}

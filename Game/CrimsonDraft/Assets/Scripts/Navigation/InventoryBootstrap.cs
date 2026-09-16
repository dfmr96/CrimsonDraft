#nullable enable

using System;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    public sealed class InventoryBootstrap : IInitializable, IDisposable
    {
        private readonly StartingLoadout        loadout;
        private readonly IInventoryService      inventory;
        private readonly InventoryStateRegistry registry;
        private readonly IOperatorRoster        roster;
        private bool initialized;

        [Preserve]
        public InventoryBootstrap(
            StartingLoadout        loadout,
            IInventoryService      inventory,
            InventoryStateRegistry registry,
            IOperatorRoster        roster)
        {
            this.loadout   = loadout;
            this.inventory = inventory;
            this.registry  = registry;
            this.roster    = roster;
        }

        public void Initialize()
        {
            if (this.initialized) return;
            this.initialized = true;

            // Melee is permanently equipped and never stored in inventory slots, so it isn't
            // part of the saved-state below -- apply it unconditionally on every load.
            for (int slot = 0; slot < this.loadout.DefaultMelee.Length; slot++)
                this.roster[slot].SetMeleeWeapon(this.loadout.DefaultMelee[slot]);

            var saved = this.registry.Load<InventorySlot[]>();
            if (saved != null)
            {
                this.inventory.LoadState(saved);
                return;
            }

            foreach (var entry in this.loadout.Items)
                this.inventory.AddItem(entry.item, entry.operatorSlot, entry.quantity);

            for (int slot = 0; slot < this.loadout.DefaultWeapons.Length; slot++)
            {
                var weaponData = this.loadout.DefaultWeapons[slot];
                if (weaponData == null) continue;

                this.inventory.AddItem(weaponData, operatorSlot: slot);

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

        public void Dispose()
        {
            this.registry.Save(this.inventory.GetRawSlots());
        }
    }
}

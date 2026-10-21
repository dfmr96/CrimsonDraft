#nullable enable

using System;
using System.Linq;
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
        private readonly ItemDatabase           itemDatabase;
        private bool initialized;

        [Preserve]
        public InventoryBootstrap(
            StartingLoadout        loadout,
            IInventoryService      inventory,
            InventoryStateRegistry registry,
            IOperatorRoster        roster,
            ItemDatabase           itemDatabase)
        {
            this.loadout      = loadout;
            this.inventory    = inventory;
            this.registry     = registry;
            this.roster       = roster;
            this.itemDatabase = itemDatabase;
        }

        public void Initialize()
        {
            if (this.initialized) return;
            this.initialized = true;

            // Melee is permanently equipped and never stored in the inventory, so it isn't part
            // of the saved state below -- apply it unconditionally on every load.
            for (int slot = 0; slot < this.loadout.DefaultMelee.Length; slot++)
                this.roster[slot].SetMeleeWeapon(this.loadout.DefaultMelee[slot]);

            var saved = this.registry.Load();
            if (saved != null)
            {
                this.inventory.Restore(saved, this.itemDatabase);
                return;
            }

            foreach (var entry in this.loadout.Items)
                this.inventory.TryAdd(entry.item, ContainerId.Operator(entry.operatorSlot), entry.quantity);

            foreach (var entry in this.loadout.StorageItems)
                this.inventory.TryAdd(entry.item, ContainerId.Storage, entry.quantity);

            for (int slot = 0; slot < this.loadout.DefaultWeapons.Length; slot++)
            {
                var weaponData = this.loadout.DefaultWeapons[slot];
                if (weaponData == null || !this.inventory.TryAdd(weaponData, ContainerId.Operator(slot))) continue;

                var weapon = this.inventory.GetContainer(ContainerId.Operator(slot)).Placements
                    .Select(p => p.Item)
                    .OfType<WeaponItem>()
                    .First(w => w.Data == weaponData && !w.IsEquipped);
                this.inventory.Equip(weapon, slot);
                weapon.SetAmmo(weapon.MaxAmmo);
            }
        }

        public void Dispose()
        {
            this.inventory.CancelHeld();
            this.registry.Save(InventorySerializer.Capture(this.inventory));
        }
    }
}

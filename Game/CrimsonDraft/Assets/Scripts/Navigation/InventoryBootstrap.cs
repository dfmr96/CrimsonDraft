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

            // Melee is permanently equipped on every load. It also takes room in the operator's grid
            // (a permanent item: it can't be moved out of it), so the loadout puts it there below.
            for (int slot = 0; slot < this.loadout.DefaultMelee.Length; slot++)
                this.roster[slot].SetMeleeWeapon(this.loadout.DefaultMelee[slot]);

            var saved = this.registry.Load();
            if (saved != null)
            {
                this.inventory.Restore(saved, this.itemDatabase);
                EnsureMeleeInGrids(); // saves from before melee took room don't have it yet
                return;
            }

            // First, so the weapons claim the first free cells of each grid.
            EnsureMeleeInGrids();

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

        private void EnsureMeleeInGrids()
        {
            for (int slot = 0; slot < this.loadout.DefaultMelee.Length; slot++)
            {
                var melee = this.loadout.DefaultMelee[slot];
                if (melee == null) continue;

                var id = ContainerId.Operator(slot);
                if (this.inventory.GetContainer(id).Placements.Any(p => p.Item.Data == melee)) continue;

                if (!this.inventory.TryAdd(melee, id))
                    UnityEngine.Debug.LogError($"InventoryBootstrap: no room in operator {slot}'s grid for '{melee.ItemId}'.");
            }
        }

        public void Dispose()
        {
            this.inventory.CancelHeld();
            this.registry.Save(InventorySerializer.Capture(this.inventory));
        }
    }
}

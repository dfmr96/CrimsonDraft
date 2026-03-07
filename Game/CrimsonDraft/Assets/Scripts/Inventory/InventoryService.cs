#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    public sealed class InventoryService : IInventoryService
    {
        private readonly IOperatorRoster roster;
        private readonly List<InventoryItem> items = new();

        [Preserve]
        public InventoryService(IOperatorRoster roster) => this.roster = roster;

        public IReadOnlyList<InventoryItem> Items => this.items;

        public void AddItem(ItemData data) => this.items.Add(new InventoryItem(data));

        public void EquipWeapon(int itemIndex, int operatorSlot)
        {
            for (int i = 0; i < this.items.Count; i++)
            {
                if (this.items[i].EquippedBySlot == operatorSlot)
                    this.items[i].EquippedBySlot = -1;
            }
            this.items[itemIndex].EquippedBySlot = operatorSlot;
        }

        public void UnequipWeapon(int itemIndex) =>
            this.items[itemIndex].EquippedBySlot = -1;

        public int GetEquippedWeaponIndex(int operatorSlot)
        {
            for (int i = 0; i < this.items.Count; i++)
            {
                if (this.items[i].EquippedBySlot == operatorSlot)
                    return i;
            }
            return -1;
        }

        public bool CanReload(int ammoBoxIndex, int operatorSlot)
        {
            InventoryItem box = this.items[ammoBoxIndex];
            if (box.Data.ItemType != ItemType.AmmoBox)
                return false;

            int weaponIndex = GetEquippedWeaponIndex(operatorSlot);
            if (weaponIndex < 0)
                return false;

            if (this.items[weaponIndex].Data.Caliber != box.Data.Caliber)
                return false;

            var op = this.roster[operatorSlot];
            return op.IsAlive && op.Ammo < op.MaxAmmo;
        }

        public void ReloadOperator(int ammoBoxIndex, int operatorSlot)
        {
            if (!CanReload(ammoBoxIndex, operatorSlot))
                return;

            this.roster[operatorSlot].Reload();
            this.items.RemoveAt(ammoBoxIndex);
        }
    }
}

#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Inventory
{
    public interface IInventoryService
    {
        IReadOnlyList<InventoryItem> Items { get; }

        void AddItem(ItemData data);

        /// <summary>Equips weapon at itemIndex to operatorSlot. Unequips any weapon that slot was previously carrying.</summary>
        void EquipWeapon(int itemIndex, int operatorSlot);

        /// <summary>Unequips weapon at itemIndex. No-op if not equipped.</summary>
        void UnequipWeapon(int itemIndex);

        /// <summary>Returns the index of the weapon equipped by operatorSlot, or -1 if none.</summary>
        int GetEquippedWeaponIndex(int operatorSlot);

        /// <summary>Returns true if ammoBox at ammoBoxIndex can reload operatorSlot (caliber match + ammo &lt; max).</summary>
        bool CanReload(int ammoBoxIndex, int operatorSlot);

        /// <summary>Reloads operatorSlot using the ammo box at ammoBoxIndex. Consumes the box (removes from list).</summary>
        void ReloadOperator(int ammoBoxIndex, int operatorSlot);
    }
}

#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Inventory
{
    public interface IInventoryService
    {
        IReadOnlyList<InventoryItem> Items { get; }

        /// <summary>Creates the correct InventoryItem subtype based on ItemData type. quantity is used for AmmoBox.</summary>
        void AddItem(ItemData data, int quantity = 0);

        /// <summary>Equips weapon at itemIndex to operatorSlot. Unequips any weapon that slot was previously carrying.</summary>
        void EquipWeapon(int itemIndex, int operatorSlot);

        /// <summary>Unequips weapon at itemIndex. No-op if not equipped.</summary>
        void UnequipWeapon(int itemIndex);

        /// <summary>Returns the index of the weapon equipped by operatorSlot, or -1 if none.</summary>
        int GetEquippedWeaponIndex(int operatorSlot);

        /// <summary>Returns true if ammoBox at ammoBoxIndex can reload operatorSlot.</summary>
        bool CanReload(int ammoBoxIndex, int operatorSlot);

        /// <summary>Reloads weapon using ammo from box. Partially deducts box.Quantity. Removes box if exhausted.</summary>
        void ReloadOperator(int ammoBoxIndex, int operatorSlot);
    }
}

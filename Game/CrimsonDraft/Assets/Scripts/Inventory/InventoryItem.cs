#nullable enable

namespace CrimsonDraft.Inventory
{
    public class InventoryItem
    {
        public ItemData Data               { get; }
        public int      EquippedBySlot     { get; internal set; } = -1;
        public int      EquippedWeaponSlot { get; internal set; } = -1;
        public bool     IsEquipped         => this.EquippedBySlot >= 0;

        public InventoryItem(ItemData data) => this.Data = data;

        public void SetEquipped(int operatorSlot, int weaponSlot)
        {
            this.EquippedBySlot     = operatorSlot;
            this.EquippedWeaponSlot = weaponSlot;
        }

        public void ClearEquipped()
        {
            this.EquippedBySlot     = -1;
            this.EquippedWeaponSlot = -1;
        }
    }
}

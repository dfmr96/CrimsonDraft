#nullable enable

using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    public sealed class WeaponItem : InventoryItem, IWeaponSlot, IHasDisplayCount
    {
        public new WeaponData Data    => (WeaponData)base.Data;
        public Caliber Caliber       => this.Data.Caliber;
        public int    MaxAmmo        => this.Data.MagazineCapacity;
        public int    CurrentAmmo    { get; private set; }

        public WeaponItem(WeaponData data) : base(data)
        {
            this.CurrentAmmo = 0;
        }

        public int DisplayCount => this.CurrentAmmo;

        public void SetAmmo(int value) =>
            this.CurrentAmmo = value < 0 ? 0 : value > this.MaxAmmo ? this.MaxAmmo : value;
    }
}

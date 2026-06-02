#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "CrimsonDraft/Inventory/Weapon Data")]
    public sealed class WeaponData : ItemData
    {
        [SerializeField] private string            caliber                = string.Empty;
        [SerializeField] private int               magazineCapacity       = 1;
        [SerializeField] private int               dispersionRadius       = 10;
        [SerializeField] private Sprite?           dispersionCircleSprite;
        [SerializeField] private BurstPatternData? burstPattern;
        [SerializeField] private WeaponSlot        weaponSlot             = WeaponSlot.Primary;

        public string            Caliber                => this.caliber;
        public int               MagazineCapacity       => this.magazineCapacity;
        public int               DispersionRadius       => this.dispersionRadius;
        public Sprite?           DispersionCircleSprite => this.dispersionCircleSprite;
        public BurstPatternData? BurstPattern           => this.burstPattern;
        public WeaponSlot        WeaponSlot             => this.weaponSlot;
    }
}

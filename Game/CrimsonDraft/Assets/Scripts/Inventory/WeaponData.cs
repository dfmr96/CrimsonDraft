#nullable enable

using NaughtyAttributes;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "CrimsonDraft/Inventory/Weapon Data")]
    public sealed class WeaponData : ItemData
    {
        [SerializeField] private Caliber           caliber                = Caliber.None;
        [SerializeField] private GunType           gunType                = GunType.Pistols;
        [SerializeField] private int               magazineCapacity       = 1;
        [SerializeField, Min(1)] private int       maxShotCount           = 6;
        [SerializeField] private int               dispersionRadius       = 10;
        [SerializeField] private Sprite?           dispersionCircleSprite;
        [SerializeField] private BurstPatternData? burstPattern;
        [SerializeField] private WeaponSlot        weaponSlot             = WeaponSlot.Primary;
        [SerializeField, Min(1)] private int       damage                 = 20;
        [SerializeField, Min(0)] private int       poiseDamage            = 10;

        // Pellets rolled per bullet within its burst-pattern ellipse - only meaningful for
        // shotgun-type weapons (see PelletSpreadStrategy); every other GunType always fires
        // exactly one pellet per bullet regardless of this value.
        [SerializeField, Min(1), ShowIf(nameof(IsShotgunGunType))] private int pelletCount = 8;

        public Caliber           Caliber                => this.caliber;
        public GunType           GunType                => this.gunType;
        public int               MagazineCapacity       => this.magazineCapacity;
        public int               MaxShotCount           => Mathf.Max(1, this.maxShotCount);
        public int               DispersionRadius       => this.dispersionRadius;
        public Sprite?           DispersionCircleSprite => this.dispersionCircleSprite;
        public BurstPatternData? BurstPattern           => this.burstPattern;
        public WeaponSlot        WeaponSlot             => this.weaponSlot;
        public int               Damage                 => this.damage;
        public int               PoiseDamage            => this.poiseDamage;
        public int               PelletCount            => this.pelletCount;

        private bool IsShotgunGunType() => this.gunType is GunType.Shotgun or GunType.REShotgun;
    }
}

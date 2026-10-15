#nullable enable

using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    // Permanently-equipped melee weapon data -- never enters the spatial inventory pipeline
    // (no ammo/magazine, no uses), so it's just an identity + icon plus its combat stats.
    [CreateAssetMenu(fileName = "MeleeWeaponData", menuName = "CrimsonDraft/Inventory/Melee Weapon Data")]
    public sealed class MeleeWeaponData : ItemData, IMeleeWeapon
    {
        [SerializeField, Min(1)] private int damage      = 20;
        [SerializeField, Min(0)] private int poiseDamage = 10;

        // Fraction of damage that gets through a vest -- same meaning as WeaponData's.
        [SerializeField, Range(0f, 1f)] private float armorDamageMultiplier = 0.25f;

        // The slash is ONE cut: a dense row of marker-sized points that starts at the confirmed aim
        // point and runs slashLength px at slashAngleDegrees (0 = straight right), varied by up to
        // +/- slashAngleJitterDegrees so it can go level, a little up or a little down -- see
        // SlashStrategy. It deals a fixed 1 damage in total (AimViewController.SlashFixedDamage), so
        // Damage above no longer scales the slash; PoiseDamage counts once for the whole swing.
        // Any point of the cut that lands off the body leaves the operator open to a counter-hit.
        [SerializeField, Min(2)] private int   slashPointCount         = 5;     // legacy: point count now follows slashLength
        [SerializeField]         private float slashLength             = 40f;
        [SerializeField]         private float slashAngleDegrees       = 0f;
        [SerializeField]         private float slashAngleJitterDegrees = 20f;

        // Legacy hand-authored point placement (Burst Pattern Editor). No longer used by the slash,
        // which is generated from the length/angle above; kept so existing assets don't lose data.
        [SerializeField] private BurstPatternData? slashPattern;

        public int               Damage                  => this.damage;
        public int                PoiseDamage             => this.poiseDamage;
        public float              ArmorDamageMultiplier   => this.armorDamageMultiplier;
        public int                SlashPointCount         => this.slashPointCount;
        public float              SlashLength             => this.slashLength;
        public float              SlashAngleDegrees       => this.slashAngleDegrees;
        public float              SlashAngleJitterDegrees => this.slashAngleJitterDegrees;
        public BurstPatternData?  SlashPattern            => this.slashPattern;
    }
}

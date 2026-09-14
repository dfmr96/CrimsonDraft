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

        // The slash is rendered as several pellet-like points (same marker sprite/pipeline as a
        // shotgun blast) laid out along a line through the confirmed aim point instead of
        // scattered inside a dispersion ellipse -- see SlashStrategy. Damage/PoiseDamage above
        // are per-point, summed across all points exactly like a shotgun's pellets.
        [SerializeField, Min(2)] private int   slashPointCount         = 5;
        [SerializeField]         private float slashLength             = 40f;
        [SerializeField]         private float slashAngleDegrees       = 45f;
        [SerializeField]         private float slashAngleJitterDegrees = 15f;

        // Optional hand-authored point placement (Tools > CrimsonDraft > Burst Pattern Editor --
        // same asset type/tool guns use for their pellet spread). When assigned, point i of the
        // slash samples entry i of the pattern (clamped) instead of the procedural length/angle
        // line above, so each point can be placed exactly where it should land on the silhouette.
        [SerializeField] private BurstPatternData? slashPattern;

        public int               Damage                  => this.damage;
        public int                PoiseDamage             => this.poiseDamage;
        public int                SlashPointCount         => this.slashPointCount;
        public float              SlashLength             => this.slashLength;
        public float              SlashAngleDegrees       => this.slashAngleDegrees;
        public float              SlashAngleJitterDegrees => this.slashAngleJitterDegrees;
        public BurstPatternData?  SlashPattern            => this.slashPattern;
    }
}

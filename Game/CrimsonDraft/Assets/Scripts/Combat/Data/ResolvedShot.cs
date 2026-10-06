#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public readonly struct ResolvedShot
    {
        public int           Index         { get; }
        public int           BulletIndex   { get; }
        public Vector2       NormalizedPos { get; }
        public ShotZone      Zone          { get; }
        public ShotPrecision Precision     { get; }
        public int           Damage        { get; }
        // True when the pellet landed on armor the weapon doesn't fully penetrate -- Damage is
        // already reduced; this only drives feedback (blocked-hit popup color).
        public bool          ArmorBlocked  { get; }

        public ResolvedShot(int index, int bulletIndex, Vector2 normalizedPos, ShotZone zone, ShotPrecision precision, int damage, bool armorBlocked = false)
        {
            this.Index         = index;
            this.BulletIndex   = bulletIndex;
            this.NormalizedPos = normalizedPos;
            this.Zone          = zone;
            this.Precision     = precision;
            this.Damage        = damage;
            this.ArmorBlocked  = armorBlocked;
        }
    }
}

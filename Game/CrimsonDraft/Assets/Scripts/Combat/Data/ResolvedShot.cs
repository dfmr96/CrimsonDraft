#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public readonly struct ResolvedShot
    {
        public int           Index         { get; }
        public Vector2       NormalizedPos { get; }
        public ShotZone      Zone          { get; }
        public ShotPrecision Precision     { get; }
        public int           Damage        { get; }

        public ResolvedShot(int index, Vector2 normalizedPos, ShotZone zone, ShotPrecision precision, int damage)
        {
            this.Index         = index;
            this.NormalizedPos = normalizedPos;
            this.Zone          = zone;
            this.Precision     = precision;
            this.Damage        = damage;
        }
    }
}

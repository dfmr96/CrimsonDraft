#nullable enable

using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Melee's counterpart of PelletSpreadStrategy, but it lays out ONE continuous cut instead of
    // separate hits: a dense row of points along a (mostly horizontal) line of lengthPx that begins
    // a little BEFORE the confirmed aim point (StartBehindFraction of its length) and runs past it. The points sit PointSpacingPx apart -- tighter
    // than the marker sprite -- so they render as a single trail, and AimViewController treats the
    // whole row as one hit (fixed damage, one popup, one poise/decapitation tick).
    //
    // The direction is angleDeg +/- a random angleJitterDeg (0 deg = straight to the right), so a
    // swing can go level, slightly up or slightly down. Any hand-authored burstPattern is ignored:
    // the cut is always generated from the weapon's length/angle.
    // firstShotLocal/bulletIndex are unused -- melee is always a single swing.
    public sealed class SlashStrategy : IShotResolutionStrategy
    {
        // Much closer than the 3 px marker (positions snap to whole pixels, so 1 is the tightest
        // possible): neighbouring markers overlap heavily and read as one solid stroke.
        internal const float PointSpacingPx = 1f;

        // How much of the cut lies BEFORE the confirmed aim point (0 = starts exactly there,
        // 0.5 = the aim point is the middle of the stroke).
        internal const float StartBehindFraction = 0.35f;

        private readonly float lengthPx;
        private readonly float angleDeg;
        private readonly float angleJitterDeg;

        public SlashStrategy(float lengthPx, float angleDeg, float angleJitterDeg)
        {
            this.lengthPx       = Mathf.Max(0f, lengthPx);
            this.angleDeg       = angleDeg;
            this.angleJitterDeg = Mathf.Max(0f, angleJitterDeg);
        }

        public Vector2[] GetPelletLocalPositions(
            Vector2 confirmedLocalPos,
            Vector2 firstShotLocal,
            int bulletIndex,
            float perBulletYOffset,
            BurstPatternData? burstPattern,
            int pelletCount)
        {
            float angle = (this.angleDeg + Random.Range(-this.angleJitterDeg, this.angleJitterDeg)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            int count = Mathf.Max(2, Mathf.CeilToInt(this.lengthPx / PointSpacingPx) + 1);
            var result = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                // The cut doesn't begin AT the aim point: it starts StartBehindFraction of its length
                // before it and carries on past it, so the aim point lands part-way along the stroke.
                float t = i / (float)(count - 1) - StartBehindFraction; // -StartBehindFraction .. 1 - StartBehindFraction
                Vector2 p = confirmedLocalPos + dir * (t * this.lengthPx);
                result[i] = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
            }
            return result;
        }
    }
}

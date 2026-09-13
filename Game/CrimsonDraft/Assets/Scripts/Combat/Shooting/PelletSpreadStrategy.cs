#nullable enable

using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Shotgun-style: one bullet still maps to one burst-pattern ellipse (same indexing as
    // SingleShotStrategy), but that ellipse now yields `pelletCount` independently sampled
    // pellets instead of one - so a single shell can hit multiple zones with independent
    // damage rolls, all scattered inside the same dispersion shape. Falls back to stacking
    // every pellet on the single-shot fallback point when no pattern is assigned, so a
    // misconfigured shotgun still fires (just without spread).
    public sealed class PelletSpreadStrategy : IShotResolutionStrategy
    {
        public Vector2[] GetPelletLocalPositions(
            Vector2 confirmedLocalPos,
            Vector2 firstShotLocal,
            int bulletIndex,
            float perBulletYOffset,
            BurstPatternData? burstPattern,
            int pelletCount)
        {
            int clampedPelletCount = Mathf.Max(1, pelletCount);
            var shots = burstPattern?.Shots;

            if (shots == null || shots.Length == 0)
            {
                var fallback = AimViewController.ComputeBulletLocalFromPrimary(firstShotLocal, bulletIndex, perBulletYOffset);
                var fallbackResult = new Vector2[clampedPelletCount];
                for (int p = 0; p < clampedPelletCount; p++)
                    fallbackResult[p] = fallback;
                return fallbackResult;
            }

            var entry  = shots[Mathf.Min(bulletIndex, shots.Length - 1)];
            var result = new Vector2[clampedPelletCount];
            for (int p = 0; p < clampedPelletCount; p++)
            {
                var offset = BurstPatternData.SamplePoint(in entry);
                result[p] = new Vector2(
                    Mathf.Round(confirmedLocalPos.x + offset.x),
                    Mathf.Round(confirmedLocalPos.y + offset.y));
            }

            return result;
        }
    }
}

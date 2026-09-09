#nullable enable

using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    // One bullet, one pellet - today's default behavior for every non-shotgun weapon.
    // Uses the burst pattern entry indexed by bulletIndex (per-bullet dispersion shape),
    // falling back to a straight vertical stack when no pattern is assigned.
    public sealed class SingleShotStrategy : IShotResolutionStrategy
    {
        public Vector2[] GetPelletLocalPositions(
            Vector2 confirmedLocalPos,
            Vector2 firstShotLocal,
            int bulletIndex,
            float perBulletYOffset,
            BurstPatternData? burstPattern,
            int pelletCount)
        {
            var shots = burstPattern?.Shots;
            Vector2 pos;
            if (shots != null && shots.Length > 0)
            {
                var entry  = shots[Mathf.Min(bulletIndex, shots.Length - 1)];
                var offset = BurstPatternData.SamplePoint(in entry);
                pos = new Vector2(
                    Mathf.Round(confirmedLocalPos.x + offset.x),
                    Mathf.Round(confirmedLocalPos.y + offset.y));
            }
            else
            {
                pos = AimViewController.ComputeBulletLocalFromPrimary(firstShotLocal, bulletIndex, perBulletYOffset);
            }

            return new[] { pos };
        }
    }
}

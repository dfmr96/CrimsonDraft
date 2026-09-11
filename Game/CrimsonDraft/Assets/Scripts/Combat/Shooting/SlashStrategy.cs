#nullable enable

using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Melee's equivalent of PelletSpreadStrategy: pelletCount points, all zone-sampled and
    // damage-summed the same way a shotgun's pellets are. Placement has two modes:
    //  - Hand-authored (burstPattern assigned via MeleeWeaponData.SlashPattern, made with the
    //    same Burst Pattern Editor tool guns use): point i samples entry i of the pattern
    //    (clamped), so each point of the cut can be placed exactly where it should land.
    //  - Procedural fallback (no pattern assigned): pelletCount points laid out evenly along a
    //    straight line (with a little perpendicular jitter so it doesn't read as a perfectly
    //    straight ruler) through the confirmed aim point, so the result still renders as a
    //    cut/slash rather than a spread.
    // firstShotLocal/bulletIndex are unused either way -- melee is always a single swing.
    public sealed class SlashStrategy : IShotResolutionStrategy
    {
        private readonly float lengthPx;
        private readonly float angleDeg;
        private readonly float angleJitterDeg;
        private readonly float perpJitterPx;

        public SlashStrategy(float lengthPx, float angleDeg, float angleJitterDeg, float perpJitterPx = 4f)
        {
            this.lengthPx       = lengthPx;
            this.angleDeg       = angleDeg;
            this.angleJitterDeg = angleJitterDeg;
            this.perpJitterPx   = perpJitterPx;
        }

        public Vector2[] GetPelletLocalPositions(
            Vector2 confirmedLocalPos,
            Vector2 firstShotLocal,
            int bulletIndex,
            float perBulletYOffset,
            BurstPatternData? burstPattern,
            int pelletCount)
        {
            int count = Mathf.Max(2, pelletCount);

            var patternShots = burstPattern?.Shots;
            if (patternShots != null && patternShots.Length > 0)
            {
                var patterned = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    var entry  = patternShots[Mathf.Min(i, patternShots.Length - 1)];
                    var offset = BurstPatternData.SamplePoint(in entry);
                    patterned[i] = new Vector2(
                        Mathf.Round(confirmedLocalPos.x + offset.x),
                        Mathf.Round(confirmedLocalPos.y + offset.y));
                }
                return patterned;
            }

            float angle = (this.angleDeg + Random.Range(-this.angleJitterDeg, this.angleJitterDeg)) * Mathf.Deg2Rad;
            Vector2 dir  = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 perp = new Vector2(-dir.y, dir.x);

            var result = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float t = (i / (float)(count - 1)) - 0.5f; // -0.5 .. 0.5 along the line
                Vector2 alongLine = confirmedLocalPos + dir * (t * this.lengthPx);
                float   jitter    = Random.Range(-this.perpJitterPx, this.perpJitterPx);
                result[i] = new Vector2(
                    Mathf.Round(alongLine.x + perp.x * jitter),
                    Mathf.Round(alongLine.y + perp.y * jitter));
            }
            return result;
        }
    }
}

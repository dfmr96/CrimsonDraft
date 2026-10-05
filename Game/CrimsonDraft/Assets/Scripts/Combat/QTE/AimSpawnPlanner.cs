#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Pure logic for picking where a QTE selector's first leg starts along its rail. No
    // MonoBehaviour, no DOTween -- just the offset math, so it's cheap to unit test.
    public static class AimSpawnPlanner
    {
        // halfExtent: half the length of the rail (selector oscillates between -halfExtent and +halfExtent).
        // danger01: 0 at full HP, 1 at 0 HP -- drives both the max offset and the edge-spawn chance.
        // previousSignedOffset: the offset returned by the previous call for this same rail, or null on the first shot.
        // Returns a signed offset in [-halfExtent, halfExtent].
        public static float ComputeSignedOffset(
            float halfExtent,
            float danger01,
            float? previousSignedOffset,
            AimSpawnConfig config,
            IRandomSource random)
        {
            if (halfExtent <= 0f || config == null || random == null)
                return 0f;

            float danger        = Mathf.Clamp01(danger01);
            float maxRatio       = Mathf.Lerp(config.BaseMaxOffsetRatio, config.LowHpMaxOffsetRatio, danger);
            float minRatio       = Mathf.Min(config.MinOffsetRatio, maxRatio);
            float edgeChance     = Mathf.Lerp(config.BaseEdgeSpawnChance, config.LowHpEdgeSpawnChance, danger);
            float edgeFloorRatio = Mathf.Clamp(maxRatio * config.EdgeZoneRatio, minRatio, maxRatio);

            float minSeparation = config.MinSeparationRatio * halfExtent;
            int   maxAttempts   = Mathf.Max(1, config.MaxRerollAttempts);

            float candidate = 0f;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                bool  forceEdge      = random.NextFloat01() < edgeChance;
                float lowRatio       = forceEdge ? edgeFloorRatio : minRatio;
                float magnitudeRatio = Mathf.Lerp(lowRatio, maxRatio, random.NextFloat01());
                float sign           = random.NextInt(0, 2) == 0 ? -1f : 1f;
                candidate = sign * magnitudeRatio * halfExtent;

                bool tooClose = previousSignedOffset.HasValue
                    && Mathf.Abs(candidate - previousSignedOffset.Value) < minSeparation;
                if (!tooClose)
                    return candidate;
            }

            // Every reroll landed too close to the previous spawn -- deterministically mirror it
            // to the opposite side rather than looping forever or silently keeping a repeat.
            return previousSignedOffset.HasValue ? -previousSignedOffset.Value : candidate;
        }
    }
}

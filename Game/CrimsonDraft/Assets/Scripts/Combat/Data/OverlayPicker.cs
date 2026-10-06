#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Combat
{
    public static class OverlayPicker
    {
        // Exactly one overlay per enemy (or none for an empty pool) -- picking a single entry is
        // what guarantees an enemy never wears a vest and blisters at the same time. Blank pool
        // slots are skipped rather than rolled as "no overlay".
        public static SilhouetteOverlay? Pick(IReadOnlyList<SilhouetteOverlay?>? pool, IRandomSource random)
        {
            if (pool == null) return null;

            var candidates = new List<SilhouetteOverlay>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                SilhouetteOverlay? overlay = pool[i];
                if (overlay != null) candidates.Add(overlay);
            }

            return candidates.Count == 0 ? null : candidates[random.NextInt(0, candidates.Count)];
        }
    }
}

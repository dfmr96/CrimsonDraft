#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Edge magnet for the Map Editor: nudges a room so its edges meet the nearest
    /// edge of another room (flush side-to-side, or lined up on the same side) when within
    /// tolerance. Each axis is solved independently and never moves further than tolerance.</summary>
    public static class MapAlign
    {
        public const int DefaultTolerance = 4;

        public static Vector2Int SnapOffset(RectInt moving, IEnumerable<RectInt> others, int tolerance)
        {
            int? bestX = null, bestY = null;

            foreach (var other in others)
            {
                bestX = Nearest(bestX, tolerance, other.xMin - moving.xMin, other.xMax - moving.xMax, other.xMax - moving.xMin, other.xMin - moving.xMax);
                bestY = Nearest(bestY, tolerance, other.yMin - moving.yMin, other.yMax - moving.yMax, other.yMax - moving.yMin, other.yMin - moving.yMax);
            }

            return new Vector2Int(bestX ?? 0, bestY ?? 0);
        }

        private static int? Nearest(int? best, int tolerance, params int[] candidates)
        {
            foreach (int delta in candidates)
            {
                if (Mathf.Abs(delta) > tolerance)
                    continue;
                if (best == null || Mathf.Abs(delta) < Mathf.Abs(best.Value))
                    best = delta;
            }
            return best;
        }
    }
}

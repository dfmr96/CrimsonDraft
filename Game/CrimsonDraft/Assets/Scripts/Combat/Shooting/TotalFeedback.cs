#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public static class TotalFeedback
    {
        public const string MissText = "MISS";

        // Text for the end-of-QTE total popup, or null when there's nothing worth totaling
        // (0 or 1 resolved shots -- the single per-shot popup already shows that number).
        // "MISS" only when no pellet touched the body at all; a body hit for 0 still totals.
        public static string? Format(IReadOnlyList<ResolvedShot> shots)
        {
            if (shots == null || shots.Count < 2) return null;

            int  total  = 0;
            bool anyHit = false;
            for (int i = 0; i < shots.Count; i++)
            {
                if (shots[i].Zone != ShotZone.Miss) anyHit = true;
                total += Mathf.Max(0, shots[i].Damage);
            }

            return anyHit ? $"-{total}" : MissText;
        }
    }
}

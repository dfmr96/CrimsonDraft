#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Pairs old spawns with the doors that replace them. Globally nearest-first: the closest
    /// compatible (door, spawn) pair is taken, then the next closest among what is left, so a spawn
    /// goes to the door next to it even when another door comes first in the hierarchy.</summary>
    public static class SpawnMatching
    {
        /// <returns>door index → spawn index, for the doors that got one.</returns>
        public static Dictionary<int, int> MatchNearest(IReadOnlyList<Vector3> doors, IReadOnlyList<Vector3> spawns, Func<int, int, bool> compatible)
        {
            var pairs = new List<(float distance, int door, int spawn)>();
            for (int d = 0; d < doors.Count; d++)
                for (int s = 0; s < spawns.Count; s++)
                    if (compatible(d, s)) pairs.Add(((doors[d] - spawns[s]).sqrMagnitude, d, s));
            pairs.Sort((a, b) => a.distance.CompareTo(b.distance));

            var result = new Dictionary<int, int>();
            var usedSpawns = new HashSet<int>();
            foreach (var (_, door, spawn) in pairs)
            {
                if (result.ContainsKey(door) || usedSpawns.Contains(spawn)) continue;
                result[door] = spawn;
                usedSpawns.Add(spawn);
            }
            return result;
        }
    }
}

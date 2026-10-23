#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Editor;

namespace CrimsonDraft.Tests
{
    public sealed class SpawnMatchingTests
    {
        [Test]
        public void MatchNearest_spawnGoesToItsNearestDoorNotTheFirstDoor()
        {
            var doors = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
            var spawns = new[] { new Vector3(9, 0, 0) };

            var match = SpawnMatching.MatchNearest(doors, spawns, (d, s) => true);

            Assert.AreEqual(1, match.Count);
            Assert.AreEqual(0, match[1]);
            Assert.IsFalse(match.ContainsKey(0));
        }

        [Test]
        public void MatchNearest_ignoresIncompatiblePairs()
        {
            var doors = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
            var spawns = new[] { new Vector3(1, 0, 0) };

            var match = SpawnMatching.MatchNearest(doors, spawns, (d, s) => d == 1);

            Assert.AreEqual(0, match[1]);
            Assert.IsFalse(match.ContainsKey(0));
        }

        [Test]
        public void MatchNearest_eachSpawnUsedOnce()
        {
            var doors = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0) };
            var spawns = new[] { new Vector3(0.4f, 0, 0), new Vector3(0.6f, 0, 0) };

            var match = SpawnMatching.MatchNearest(doors, spawns, (d, s) => true);

            Assert.AreEqual(0, match[0]);
            Assert.AreEqual(1, match[1]);
        }
    }
}

#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Pushables;

namespace CrimsonDraft.Tests
{
    public sealed class PushableObjectTests
    {
        [Test]
        public void ResolveAxis_playerDueSouthOfBox_pushesNorth()
        {
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(0f, 0f, 5f), playerPosition: new Vector3(0f, 0f, 0f));

            Assert.AreEqual(new Vector3(0f, 0f, 1f), axis);
        }

        [Test]
        public void ResolveAxis_playerDueNorthOfBox_pushesSouth()
        {
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(0f, 0f, -5f), playerPosition: new Vector3(0f, 0f, 0f));

            Assert.AreEqual(new Vector3(0f, 0f, -1f), axis);
        }

        [Test]
        public void ResolveAxis_playerDueWestOfBox_pushesEast()
        {
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(5f, 0f, 0f), playerPosition: new Vector3(0f, 0f, 0f));

            Assert.AreEqual(new Vector3(1f, 0f, 0f), axis);
        }

        [Test]
        public void ResolveAxis_playerDueEastOfBox_pushesWest()
        {
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(-5f, 0f, 0f), playerPosition: new Vector3(0f, 0f, 0f));

            Assert.AreEqual(new Vector3(-1f, 0f, 0f), axis);
        }

        [Test]
        public void ResolveAxis_cornerContact_xDeltaSlightlyLarger_choosesXAxis_notDiagonal()
        {
            // Player stands just off-center near a corner: |dx|=3 vs |dz|=2.9 -- X wins,
            // and the result must be a pure axis vector, never a diagonal blend of both.
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(3f, 0f, 2.9f), playerPosition: Vector3.zero);

            Assert.AreEqual(new Vector3(1f, 0f, 0f), axis);
        }

        [Test]
        public void ResolveAxis_cornerContact_zDeltaSlightlyLarger_choosesZAxis()
        {
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(2.9f, 0f, 3f), playerPosition: Vector3.zero);

            Assert.AreEqual(new Vector3(0f, 0f, 1f), axis);
        }

        [Test]
        public void ResolveAxis_exactlyEqualDeltas_choosesXAxis_tieBreakIsDeterministic()
        {
            // Documents the tie-break: ResolveAxis uses >=, so an exact tie always picks X.
            var axis = PushableObject.ResolveAxis(boxPosition: new Vector3(3f, 0f, 3f), playerPosition: Vector3.zero);

            Assert.AreEqual(new Vector3(1f, 0f, 0f), axis);
        }
    }
}

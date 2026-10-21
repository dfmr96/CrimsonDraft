#nullable enable

using System.Linq;
using NUnit.Framework;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapDoorPairingTests
    {
        private static MapDoorEnd End(string doorId, string room, string? destination)
            => new(doorId, room, destination);

        [Test]
        public void ExcludedHalf_pairsWithTheDrawnHalfLeadingBack()
        {
            var result = MapDoorPairing.Pair(
                drawn:    new[] { End("DR_32B", "DINNER", "KITCHEN"), End("DR_72B", "DINNER", "HALL") },
                excluded: new[] { End("DR_187", "KITCHEN", "DINNER") });

            var sides = result.OtherSidesOf("DR_32B");
            Assert.AreEqual(1, sides.Count);
            Assert.AreEqual("DR_187", sides[0].DoorId);
            Assert.AreEqual("KITCHEN", sides[0].RoomId);
            Assert.IsEmpty(result.OtherSidesOf("DR_72B"));
            Assert.IsEmpty(result.Unpaired);
            Assert.IsEmpty(result.Ambiguous);
        }

        [Test]
        public void ExcludedHalf_withNoDrawnHalfLeadingBack_isUnpaired()
        {
            var result = MapDoorPairing.Pair(
                drawn:    new[] { End("DR_32B", "DINNER", "HALL") },
                excluded: new[] { End("DR_187", "KITCHEN", "DINNER") });

            Assert.IsEmpty(result.OtherSidesOf("DR_32B"));
            CollectionAssert.AreEqual(new[] { "DR_187" }, result.Unpaired.Select(e => e.DoorId));
        }

        [Test]
        public void ExcludedHalf_withoutDestination_isUnpaired()
        {
            var result = MapDoorPairing.Pair(
                drawn:    new[] { End("DR_32B", "DINNER", "KITCHEN") },
                excluded: new[] { End("DR_187", "KITCHEN", null) });

            Assert.IsEmpty(result.OtherSidesOf("DR_32B"));
            CollectionAssert.AreEqual(new[] { "DR_187" }, result.Unpaired.Select(e => e.DoorId));
        }

        [Test]
        public void TwoDrawnHalvesLeadingBack_isAmbiguous_andPairsNeither()
        {
            var result = MapDoorPairing.Pair(
                drawn:    new[] { End("DR_1", "DINNER", "KITCHEN"), End("DR_2", "DINNER", "KITCHEN") },
                excluded: new[] { End("DR_187", "KITCHEN", "DINNER") });

            Assert.IsEmpty(result.OtherSidesOf("DR_1"));
            Assert.IsEmpty(result.OtherSidesOf("DR_2"));
            CollectionAssert.AreEqual(new[] { "DR_187" }, result.Ambiguous.Select(e => e.DoorId));
        }
    }
}

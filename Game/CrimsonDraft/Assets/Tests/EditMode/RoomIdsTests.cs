#nullable enable

using NUnit.Framework;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomIdsTests
    {
        [TestCase("Deck_B", "HALLWAY B1",         "Deck_B/HALLWAY_B1")]
        [TestCase("Deck_B", "INTERROGATION ROOM", "Deck_B/INTERROGATION_ROOM")]
        [TestCase("Deck_C", "SAVEROOM_C",         "Deck_C/SAVE_ROOM")]
        [TestCase("Deck_B", "CHEFF_ROOM",         "Deck_B/CHEFF_ROOM")]
        [TestCase("Deck_B", "Deck_B/KITCHEN",     "Deck_B/KITCHEN")]
        public void Normalize_prefixesDeck_andCleansRoomPart(string deck, string legacy, string expected)
            => Assert.AreEqual(expected, RoomIds.Normalize(deck, legacy));

        [TestCase("Deck_B/KITCHEN",    true)]
        [TestCase("Deck_C/SAVE_ROOM",  true)]
        [TestCase("KITCHEN",           false)]
        [TestCase("Deck_B/HALLWAY B1", false)]
        [TestCase("Deck_B/kitchen",    false)]
        [TestCase("",                  false)]
        public void IsWellFormed(string id, bool expected)
            => Assert.AreEqual(expected, RoomIds.IsWellFormed(id));

        [Test]
        public void DeckOf_returnsPrefix()
        {
            Assert.AreEqual("Deck_C", RoomIds.DeckOf("Deck_C/OFFICE"));
            Assert.IsNull(RoomIds.DeckOf("OFFICE"));
        }

        [Test]
        public void DeckOfScene_dropsDevelopmentSuffix()
            => Assert.AreEqual("Deck_C", RoomIds.DeckOfScene("Deck_C_Development"));

        [Test]
        public void FindCollisions_reportsTwoLegacyIdsNormalizingToOne()
        {
            var collisions = RoomIds.FindCollisions(new[]
            {
                ("Deck_B", "HALLWAY B1"),
                ("Deck_B", "HALLWAY_B1"),
                ("Deck_C", "HALLWAY_B1"),
            });

            CollectionAssert.AreEqual(new[] { "Deck_B/HALLWAY_B1" }, collisions);
        }
    }
}

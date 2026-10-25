#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomCatalogTests
    {
        private RoomCatalog catalog = null!;

        [SetUp]
        public void SetUp() => this.catalog = ScriptableObject.CreateInstance<RoomCatalog>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.catalog);

        private static RoomCatalog.Entry E(string id, string scene) => new RoomCatalog.Entry(id, scene);

        [Test]
        public void EditorSetRooms_groupsByDeckInFirstAppearance_sortsIds_dropsDuplicates()
        {
            this.catalog.EditorSetRooms(new[]
            {
                E("Deck_C/B", "Deck_C_Development"),
                E("Deck_B/Z", "Deck_B_Development"),
                E("Deck_B/A", "Deck_B_Development"),
                E("Deck_B/A", "Deck_B_Development"),
            });

            CollectionAssert.AreEqual(new[] { "Deck_C/B", "Deck_B/A", "Deck_B/Z" },
                this.catalog.Rooms.Select(r => r.RoomId).ToArray());
        }

        [Test]
        public void TryGetScene_knownAndUnknown()
        {
            this.catalog.EditorSetRooms(new[] { E("Deck_B/A", "Deck_B_Development") });

            Assert.IsTrue(this.catalog.TryGetScene("Deck_B/A", out var scene));
            Assert.AreEqual("Deck_B_Development", scene);
            Assert.IsFalse(this.catalog.TryGetScene("Deck_B/NOPE", out _));
            Assert.IsTrue(this.catalog.Contains("Deck_B/A"));
            Assert.IsFalse(this.catalog.Contains("Deck_B/NOPE"));
        }
    }
}

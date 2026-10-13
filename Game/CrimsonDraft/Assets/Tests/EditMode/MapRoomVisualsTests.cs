#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapRoomVisualsTests
    {
        private Sprite incomplete = null!;
        private Sprite complete   = null!;
        private RoomStateRegistry rooms     = null!;
        private PickupRegistry    pickups   = null!;
        private KnownMapsRegistry knownMaps = null!;

        [SetUp]
        public void SetUp()
        {
            this.incomplete = MapTestData.Sprite();
            this.complete   = MapTestData.Sprite();
            this.rooms      = new RoomStateRegistry();
            this.pickups    = new PickupRegistry();
            this.knownMaps  = new KnownMapsRegistry();
        }

        private MapRoomData Room(string id, params string[] pickupIds)
            => MapTestData.Room(id, pickups: pickupIds, incomplete: this.incomplete, complete: this.complete);

        private System.Collections.Generic.IReadOnlyList<MapRoomVisual> Resolve(MapData map, string? current = null)
            => MapRoomVisuals.Resolve(map, this.rooms, this.pickups, this.knownMaps, current);

        [Test]
        public void Unvisited_withoutMapItem_isHidden()
        {
            Assert.IsEmpty(Resolve(MapTestData.Map("deck-a", Room("a"))));
        }

        [Test]
        public void Visited_isDrawn()
        {
            this.rooms.MarkVisited("a");
            Assert.AreEqual(1, Resolve(MapTestData.Map("deck-a", Room("a"))).Count);
        }

        [Test]
        public void Unvisited_withMapItem_isDrawn()
        {
            this.knownMaps.MarkKnown("deck-a");
            Assert.AreEqual(1, Resolve(MapTestData.Map("deck-a", Room("a"))).Count);
        }

        [Test]
        public void PendingPickup_usesIncompleteSprite()
        {
            this.rooms.MarkVisited("a");
            Assert.AreSame(this.incomplete, Resolve(MapTestData.Map("deck-a", Room("a", "p1")))[0].Sprite);
        }

        [Test]
        public void AllPickupsCollected_usesCompleteSprite()
        {
            this.rooms.MarkVisited("a");
            this.pickups.SetCollected("p1");
            Assert.AreSame(this.complete, Resolve(MapTestData.Map("deck-a", Room("a", "p1")))[0].Sprite);
        }

        [Test]
        public void NoPickups_unvisitedWithMapItem_usesCompleteSprite()
        {
            this.knownMaps.MarkKnown("deck-a");
            Assert.AreSame(this.complete, Resolve(MapTestData.Map("deck-a", Room("a")))[0].Sprite);
        }

        [Test]
        public void MissingResolvedSprite_isNotDrawn()
        {
            this.rooms.MarkVisited("a");
            var room = MapTestData.Room("a", pickups: new[] { "p1" }, complete: this.complete);
            Assert.IsEmpty(Resolve(MapTestData.Map("deck-a", room)));
        }

        [Test]
        public void CurrentRoom_isFlagged_othersAreNot()
        {
            this.rooms.MarkVisited("a");
            this.rooms.MarkVisited("b");
            var visuals = Resolve(MapTestData.Map("deck-a", Room("a"), Room("b")), current: "b");
            Assert.IsFalse(visuals[0].IsCurrent);
            Assert.IsTrue(visuals[1].IsCurrent);
        }

        [Test]
        public void NullCurrentRoom_flagsNone()
        {
            this.rooms.MarkVisited("a");
            Assert.IsFalse(Resolve(MapTestData.Map("deck-a", Room("a")))[0].IsCurrent);
        }

        [Test]
        public void Visual_carriesPositionAndQuarterTurns()
        {
            this.rooms.MarkVisited("a");
            var room = MapTestData.Room("a", new Vector2Int(3, -4), 3, incomplete: this.incomplete, complete: this.complete);
            var visual = Resolve(MapTestData.Map("deck-a", room))[0];
            Assert.AreEqual(new Vector2Int(3, -4), visual.Position);
            Assert.AreEqual(3, visual.QuarterTurns);
        }

        [Test]
        public void Preview_ignoresDiscovery_andPicksRequestedSprite()
        {
            var map = MapTestData.Map("deck-a", Room("a", "p1"));
            Assert.AreSame(this.complete,   MapRoomVisuals.Preview(map, complete: true)[0].Sprite);
            Assert.AreSame(this.incomplete, MapRoomVisuals.Preview(map, complete: false)[0].Sprite);
        }
    }
}

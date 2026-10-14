#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapDoorVisualsTests
    {
        private RoomStateRegistry rooms     = null!;
        private DoorStateRegistry doors     = null!;
        private KnownMapsRegistry knownMaps = null!;

        [SetUp]
        public void SetUp()
        {
            this.rooms     = new RoomStateRegistry();
            this.doors     = new DoorStateRegistry();
            this.knownMaps = new KnownMapsRegistry();
        }

        private IReadOnlyList<MapDoorVisual> Resolve(MapData map)
            => MapDoorVisuals.Resolve(map, this.rooms, this.doors, this.knownMaps);

        private static MapData SingleDoorMap(Vector2Int roomPosition = default, Vector2 doorOffset = default)
            => MapTestData.Map(
                "deck-a",
                new[] { MapTestData.Room("a", roomPosition, doors: new[] { "d1" }) },
                new[] { MapTestData.Door("d1", doorOffset) });

        [Test]
        public void DoorOfUndiscoveredRoom_isHidden()
        {
            Assert.IsEmpty(Resolve(SingleDoorMap()));
        }

        [Test]
        public void DoorOfVisitedRoom_isDrawn()
        {
            this.rooms.MarkVisited("a");
            Assert.AreEqual(1, Resolve(SingleDoorMap()).Count);
        }

        [Test]
        public void DoorOfUnvisitedRoom_withMapItem_isDrawn()
        {
            this.knownMaps.MarkKnown("deck-a");
            Assert.AreEqual(1, Resolve(SingleDoorMap()).Count);
        }

        [Test]
        public void DoorNotOwnedByAnyRoom_isHidden_evenWithMapItem()
        {
            this.knownMaps.MarkKnown("deck-a");
            var map = MapTestData.Map(
                "deck-a",
                new[] { MapTestData.Room("a") },
                new[] { MapTestData.Door("orphan") });

            Assert.IsEmpty(Resolve(map));
        }

        [Test]
        public void UntouchedDoor_isUnknown()
        {
            this.rooms.MarkVisited("a");
            Assert.AreEqual(DoorMapState.Unknown, Resolve(SingleDoorMap())[0].State);
        }

        [Test]
        public void DoorTriedWhileLocked_isLocked()
        {
            this.rooms.MarkVisited("a");
            this.doors.MarkLocked("d1");
            Assert.AreEqual(DoorMapState.Locked, Resolve(SingleDoorMap())[0].State);
        }

        [Test]
        public void OpenedDoor_isUnlocked()
        {
            this.rooms.MarkVisited("a");
            this.doors.MarkUnlocked("d1");
            Assert.AreEqual(DoorMapState.Unlocked, Resolve(SingleDoorMap())[0].State);
        }

        [Test]
        public void DoorSharedByTwoVisibleRooms_isDrawnOnce()
        {
            this.rooms.MarkVisited("a");
            this.rooms.MarkVisited("b");
            var map = MapTestData.Map(
                "deck-a",
                new[]
                {
                    MapTestData.Room("a", doors: new[] { "d1" }),
                    MapTestData.Room("b", doors: new[] { "d1" }),
                },
                new[] { MapTestData.Door("d1") });

            Assert.AreEqual(1, Resolve(map).Count);
        }

        // Kitchen <-> Dinner: only DR_32B (in "dinner") is drawn; its far half DR_187 (in
        // "kitchen") is excluded from the map and recorded as its other side.
        private static MapData PairedDoorMap(Vector2Int dinnerPosition = default, Vector2 doorOffset = default)
            => MapTestData.Map(
                "deck-a",
                new[]
                {
                    MapTestData.Room("dinner", dinnerPosition, doors: new[] { "DR_32B" }),
                    MapTestData.Room("kitchen", new Vector2Int(100, 100)),
                },
                new[] { MapTestData.Door("DR_32B", doorOffset, otherSides: new[] { MapTestData.Side("DR_187", "kitchen") }) });

        [Test]
        public void PairedDoor_isHidden_whileNeitherRoomIsVisited()
        {
            Assert.IsEmpty(Resolve(PairedDoorMap()));
        }

        [Test]
        public void PairedDoor_isDrawn_fromItsOtherSidesRoom()
        {
            this.rooms.MarkVisited("kitchen");
            var visuals = Resolve(PairedDoorMap());

            Assert.AreEqual(1, visuals.Count);
            Assert.AreEqual("DR_32B", visuals[0].DoorId);
        }

        [Test]
        public void PairedDoor_seenFromItsOtherSide_staysPlacedOnItsOwnRoom()
        {
            this.rooms.MarkVisited("kitchen");
            var visual = Resolve(PairedDoorMap(new Vector2Int(10, -4), new Vector2(2f, 3f)))[0];

            Assert.AreEqual("dinner", visual.RoomId);
            Assert.AreEqual(new Vector2(12f, -1f), visual.Position);
        }

        [Test]
        public void PairedDoor_triedLockedFromItsOtherSide_isLocked()
        {
            this.rooms.MarkVisited("kitchen");
            this.doors.MarkLocked("DR_187");
            Assert.AreEqual(DoorMapState.Locked, Resolve(PairedDoorMap())[0].State);
        }

        [Test]
        public void PairedDoor_unlockedOnEitherHalf_beatsLockedOnTheOther()
        {
            this.rooms.MarkVisited("dinner");
            this.doors.MarkLocked("DR_187");
            this.doors.MarkUnlocked("DR_32B");
            Assert.AreEqual(DoorMapState.Unlocked, Resolve(PairedDoorMap())[0].State);
        }

        [Test]
        public void Visual_positionIsRoomPositionPlusDoorOffset()
        {
            this.rooms.MarkVisited("a");
            var visual = Resolve(SingleDoorMap(new Vector2Int(10, -4), new Vector2(2.5f, 3f)))[0];
            Assert.AreEqual(new Vector2(12.5f, -1f), visual.Position);
        }

        [Test]
        public void Visual_carriesIdRotationAndSize()
        {
            this.rooms.MarkVisited("a");
            var map = MapTestData.Map(
                "deck-a",
                new[] { MapTestData.Room("a", doors: new[] { "d1" }) },
                new[] { MapTestData.Door("d1", rotation: 90f, size: new Vector2(4f, 1f)) });

            var visual = Resolve(map)[0];
            Assert.AreEqual("d1", visual.DoorId);
            Assert.AreEqual(90f, visual.Rotation);
            Assert.AreEqual(new Vector2(4f, 1f), visual.Size);
        }

        [Test]
        public void Visual_carriesOwningRoomId()
        {
            this.rooms.MarkVisited("a");
            Assert.AreEqual("a", Resolve(SingleDoorMap())[0].RoomId);
        }

        [Test]
        public void Preview_showsEveryOwnedDoorAsUnknown_ignoringDiscoveryAndState()
        {
            this.doors.MarkUnlocked("d1");
            var map = MapTestData.Map(
                "deck-a",
                new[] { MapTestData.Room("a", doors: new[] { "d1" }) },
                new[] { MapTestData.Door("d1"), MapTestData.Door("orphan") });

            var preview = MapDoorVisuals.Preview(map);

            Assert.AreEqual(1, preview.Count);
            Assert.AreEqual("d1", preview[0].DoorId);
            Assert.AreEqual(DoorMapState.Unknown, preview[0].State);
        }

        [TestCase(DoorMapState.Locked)]
        [TestCase(DoorMapState.Unlocked)]
        public void Preview_canShowEveryDoorInARequestedState(DoorMapState state)
        {
            var preview = MapDoorVisuals.Preview(SingleDoorMap(), state);
            Assert.AreEqual(state, preview[0].State);
        }

        [TestCase(0f,    false)]
        [TestCase(180f,  false)]
        [TestCase(-180f, false)]
        [TestCase(90f,   true)]
        [TestCase(270f,  true)]
        [TestCase(-90f,  true)]
        public void Visual_isVertical_onOddQuarterTurns(float rotation, bool expected)
        {
            this.rooms.MarkVisited("a");
            var map = MapTestData.Map(
                "deck-a",
                new[] { MapTestData.Room("a", doors: new[] { "d1" }) },
                new[] { MapTestData.Door("d1", rotation: rotation) });

            Assert.AreEqual(expected, Resolve(map)[0].IsVertical);
        }

        [Test]
        public void DoorSprites_pickSpritePerStateAndOrientation()
        {
            var sprites = new MapDoorSprites
            {
                Horizontal = SpriteSet(),
                Vertical   = SpriteSet(),
            };

            Assert.AreSame(sprites.Horizontal.Unknown,  sprites.For(DoorMapState.Unknown,  vertical: false));
            Assert.AreSame(sprites.Horizontal.Locked,   sprites.For(DoorMapState.Locked,   vertical: false));
            Assert.AreSame(sprites.Horizontal.Unlocked, sprites.For(DoorMapState.Unlocked, vertical: false));
            Assert.AreSame(sprites.Vertical.Unknown,    sprites.For(DoorMapState.Unknown,  vertical: true));
            Assert.AreSame(sprites.Vertical.Locked,     sprites.For(DoorMapState.Locked,   vertical: true));
            Assert.AreSame(sprites.Vertical.Unlocked,   sprites.For(DoorMapState.Unlocked, vertical: true));
        }

        private static MapDoorSpriteSet SpriteSet()
            => new()
            {
                Unknown  = MapTestData.Sprite(),
                Locked   = MapTestData.Sprite(),
                Unlocked = MapTestData.Sprite(),
            };
    }
}

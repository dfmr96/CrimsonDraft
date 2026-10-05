#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapLayoutMergeTests
    {
        private static MapRoomData Baked(string id, string[]? pickups = null, string[]? doors = null)
            => new() { RoomId = id, PickupIds = pickups ?? new string[0], DoorIds = doors ?? new string[0] };

        [Test]
        public void Upsert_keepsLayout_andReplacesIds()
        {
            var sprite   = MapTestData.Sprite();
            var existing = MapTestData.Room("a", new Vector2Int(3, 4), 1, new[] { "p1" }, sprite, sprite);
            var result   = MapLayoutMerge.Upsert(new[] { existing }, new[] { Baked("a", new[] { "p2" }, new[] { "d1" }) });

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(sprite, result[0].IncompleteSprite);
            Assert.AreSame(sprite, result[0].CompleteSprite);
            Assert.AreEqual(new Vector2Int(3, 4), result[0].Position);
            Assert.AreEqual(1, result[0].QuarterTurns);
            CollectionAssert.AreEqual(new[] { "p2" }, result[0].PickupIds);
            CollectionAssert.AreEqual(new[] { "d1" }, result[0].DoorIds);
            Assert.IsFalse(result[0].IsOrphan);
        }

        [Test]
        public void Upsert_appendsNewRooms_afterExistingOnes()
        {
            var result = MapLayoutMerge.Upsert(new[] { MapTestData.Room("a") }, new[] { Baked("b"), Baked("a") });
            CollectionAssert.AreEqual(new[] { "a", "b" }, new[] { result[0].RoomId, result[1].RoomId });
            Assert.AreEqual(Vector2Int.zero, result[1].Position);
            Assert.IsNull(result[1].IncompleteSprite);
        }

        [Test]
        public void Upsert_keepsMissingRooms_markedAsOrphans()
        {
            var result = MapLayoutMerge.Upsert(new[] { MapTestData.Room("a"), MapTestData.Room("gone") }, new[] { Baked("a") });
            Assert.AreEqual(2, result.Count);
            Assert.IsFalse(result[0].IsOrphan);
            Assert.IsTrue(result[1].IsOrphan);
        }

        [Test]
        public void Upsert_clearsOrphan_whenRoomReturns()
        {
            var room = MapTestData.Room("a");
            room.IsOrphan = true;
            Assert.IsFalse(MapLayoutMerge.Upsert(new[] { room }, new[] { Baked("a") })[0].IsOrphan);
        }

        [Test]
        public void Upsert_collapsesDuplicateIds_keepingTheEntryWithLayout()
        {
            var sprite = MapTestData.Sprite();
            var bare   = MapTestData.Room("a");
            var laid   = MapTestData.Room("a", new Vector2Int(8, 8), 0, null, sprite, sprite);
            var result = MapLayoutMerge.Upsert(new[] { bare, laid, MapTestData.Room("a") }, new[] { Baked("a", new[] { "p1" }) });

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(sprite, result[0].IncompleteSprite);
            Assert.AreEqual(new Vector2Int(8, 8), result[0].Position);
            CollectionAssert.AreEqual(new[] { "p1" }, result[0].PickupIds);
        }

        [Test]
        public void Upsert_collapsedOrphan_unionsItsIds()
        {
            var first  = MapTestData.Room("gone", pickups: new[] { "p1" });
            var second = MapTestData.Room("gone", pickups: new[] { "p2" });
            var result = MapLayoutMerge.Upsert(new[] { first, second }, new MapRoomData[0]);

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].IsOrphan);
            CollectionAssert.AreEquivalent(new[] { "p1", "p2" }, result[0].PickupIds);
        }

        [Test]
        public void Upsert_isIdempotent()
        {
            var baked = new[] { Baked("a", new[] { "p1" }), Baked("b") };
            var once  = MapLayoutMerge.Upsert(new List<MapRoomData>(), baked);
            var twice = MapLayoutMerge.Upsert(once, baked);

            Assert.AreEqual(once.Count, twice.Count);
            for (int i = 0; i < once.Count; i++)
            {
                Assert.AreEqual(once[i].RoomId, twice[i].RoomId);
                CollectionAssert.AreEqual(once[i].PickupIds, twice[i].PickupIds);
                Assert.AreEqual(once[i].Position, twice[i].Position);
                Assert.AreEqual(once[i].IsOrphan, twice[i].IsOrphan);
            }
        }
    }
}

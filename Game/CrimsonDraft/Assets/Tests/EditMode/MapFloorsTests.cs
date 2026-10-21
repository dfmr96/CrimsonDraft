#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapFloorsTests
    {
        private MapData a = null!, b = null!, c = null!;

        [SetUp]
        public void SetUp()
        {
            this.a = MapTestData.Map("deck-a");
            this.b = MapTestData.Map("deck-b");
            this.c = MapTestData.Map("deck-c");
        }

        [Test]
        public void Available_keepsSetOrder_andSkipsUnknownAndNull()
        {
            var known = new KnownMapsRegistry();
            known.MarkKnown("deck-c");
            known.MarkKnown("deck-a");
            var result = MapFloors.Available(new MapData?[] { this.a, null, this.b, this.c }, new RoomStateRegistry(), known);
            CollectionAssert.AreEqual(new[] { this.a, this.c }, result);
        }

        [Test]
        public void Available_appendsPlayerFloor_whenTheSetLacksIt()
        {
            var known = new KnownMapsRegistry();
            known.MarkKnown("deck-a");
            var result = MapFloors.Available(new MapData?[] { this.a, this.b }, new RoomStateRegistry(), known, playerFloor: this.c);
            CollectionAssert.AreEqual(new[] { this.a, this.c }, result);
        }

        [Test]
        public void Available_doesNotDuplicatePlayerFloor_alreadyInTheSet()
        {
            var known = new KnownMapsRegistry();
            known.MarkKnown("deck-a");
            var result = MapFloors.Available(new MapData?[] { this.a }, new RoomStateRegistry(), known, playerFloor: this.a);
            CollectionAssert.AreEqual(new[] { this.a }, result);
        }

        [Test]
        public void Start_isPlayerFloor()
        {
            Assert.AreSame(this.b, new MapFloors(new[] { this.a, this.b, this.c }, this.b).Current);
        }

        [Test]
        public void Start_whenPlayerFloorUnavailable_isFirst()
        {
            Assert.AreSame(this.a, new MapFloors(new[] { this.a, this.c }, this.b).Current);
        }

        [Test]
        public void Empty_hasNoCurrent_andNoArrows()
        {
            var floors = new MapFloors(new MapData[0], this.a);
            Assert.IsNull(floors.Current);
            Assert.IsFalse(floors.HasUp);
            Assert.IsFalse(floors.HasDown);
            Assert.IsFalse(floors.Step(+1));
        }

        [Test]
        public void Step_movesAndReportsArrows()
        {
            var floors = new MapFloors(new[] { this.a, this.b, this.c }, this.b);
            Assert.IsTrue(floors.HasUp);
            Assert.IsTrue(floors.HasDown);
            Assert.IsTrue(floors.Step(-1));
            Assert.AreSame(this.a, floors.Current);
            Assert.IsFalse(floors.HasUp);
        }

        [Test]
        public void Step_clampsAtBothEnds_withoutWrapping()
        {
            var floors = new MapFloors(new[] { this.a, this.b }, this.a);
            Assert.IsFalse(floors.Step(-1));
            Assert.AreSame(this.a, floors.Current);
            Assert.IsTrue(floors.Step(+1));
            Assert.IsFalse(floors.Step(+1));
            Assert.AreSame(this.b, floors.Current);
        }
    }
}

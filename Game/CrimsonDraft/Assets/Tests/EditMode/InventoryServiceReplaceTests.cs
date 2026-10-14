#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed partial class InventoryServiceTests
    {
        // One operator whose 4x4 grid holds a 1x2 flask rotated flat at (0,0) and is otherwise
        // full -- the only free room for a new 1x2 item is the flask's own (rotated) spot.
        private static InventoryService ServiceWithRotatedFlaskAndNoRoom(ItemData flask)
        {
            var s    = Service(operators: 1);
            var item = AddAt(s, flask, 0);
            s.TryPickUp(item);
            s.RotateHeld();
            Assert.AreEqual(DropResult.Placed, s.TryDrop(ContainerId.Operator(0), Vector2Int.zero));
            Assert.IsTrue(s.TryAdd(Sized(Consumable(), 2, 1)));
            Assert.IsTrue(s.TryAdd(Sized(Consumable(), 4, 3)));
            return s;
        }

        [Test]
        public void TryReplace_putsReplacementInTheSameRotatedSpot()
        {
            var s = ServiceWithRotatedFlaskAndNoRoom(Sized(Key(id: "empty"), 1, 2));

            int changes = CountChanges(Op(s, 0), () =>
                Assert.IsTrue(s.TryReplace("empty", Sized(Key(id: "filled"), 1, 2))));

            Assert.AreEqual(1, changes);
            Assert.IsFalse(s.HasItem("empty"));
            Assert.IsTrue(s.HasItem("filled"));
            var placement = Op(s, 0).GetPlacement(Op(s, 0).GetItemAt(Vector2Int.zero)!)!;
            Assert.AreEqual("filled", placement.Item.Data.ItemId);
            Assert.AreEqual(Vector2Int.zero, placement.Origin);
            Assert.AreEqual(1, placement.Rotation);
        }

        [Test]
        public void TryReplace_replacementTooBigForTheSpot_goesToAnyFreeCell()
        {
            var s = Service(operators: 2);
            AddAt(s, Key(id: "empty"), 0);
            Assert.IsTrue(s.TryAdd(Sized(Consumable(), 3, 4), ContainerId.Operator(0)));

            Assert.IsTrue(s.TryReplace("empty", Sized(Key(id: "big"), 2, 2)));

            Assert.IsFalse(s.HasItem("empty"));
            Assert.AreEqual(1, Op(s, 1).Count);
        }

        [Test]
        public void TryReplace_withoutRoom_keepsTheOriginalWhereItWas()
        {
            var s = ServiceWithRotatedFlaskAndNoRoom(Sized(Key(id: "empty"), 1, 2));

            Assert.IsFalse(s.TryReplace("empty", Sized(Key(id: "big"), 2, 2)));

            Assert.IsTrue(s.HasItem("empty"));
            var placement = Op(s, 0).GetPlacement(Op(s, 0).GetItemAt(Vector2Int.zero)!)!;
            Assert.AreEqual("empty", placement.Item.Data.ItemId);
            Assert.AreEqual(1, placement.Rotation);
        }

        [Test]
        public void TryReplace_missingItem_returnsFalse()
        {
            var s = Service();
            Assert.IsFalse(s.TryReplace("nothing", Key()));
            Assert.AreEqual(0, Op(s, 0).Count);
        }
    }
}

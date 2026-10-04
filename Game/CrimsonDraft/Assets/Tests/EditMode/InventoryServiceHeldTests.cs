#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed partial class InventoryServiceTests
    {
        private static InventoryItem AddAt(InventoryService s, ItemData data, int op)
        {
            Assert.IsTrue(s.TryAdd(data, ContainerId.Operator(op)));
            return Op(s, op).Placements.Last(p => p.Item.Data == data).Item;
        }

        [Test]
        public void TryPickUp_movesItemToHand_andRaisesBothEvents()
        {
            var s    = Service();
            var item = AddAt(s, Consumable(), 0);
            int held = 0;
            s.HeldChanged += () => held++;

            int changes = CountChanges(Op(s, 0), () => Assert.IsTrue(s.TryPickUp(item)));

            Assert.AreSame(item, s.Held);
            Assert.AreEqual(0, Op(s, 0).Count);
            Assert.AreEqual(1, changes);
            Assert.AreEqual(1, held);
        }

        [Test]
        public void TryPickUp_rejectsEquippedItem_andSecondPickUp()
        {
            var s      = Service();
            var weapon = AddAt(s, Weapon(), 0);
            weapon.SetEquipped(0, 0);
            Assert.IsFalse(s.TryPickUp(weapon));

            var a = AddAt(s, Consumable(), 0);
            var b = AddAt(s, Consumable(), 0);
            Assert.IsTrue(s.TryPickUp(a));
            Assert.IsFalse(s.TryPickUp(b));
        }

        [Test]
        public void TryDrop_onEmptyCell_placesWithHeldRotation()
        {
            var s    = Service();
            var item = AddAt(s, Sized(Consumable(), 2, 1), 0);
            s.TryPickUp(item);
            s.RotateHeld();

            Assert.AreEqual(DropResult.Placed, s.TryDrop(ContainerId.Operator(1), new Vector2Int(3, 0)));
            Assert.IsNull(s.Held);
            var placement = Op(s, 1).GetPlacement(item)!;
            Assert.AreEqual(1, placement.Rotation);
            Assert.AreEqual(new Vector2Int(1, 2), placement.Footprint);
        }

        [Test]
        public void TryDrop_outOfBounds_isRejected_andKeepsHeld()
        {
            var s    = Service();
            var item = AddAt(s, Sized(Consumable(), 2, 1), 0);
            s.TryPickUp(item);

            Assert.AreEqual(DropResult.Rejected, s.TryDrop(ContainerId.Operator(0), new Vector2Int(3, 0)));
            Assert.AreSame(item, s.Held);
        }

        [Test]
        public void TryDrop_overTwoItems_isRejected()
        {
            var s = Service();
            AddAt(s, Consumable(), 1);
            AddAt(s, Consumable(), 1);
            var held = AddAt(s, Sized(Consumable(), 2, 1), 0);
            s.TryPickUp(held);

            Assert.AreEqual(DropResult.Rejected, s.TryDrop(ContainerId.Operator(1), Vector2Int.zero));
        }

        [Test]
        public void TryDrop_rejected_raisesNoChanged()
        {
            var s    = Service();
            var item = AddAt(s, Sized(Consumable(), 2, 1), 0);
            s.TryPickUp(item);

            int changes = CountChanges(Op(s, 0), () => s.TryDrop(ContainerId.Operator(0), new Vector2Int(3, 3)));
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void TryDrop_onSingleItem_swaps_andCancelReturnsDisplacedToHeldOrigin()
        {
            var s = Service();
            var a = AddAt(s, Consumable(), 0);
            var b = AddAt(s, Sized(Consumable(), 2, 1), 1);
            s.TryPickUp(a);

            Assert.AreEqual(DropResult.Swapped, s.TryDrop(ContainerId.Operator(1), Vector2Int.zero));
            Assert.AreSame(b, s.Held);
            Assert.AreEqual(Vector2Int.zero, Op(s, 1).GetPlacement(a)!.Origin);

            s.CancelHeld();
            Assert.IsNull(s.Held);
            Assert.AreEqual(Vector2Int.zero, Op(s, 0).GetPlacement(b)!.Origin);
        }

        [Test]
        public void TryDrop_swapRejected_whenDisplacedCannotReturn()
        {
            var s = Service();
            var a = AddAt(s, Consumable(), 0);
            AddAt(s, Sized(Consumable(), 3, 4), 0);
            AddAt(s, Sized(Consumable(), 1, 3), 0);
            var b = AddAt(s, Sized(Consumable(), 2, 1), 1);
            s.TryPickUp(a);

            Assert.AreEqual(DropResult.Rejected, s.TryDrop(ContainerId.Operator(1), Vector2Int.zero));
            Assert.AreSame(a, s.Held);
            Assert.AreEqual(Vector2Int.zero, Op(s, 1).GetPlacement(b)!.Origin);
        }

        [Test]
        public void TryDrop_ontoEquippedItem_isRejected()
        {
            var s      = Service();
            var a      = AddAt(s, Consumable(), 0);
            var weapon = AddAt(s, Weapon(), 1);
            weapon.SetEquipped(1, 0);
            s.TryPickUp(a);

            Assert.AreEqual(DropResult.Rejected, s.TryDrop(ContainerId.Operator(1), Vector2Int.zero));
        }

        [Test]
        public void CancelHeld_returnsItemToOriginalPlacement()
        {
            var s = Service();
            AddAt(s, Consumable(), 0);
            var item   = AddAt(s, Sized(Consumable(), 2, 1), 0);
            var origin = Op(s, 0).GetPlacement(item)!.Origin;
            s.TryPickUp(item);
            s.RotateHeld();

            s.CancelHeld();

            var placement = Op(s, 0).GetPlacement(item)!;
            Assert.AreEqual(origin, placement.Origin);
            Assert.AreEqual(0, placement.Rotation);
            Assert.IsNull(s.Held);
        }

        [Test]
        public void TrySplit_movesHalfToHand_andCancelMergesBack()
        {
            var s     = Service();
            var stack = AddAt(s, Ammo(defaultQuantity: 30), 0);

            Assert.IsTrue(s.TrySplit(stack));
            Assert.AreEqual(15, stack.Quantity);
            Assert.AreEqual(15, s.Held!.Quantity);

            s.CancelHeld();
            Assert.AreEqual(30, stack.Quantity);
            Assert.AreEqual(1, Op(s, 0).Count);
        }

        [Test]
        public void TrySplit_rejectsSingleUnitAndNonStackable()
        {
            var s = Service();
            Assert.IsFalse(s.TrySplit(AddAt(s, Ammo(defaultQuantity: 1), 0)));
            Assert.IsFalse(s.TrySplit(AddAt(s, Consumable(), 0)));
        }

        [Test]
        public void TryDrop_splitOntoItem_isRejected_butOntoEmptyCellPlacesNewStack()
        {
            var s     = Service();
            var stack = AddAt(s, Ammo(defaultQuantity: 30), 0);
            s.TrySplit(stack);

            Assert.AreEqual(DropResult.Rejected, s.TryDrop(ContainerId.Operator(0), Vector2Int.zero));
            Assert.AreEqual(DropResult.Placed, s.TryDrop(ContainerId.Operator(1), Vector2Int.zero));
            Assert.AreEqual(15, Only(Op(s, 1)).Quantity);
        }

        [Test]
        public void CanSplit_andTrySplit_rejected_whenNoFreeCellAnywhere()
        {
            var s     = Service(operators: 1);
            var stack = AddAt(s, Ammo(defaultQuantity: 30), 0);
            AddAt(s, Sized(Consumable(), 3, 4), 0);
            AddAt(s, Sized(Consumable(), 1, 3), 0);

            Assert.IsFalse(s.CanSplit(stack));
            Assert.IsFalse(s.TrySplit(stack));
            Assert.AreEqual(30, stack.Quantity);
            Assert.IsNull(s.Held);
        }

        [Test]
        public void CanSplit_trueWithRoom_andHeldIsSplitTracksTheHand()
        {
            var s     = Service();
            var stack = AddAt(s, Ammo(defaultQuantity: 30), 0);
            var other = AddAt(s, Consumable(), 0);

            Assert.IsTrue(s.CanSplit(stack));
            Assert.IsFalse(s.HeldIsSplit);
            s.TrySplit(stack);
            Assert.IsTrue(s.HeldIsSplit);
            s.CancelHeld();
            s.TryPickUp(other);
            Assert.IsFalse(s.HeldIsSplit);
        }
    }
}

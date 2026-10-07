#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed partial class InventoryServiceTests
    {
        private static ItemContainer Box(InventoryService s) => s.GetContainer(ContainerId.Storage);

        private static InventoryItem Stored(InventoryService s, ItemData data, int quantity = 0)
        {
            Assert.IsTrue(s.TryAdd(data, ContainerId.Storage, quantity));
            return Box(s).Placements.Last(p => p.Item.Data == data).Item;
        }

        [Test]
        public void TryAdd_auto_neverUsesStorage()
        {
            var s = Service(operators: 1);
            s.TryAdd(Sized(Consumable(), 4, 4));

            Assert.IsFalse(s.TryAdd(Consumable()));
            Assert.AreEqual(0, Box(s).Count);
        }

        [Test]
        public void TryConsumeOne_stack_removesOnlyOneUnit()
        {
            var s    = Service();
            var tape = Consumable(stackable: true, maxStack: 5, id: "ticker_tape");
            Assert.IsTrue(s.TryAdd(tape, ContainerId.Operator(0), 3));

            Assert.IsTrue(s.TryConsumeOne(tape));

            Assert.AreEqual(2, Only(Op(s, 0)).Quantity);
        }

        [Test]
        public void TryConsumeOne_lastUnit_removesTheItem()
        {
            var s    = Service();
            var tape = Consumable(stackable: true, maxStack: 5, id: "ticker_tape");
            Assert.IsTrue(s.TryAdd(tape, ContainerId.Operator(0), 1));

            Assert.IsTrue(s.TryConsumeOne(tape));

            Assert.AreEqual(0, Op(s, 0).Count);
            Assert.IsFalse(s.HasItem("ticker_tape"));
        }

        [Test]
        public void TryConsumeOne_notifiesTheContainer()
        {
            var s    = Service();
            var tape = Consumable(stackable: true, maxStack: 5, id: "ticker_tape");
            Assert.IsTrue(s.TryAdd(tape, ContainerId.Operator(0), 3));

            Assert.AreEqual(1, CountChanges(Op(s, 0), () => s.TryConsumeOne(tape)));
        }

        [Test]
        public void TryConsumeOne_ignoresStorage()
        {
            var s    = Service();
            var tape = Consumable(stackable: true, maxStack: 5, id: "ticker_tape");
            var box  = Stored(s, tape, 3);

            Assert.IsFalse(s.TryConsumeOne(tape));
            Assert.AreEqual(3, box.Quantity);
        }

        [Test]
        public void TryUseKey_andHasItem_ignoreStorage()
        {
            var s = Service();
            Stored(s, Key(id: "door-key"));

            Assert.IsFalse(s.HasItem("door-key"));
            Assert.AreEqual(KeyUseResult.NotFound, s.TryUseKey("door-key").Result);
            Assert.IsFalse(s.TryRemove("door-key"));
            Assert.AreEqual(1, Box(s).Count);
        }

        [Test]
        public void HasEquippedWeapon_ignoresStorage_andEquipFromStorageIsNoOp()
        {
            var roster = FakeRoster.WithOperators(1);
            var s      = Service(roster);
            var weapon = (WeaponItem)Stored(s, Weapon());

            s.Equip(weapon, 0);

            Assert.IsFalse(weapon.IsEquipped);
            Assert.IsNull(roster[0].PrimaryWeapon);
            Assert.IsFalse(s.HasEquippedWeapon(0));
        }

        [Test]
        public void Actions_onStoredItems_areRejected_withoutChanged()
        {
            var a       = Consumable();
            var b       = Consumable();
            var s       = Service(combine: new FakeCombineService(a, b, Consumable()));
            var storedA = Stored(s, a);
            var carried = AddAt(s, b, 0);
            var ammo    = Stored(s, Ammo(Caliber._9mm, defaultQuantity: 10));
            var weapon  = (WeaponItem)AddAt(s, Weapon(Caliber._9mm, magazine: 6), 0);
            s.Equip(weapon, 0);

            int changes = CountChanges(Box(s), () =>
            {
                Assert.IsFalse(s.TryCombine(storedA, carried, out _));
                Assert.IsFalse(s.TryUseConsumable(storedA, 0));
                Assert.IsFalse(s.CanReload(ammo, 0));
                Assert.IsFalse(s.TryReload(ammo, 0));
                Assert.IsFalse(s.CanSplit(ammo));
                Assert.IsFalse(s.TrySplit(ammo));
            });

            Assert.AreEqual(0, changes);
            Assert.AreEqual(0, weapon.CurrentAmmo);
        }

        [Test]
        public void StoreAndRetrieve_moveItemsBetweenOperatorAndStorage()
        {
            var s    = Service();
            var item = AddAt(s, Consumable(id: "med"), 0);

            int boxChanges = CountChanges(Box(s), () =>
            {
                Assert.IsTrue(s.TryPickUp(item));
                Assert.AreEqual(DropResult.Placed, s.TryDrop(ContainerId.Storage, new Vector2Int(5, 2)));
            });

            Assert.AreEqual(1, boxChanges);
            Assert.AreEqual(ContainerId.Storage, s.FindContainerOf(item));
            Assert.IsFalse(s.HasItem("med"));

            Assert.IsTrue(s.TryPickUp(item));
            Assert.AreEqual(DropResult.Placed, s.TryDrop(ContainerId.Operator(1), Vector2Int.zero));
            Assert.IsTrue(s.HasItem("med"));
        }

        [Test]
        public void TryDrop_swapBetweenStorageAndOperator_returnsDisplacedToStorage()
        {
            var s       = Service();
            var fromBox = Stored(s, Consumable());
            var onOp    = AddAt(s, Sized(Consumable(), 2, 1), 0);

            s.TryPickUp(fromBox);
            Assert.AreEqual(DropResult.Swapped, s.TryDrop(ContainerId.Operator(0), Vector2Int.zero));
            Assert.AreSame(onOp, s.Held);

            s.CancelHeld();
            Assert.AreEqual(ContainerId.Storage, s.FindContainerOf(onOp));
            Assert.AreEqual(ContainerId.Operator(0), s.FindContainerOf(fromBox));
        }

        [Test]
        public void TryDrop_swapIntoStorageRejected_whenDisplacedCannotReturn()
        {
            var s       = Service(operators: 1);
            var single  = AddAt(s, Consumable(), 0);
            AddAt(s, Sized(Consumable(), 3, 4), 0);
            AddAt(s, Sized(Consumable(), 1, 3), 0);
            var wide    = Stored(s, Sized(Consumable(), 2, 1));
            var wideAt  = Box(s).GetPlacement(wide)!.Origin;

            s.TryPickUp(single);
            Assert.AreEqual(DropResult.Rejected, s.TryDrop(ContainerId.Storage, wideAt));
            Assert.AreSame(single, s.Held);
            Assert.AreEqual(wideAt, Box(s).GetPlacement(wide)!.Origin);
        }

        [Test]
        public void CancelHeld_returnsItemTakenFromStorageToStorage()
        {
            var s    = Service();
            var item = Stored(s, Consumable());
            var at   = Box(s).GetPlacement(item)!.Origin;

            s.TryPickUp(item);
            s.CancelHeld();

            Assert.AreEqual(ContainerId.Storage, s.FindContainerOf(item));
            Assert.AreEqual(at, Box(s).GetPlacement(item)!.Origin);
        }
    }
}

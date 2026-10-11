#nullable enable

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed partial class InventoryServiceTests
    {
        private static InventoryService Service(int operators = 2, ICombineService? combine = null) =>
            new InventoryService(FakeRoster.WithOperators(operators), combine ?? FakeCombineService.None, FakeCorpseAccess.None);

        private static InventoryService Service(FakeRoster roster, ICombineService? combine = null, ICorpseAccess? access = null) =>
            new InventoryService(roster, combine ?? FakeCombineService.None, access ?? FakeCorpseAccess.None);

        private static ItemContainer Op(InventoryService s, int index) => s.GetContainer(ContainerId.Operator(index));

        private static int CountChanges(ItemContainer container, Action action)
        {
            int count = 0;
            void Handler() => count++;
            container.Changed += Handler;
            action();
            container.Changed -= Handler;
            return count;
        }

        private static InventoryItem Only(ItemContainer container) => container.Placements.Single().Item;

        [Test]
        public void OperatorContainers_oneFourByFourPerOperator()
        {
            var s = Service(operators: 3);
            Assert.AreEqual(3, s.OperatorContainers.Count);
            Assert.AreEqual(4, s.OperatorContainers[2].Width);
            Assert.AreEqual(4, s.OperatorContainers[2].Height);
            Assert.AreEqual(ContainerId.Operator(2), s.OperatorContainers[2].Id);
        }

        [Test]
        public void GetContainer_storage_isTwelveByFour()
        {
            var storage = Service().GetContainer(ContainerId.Storage);
            Assert.AreEqual(ContainerId.Storage, storage.Id);
            Assert.AreEqual(12, storage.Width);
            Assert.AreEqual(4, storage.Height);
        }

        [Test]
        public void TryAdd_placesAtFirstFreeCellOfFirstOperator_andRaisesChangedOnce()
        {
            var s       = Service();
            int changes = CountChanges(Op(s, 0), () => Assert.IsTrue(s.TryAdd(Consumable())));

            Assert.AreEqual(1, changes);
            Assert.AreEqual(Vector2Int.zero, Op(s, 0).Placements.Single().Origin);
            Assert.AreEqual(0, Op(s, 1).Count);
        }

        [Test]
        public void TryAdd_skipsFullOperator()
        {
            var s = Service();
            Assert.IsTrue(s.TryAdd(Sized(Consumable(), 4, 4)));
            Assert.IsTrue(s.TryAdd(Consumable()));
            Assert.AreEqual(1, Op(s, 1).Count);
        }

        [Test]
        public void TryAdd_whenFull_returnsFalse_andRaisesNoChanged()
        {
            var s = Service(operators: 1);
            s.TryAdd(Sized(Consumable(), 4, 4));

            int changes = CountChanges(Op(s, 0), () => Assert.IsFalse(s.TryAdd(Consumable())));
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void TryAdd_toTarget_onlyUsesThatContainer()
        {
            var s = Service();
            Assert.IsTrue(s.TryAdd(Consumable(), ContainerId.Operator(1)));
            Assert.AreEqual(0, Op(s, 0).Count);
            Assert.AreEqual(1, Op(s, 1).Count);
        }

        [Test]
        public void TryAdd_stackable_fillsExistingStackThenOverflowsToNewStack()
        {
            var s    = Service(operators: 1);
            var ammo = Ammo(defaultQuantity: 20, maxStack: 30);
            s.TryAdd(ammo);
            Assert.IsTrue(s.TryAdd(ammo, quantity: 15));

            var quantities = Op(s, 0).Placements.Select(p => p.Item.Quantity).OrderByDescending(q => q).ToArray();
            CollectionAssert.AreEqual(new[] { 30, 5 }, quantities);
        }

        [Test]
        public void TryAdd_stackableOverflowWithoutRoom_returnsFalse_andLeavesStacksUntouched()
        {
            var s    = Service(operators: 1);
            var ammo = Ammo(defaultQuantity: 25, maxStack: 30);
            s.TryAdd(ammo);
            s.TryAdd(Sized(Consumable(), 3, 4));
            s.TryAdd(Sized(Consumable(), 1, 3));

            Assert.IsFalse(s.TryAdd(ammo, quantity: 10));
            var stack = Op(s, 0).Placements.Single(p => p.Item is AmmoBoxItem).Item;
            Assert.AreEqual(25, stack.Quantity);
        }

        [Test]
        public void TryAdd_ammoWithoutQuantity_usesDefault()
        {
            var s = Service();
            s.TryAdd(Ammo(defaultQuantity: 12));
            Assert.AreEqual(12, Only(Op(s, 0)).Quantity);
        }

        [Test]
        public void Remove_removesItem_andRaisesChanged()
        {
            var s = Service();
            s.TryAdd(Consumable());
            var item = Only(Op(s, 0));

            int changes = CountChanges(Op(s, 0), () => s.Remove(item));
            Assert.AreEqual(1, changes);
            Assert.AreEqual(0, Op(s, 0).Count);
        }

        [Test]
        public void Remove_itemNotInInventory_throws()
        {
            var stranger = InventoryItemFactory.Create(Consumable());
            Assert.Throws<ArgumentException>(() => Service().Remove(stranger));
        }

        [Test]
        public void TryRemove_andHasItem_workById()
        {
            var s    = Service();
            var data = Consumable(id: "medkit");
            s.TryAdd(data);

            Assert.IsTrue(s.HasItem("medkit"));
            Assert.IsTrue(s.TryRemove("medkit"));
            Assert.IsFalse(s.HasItem("medkit"));
            Assert.IsFalse(s.TryRemove("medkit"));
        }

        [Test]
        public void FindContainerOf_returnsOwningContainer_orNull()
        {
            var s = Service();
            s.TryAdd(Consumable(), ContainerId.Operator(1));

            Assert.AreEqual(ContainerId.Operator(1), s.FindContainerOf(Only(Op(s, 1))));
            Assert.IsNull(s.FindContainerOf(InventoryItemFactory.Create(Consumable())));
        }
    }
}

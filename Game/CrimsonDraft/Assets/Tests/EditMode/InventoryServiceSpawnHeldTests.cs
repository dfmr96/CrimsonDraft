#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed partial class InventoryServiceTests
    {
        [Test]
        public void TrySpawnHeld_putsItemInHand_andRaisesHeldChanged()
        {
            var s    = Service();
            var item = InventoryItemFactory.Create(Consumable());
            int held = 0;
            s.HeldChanged += () => held++;

            Assert.IsTrue(s.TrySpawnHeld(item));

            Assert.AreSame(item, s.Held);
            Assert.IsTrue(s.HeldIsSpawned);
            Assert.AreEqual(0, s.HeldRotation);
            Assert.AreEqual(1, held);
        }

        [Test]
        public void TrySpawnHeld_whileHoldingPickedUpItem_isRejected()
        {
            var s      = Service();
            var picked = AddAt(s, Consumable(), 0);
            s.TryPickUp(picked);

            Assert.IsFalse(s.TrySpawnHeld(InventoryItemFactory.Create(Consumable())));
            Assert.AreSame(picked, s.Held);
            Assert.IsFalse(s.HeldIsSpawned);
        }

        [Test]
        public void TrySpawnHeld_whileHoldingSpawnedItem_replacesIt()
        {
            var s      = Service();
            var first  = InventoryItemFactory.Create(Consumable());
            var second = InventoryItemFactory.Create(Key());
            s.TrySpawnHeld(first);

            Assert.IsTrue(s.TrySpawnHeld(second));
            Assert.AreSame(second, s.Held);
            Assert.IsTrue(s.HeldIsSpawned);
        }

        [Test]
        public void TryDrop_spawnedItem_onEmptyCell_placesIt()
        {
            var s    = Service();
            var item = InventoryItemFactory.Create(Consumable());
            s.TrySpawnHeld(item);

            Assert.AreEqual(DropResult.Placed, s.TryDrop(ContainerId.Operator(0), Vector2Int.zero));
            Assert.IsNull(s.Held);
            Assert.IsFalse(s.HeldIsSpawned);
            Assert.IsNotNull(Op(s, 0).GetPlacement(item));
        }

        [Test]
        public void TryDrop_spawnedItem_overAnotherItem_isRejected_withoutSwap()
        {
            var s        = Service();
            var occupant = AddAt(s, Consumable(), 0);
            var origin   = Op(s, 0).GetPlacement(occupant)!.Origin;
            var item     = InventoryItemFactory.Create(Consumable());
            s.TrySpawnHeld(item);

            Assert.AreEqual(DropResult.Rejected, s.TryDrop(ContainerId.Operator(0), origin));
            Assert.AreSame(item, s.Held);
            Assert.AreSame(occupant, Op(s, 0).GetItemAt(origin));
        }

        [Test]
        public void CancelHeld_spawnedItem_discardsIt()
        {
            var s = Service();
            s.TrySpawnHeld(InventoryItemFactory.Create(Consumable()));

            int changes = CountChanges(Op(s, 0), () => s.CancelHeld());

            Assert.IsNull(s.Held);
            Assert.IsFalse(s.HeldIsSpawned);
            Assert.AreEqual(0, Op(s, 0).Count);
            Assert.AreEqual(0, changes);
        }
    }
}

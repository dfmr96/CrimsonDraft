#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class InventorySerializerTests
    {
        private static InventoryService Service(FakeRoster? roster = null) =>
            new InventoryService(roster ?? FakeRoster.WithOperators(2), FakeCombineService.None);

        private static ItemContainer Op(InventoryService s, int i) => s.GetContainer(ContainerId.Operator(i));

        private static InventoryItemEntry Entry(string id, int op, int col, int row, int quantity = 0) =>
            new InventoryItemEntry
            {
                containerKind  = (int)ContainerKind.Operator,
                containerIndex = op,
                itemId         = id,
                col            = col,
                row            = row,
                quantity       = quantity,
            };

        [Test]
        public void CaptureThenRestore_roundTripsEveryItemField()
        {
            var weaponData = Weapon(magazine: 6, id: "pistol");
            var ammoData   = Ammo(defaultQuantity: 30, id: "ammo");
            var keyData    = Key(maxUses: 3, id: "key");
            var medData    = Sized(Consumable(id: "med"), 1, 2);
            var source     = Service();

            source.TryAdd(weaponData, ContainerId.Operator(0));
            var weapon = (WeaponItem)Op(source, 0).Placements.Single().Item;
            weapon.SetAmmo(4);
            source.Equip(weapon, 0);
            source.TryAdd(ammoData, ContainerId.Operator(1), 12);
            source.TryAdd(keyData, ContainerId.Operator(1));
            source.TryUseKey("key");
            source.TryAdd(medData, ContainerId.Operator(1));
            var med = Op(source, 1).Placements.Single(p => p.Item.Data == medData).Item;
            med.IsExamined = true;
            source.TryPickUp(med);
            source.RotateHeld();
            Assert.AreEqual(DropResult.Placed, source.TryDrop(ContainerId.Operator(1), new Vector2Int(2, 3)));

            var entries        = InventorySerializer.Capture(source);
            var restoredRoster = FakeRoster.WithOperators(2);
            var restored       = Service(restoredRoster);
            restored.Restore(entries, Database(weaponData, ammoData, keyData, medData));

            var rWeapon = (WeaponItem)Op(restored, 0).Placements.Single().Item;
            Assert.AreEqual(4, rWeapon.CurrentAmmo);
            Assert.AreSame(rWeapon, restoredRoster[0].PrimaryWeapon);
            Assert.AreEqual(12, Op(restored, 1).Placements.Single(p => p.Item.Data == ammoData).Item.Quantity);
            Assert.AreEqual(2, ((KeyItem)Op(restored, 1).Placements.Single(p => p.Item.Data == keyData).Item).UsesRemaining);
            var rMed = Op(restored, 1).Placements.Single(p => p.Item.Data == medData);
            Assert.AreEqual(new Vector2Int(2, 3), rMed.Origin);
            Assert.AreEqual(1, rMed.Rotation);
            Assert.IsTrue(rMed.Item.IsExamined);
        }

        [Test]
        public void Capture_whileHolding_throws()
        {
            var s = Service();
            s.TryAdd(Consumable());
            s.TryPickUp(Op(s, 0).Placements.Single().Item);
            Assert.Throws<InvalidOperationException>(() => InventorySerializer.Capture(s));
        }

        [Test]
        public void Restore_unpositionedEntry_goesToFreeCell()
        {
            var data = Consumable(id: "med");
            var s    = Service();
            s.Restore(new[] { Entry("med", 1, -1, -1) }, Database(data));
            Assert.AreEqual(Vector2Int.zero, Op(s, 1).Placements.Single().Origin);
        }

        [Test]
        public void Restore_overlappingEntries_secondGoesToFreeCell()
        {
            var data = Consumable(id: "med");
            var s    = Service();
            s.Restore(new[] { Entry("med", 0, 0, 0), Entry("med", 0, 0, 0) }, Database(data));
            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) },
                Op(s, 0).Placements.Select(p => p.Origin).ToArray());
        }

        [Test]
        public void Restore_unpositionedEntryFirst_doesNotDisplacePositionedEntries()
        {
            var ammo   = Ammo(defaultQuantity: 15, id: "ammo");
            var rifle  = Sized(Weapon(id: "rifle"), 2, 1);
            var small  = Consumable(id: "small");
            var wide   = Sized(Consumable(id: "wide"), 4, 3);
            var s      = Service(FakeRoster.WithOperators(1));

            s.Restore(new[]
            {
                Entry("ammo", 0, -1, -1, 15),
                Entry("small", 0, 2, 0),
                Entry("wide", 0, 0, 1),
                Entry("rifle", 0, 0, 0),
            }, Database(ammo, rifle, small, wide));

            Assert.AreEqual(4, Op(s, 0).Count);
            Assert.AreEqual(Vector2Int.zero, Op(s, 0).Placements.Single(p => p.Item.Data == rifle).Origin);
            Assert.AreEqual(new Vector2Int(3, 0), Op(s, 0).Placements.Single(p => p.Item.Data == ammo).Origin);
        }

        [Test]
        public void Restore_unpositionedEntry_rotatesWhenOnlyAVerticalSpotIsFree()
        {
            var bar  = Sized(Consumable(id: "bar"), 2, 1);
            var wall = Sized(Consumable(id: "wall"), 3, 4);
            var s    = Service(FakeRoster.WithOperators(1));

            s.Restore(new[] { Entry("wall", 0, 1, 0), Entry("bar", 0, -1, -1) }, Database(bar, wall));

            var placement = Op(s, 0).Placements.Single(p => p.Item.Data == bar);
            Assert.AreEqual(1, placement.Rotation);
            Assert.AreEqual(Vector2Int.zero, placement.Origin);
        }

        [Test]
        public void Restore_withNoRoomAnywhere_dropsItemAndLogsError()
        {
            var big   = Sized(Consumable(id: "big"), 4, 4);
            var small = Consumable(id: "small");
            var s     = Service(FakeRoster.WithOperators(1));

            LogAssert.Expect(LogType.Error, new Regex("small"));
            s.Restore(new[] { Entry("big", 0, 0, 0), Entry("small", 0, 0, 0) }, Database(big, small));
            Assert.AreEqual(1, Op(s, 0).Count);
        }

        [Test]
        public void Restore_skipsUnknownIds_andClearsPreviousState()
        {
            var data = Consumable(id: "med");
            var s    = Service();
            s.TryAdd(Consumable(id: "old"));
            s.Restore(new[] { Entry("ghost", 0, 0, 0), Entry("med", 0, 1, 0) }, Database(data));

            Assert.IsFalse(s.HasItem("old"));
            Assert.AreEqual(1, Op(s, 0).Count);
        }

        [Test]
        public void Restore_raisesChangedOnEveryContainer()
        {
            var s       = Service();
            int changes = 0;
            foreach (var c in s.OperatorContainers) c.Changed += () => changes++;
            s.Restore(Array.Empty<InventoryItemEntry>(), Database());
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void FromLegacySlots_mapsSlotIndexToOperator_andPicksAmmoQuantity()
        {
            var legacy = new List<InventorySlotEntry>
            {
                new InventorySlotEntry { slotIndex = 17, itemId = "ammo", slotQuantity = 1, ammoBoxQuantity = 9, gridCol = 2, gridRow = 1 },
                new InventorySlotEntry { slotIndex = 3,  itemId = "med",  slotQuantity = 2, gridCol = -1, gridRow = -1 },
            };

            var entries = InventorySerializer.FromLegacySlots(legacy);

            Assert.AreEqual(1, entries[0].containerIndex);
            Assert.AreEqual(9, entries[0].quantity);
            Assert.AreEqual(2, entries[0].col);
            Assert.AreEqual(0, entries[1].containerIndex);
            Assert.AreEqual(2, entries[1].quantity);
            Assert.AreEqual(-1, entries[1].col);
        }
    }
}

#nullable enable

using System;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class ItemContainerTests
    {
        private static ItemContainer NewContainer(int w = 4, int h = 4) =>
            new ItemContainer(ContainerId.Operator(0), w, h);

        private static InventoryItem Item(int w = 1, int h = 1) =>
            new ConsumableItem(InventoryTestData.Sized(InventoryTestData.Consumable(), w, h));

        [Test]
        public void NewContainer_isEmpty()
        {
            var c = NewContainer();
            Assert.AreEqual(0, c.Count);
            Assert.IsNull(c.GetItemAt(Vector2Int.zero));
        }

        [Test]
        public void Place_fillsEveryFootprintCell()
        {
            var c    = NewContainer();
            var item = Item(2, 1);
            c.Place(item, new Vector2Int(1, 0), 0);

            Assert.AreSame(item, c.GetItemAt(new Vector2Int(1, 0)));
            Assert.AreSame(item, c.GetItemAt(new Vector2Int(2, 0)));
            Assert.IsNull(c.GetItemAt(new Vector2Int(3, 0)));
            Assert.AreEqual(new Vector2Int(1, 0), c.GetPlacement(item)!.Origin);
        }

        [Test]
        public void Place_rotated_swapsFootprint()
        {
            var c    = NewContainer();
            var item = Item(2, 1);
            c.Place(item, Vector2Int.zero, 1);

            Assert.AreEqual(new Vector2Int(1, 2), c.GetPlacement(item)!.Footprint);
            Assert.AreSame(item, c.GetItemAt(new Vector2Int(0, 1)));
            Assert.IsNull(c.GetItemAt(new Vector2Int(1, 0)));
        }

        [Test]
        public void CanPlace_falseOutOfBounds()
        {
            Assert.IsFalse(NewContainer().CanPlace(new Vector2Int(2, 1), new Vector2Int(3, 0)));
            Assert.IsFalse(NewContainer().CanPlace(Vector2Int.one, new Vector2Int(-1, 0)));
        }

        [Test]
        public void CanPlace_falseWhenOccupied_trueWhenIgnoringOccupant()
        {
            var c    = NewContainer();
            var item = Item();
            c.Place(item, Vector2Int.zero, 0);

            Assert.IsFalse(c.CanPlace(Vector2Int.one, Vector2Int.zero));
            Assert.IsTrue(c.CanPlace(Vector2Int.one, Vector2Int.zero, ignore: item));
        }

        [Test]
        public void Place_overlapping_throws()
        {
            var c = NewContainer();
            c.Place(Item(), Vector2Int.zero, 0);
            Assert.Throws<InvalidOperationException>(() => c.Place(Item(), Vector2Int.zero, 0));
        }

        [Test]
        public void Place_sameItemTwice_throws()
        {
            var c    = NewContainer();
            var item = Item();
            c.Place(item, Vector2Int.zero, 0);
            Assert.Throws<InvalidOperationException>(() => c.Place(item, new Vector2Int(2, 2), 0));
        }

        [Test]
        public void Remove_clearsCellsAndPlacement()
        {
            var c    = NewContainer();
            var item = Item(2, 2);
            c.Place(item, Vector2Int.zero, 0);
            c.Remove(item);

            Assert.IsFalse(c.Contains(item));
            Assert.IsNull(c.GetItemAt(new Vector2Int(1, 1)));
            Assert.IsTrue(c.CanPlace(new Vector2Int(4, 4), Vector2Int.zero));
        }

        [Test]
        public void TryFindFreeCell_returnsFirstRowMajorFit()
        {
            var c = NewContainer();
            c.Place(Item(4, 1), Vector2Int.zero, 0);
            c.Place(Item(), new Vector2Int(0, 1), 0);

            Assert.IsTrue(c.TryFindFreeCell(new Vector2Int(2, 1), out var origin));
            Assert.AreEqual(new Vector2Int(1, 1), origin);
        }

        [Test]
        public void TryFindFreeCell_falseWhenNoFit()
        {
            var c = NewContainer();
            c.Place(Item(4, 4), Vector2Int.zero, 0);
            Assert.IsFalse(c.TryFindFreeCell(Vector2Int.one, out _));
        }

        [Test]
        public void GetOverlapping_returnsDistinctItems()
        {
            var c = NewContainer();
            var a = Item(2, 1);
            var b = Item();
            c.Place(a, Vector2Int.zero, 0);
            c.Place(b, new Vector2Int(0, 1), 0);

            var hits = c.GetOverlapping(new Vector2Int(2, 2), Vector2Int.zero);
            Assert.AreEqual(2, hits.Count);
            CollectionAssert.Contains(hits, a);
            CollectionAssert.Contains(hits, b);
        }

        [Test]
        public void NotifyChanged_raisesChanged()
        {
            var c     = NewContainer();
            int count = 0;
            c.Changed += () => count++;
            c.NotifyChanged();
            Assert.AreEqual(1, count);
        }
    }
}

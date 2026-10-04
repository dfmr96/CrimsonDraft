#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class GridNavigatorTests
    {
        private static readonly ContainerId Op0 = ContainerId.Operator(0);
        private static readonly ContainerId Op1 = ContainerId.Operator(1);

        private Dictionary<ContainerId, ItemContainer> containers = null!;
        private Dictionary<ContainerId, GridLinks>     links      = null!;

        [SetUp]
        public void SetUp()
        {
            this.containers = new Dictionary<ContainerId, ItemContainer>
            {
                [Op0] = new ItemContainer(Op0, 4, 4),
                [Op1] = new ItemContainer(Op1, 4, 4),
            };
            this.links = new Dictionary<ContainerId, GridLinks>
            {
                [Op0] = new GridLinks(Op1, Op1, null, null),
                [Op1] = new GridLinks(Op0, Op0, null, null),
            };
        }

        private GridNavigator Navigator(int col = 0, int row = 0)
        {
            var nav = new GridNavigator(id => this.containers[id], this.links, Op0);
            nav.Reset(Op0, new Vector2Int(col, row));
            return nav;
        }

        [Test]
        public void Move_rightWithinGrid()
        {
            var nav = Navigator();
            Assert.AreEqual(NavigationExit.None, nav.Move(Vector2Int.right, isHolding: false));
            Assert.AreEqual(new Vector2Int(1, 0), nav.Cell);
        }

        [Test]
        public void Move_downIncreasesRow()
        {
            var nav = Navigator();
            nav.Move(Vector2Int.down, isHolding: false);
            Assert.AreEqual(new Vector2Int(0, 1), nav.Cell);
        }

        [Test]
        public void Move_pastRightEdge_entersRightNeighborAtColumnZero()
        {
            var nav = Navigator(col: 3, row: 2);
            nav.Move(Vector2Int.right, isHolding: false);
            Assert.AreEqual(Op1, nav.Grid);
            Assert.AreEqual(new Vector2Int(0, 2), nav.Cell);
        }

        [Test]
        public void Move_pastLeftEdge_entersLeftNeighborAtLastColumn()
        {
            var nav = Navigator(col: 0, row: 1);
            nav.Move(Vector2Int.left, isHolding: false);
            Assert.AreEqual(Op1, nav.Grid);
            Assert.AreEqual(new Vector2Int(3, 1), nav.Cell);
        }

        [Test]
        public void Move_notHolding_skipsAcrossMultiCellItem()
        {
            this.containers[Op0].Place(InventoryItemFactory.Create(Sized(Consumable(), 2, 1)), Vector2Int.zero, 0);
            var nav = Navigator();
            nav.Move(Vector2Int.right, isHolding: false);
            Assert.AreEqual(new Vector2Int(2, 0), nav.Cell);
        }

        [Test]
        public void Move_holding_doesNotSkipItems()
        {
            this.containers[Op0].Place(InventoryItemFactory.Create(Sized(Consumable(), 2, 1)), Vector2Int.zero, 0);
            var nav = Navigator();
            nav.Move(Vector2Int.right, isHolding: true);
            Assert.AreEqual(new Vector2Int(1, 0), nav.Cell);
        }

        [Test]
        public void Move_upFromTopRow_notHolding_exitsUp()
        {
            var nav = Navigator(col: 2);
            Assert.AreEqual(NavigationExit.Up, nav.Move(Vector2Int.up, isHolding: false));
            Assert.AreEqual(new Vector2Int(2, 0), nav.Cell);
        }

        [Test]
        public void Move_upFromTopRow_holding_wrapsToBottom()
        {
            var nav = Navigator(col: 2);
            Assert.AreEqual(NavigationExit.None, nav.Move(Vector2Int.up, isHolding: true));
            Assert.AreEqual(new Vector2Int(2, 3), nav.Cell);
        }

        [Test]
        public void Move_downFromBottomRow_wrapsToTop()
        {
            var nav = Navigator(row: 3);
            nav.Move(Vector2Int.down, isHolding: false);
            Assert.AreEqual(new Vector2Int(0, 0), nav.Cell);
        }

        [Test]
        public void Move_intoShorterGrid_clampsRow()
        {
            this.containers[Op1] = new ItemContainer(Op1, 4, 2);
            var nav = Navigator(col: 3, row: 3);
            nav.Move(Vector2Int.right, isHolding: false);
            Assert.AreEqual(new Vector2Int(0, 1), nav.Cell);
        }

        [Test]
        public void Move_upWithUpLink_entersBottomRowOfThatGrid()
        {
            var storage = ContainerId.Storage;
            this.containers[storage] = new ItemContainer(storage, 8, 6);
            this.links[Op0] = new GridLinks(Op1, Op1, storage, null);

            var nav = Navigator(col: 2);
            Assert.AreEqual(NavigationExit.None, nav.Move(Vector2Int.up, isHolding: false));
            Assert.AreEqual(storage, nav.Grid);
            Assert.AreEqual(new Vector2Int(2, 5), nav.Cell);
        }

        [Test]
        public void Move_pastEdgeWithoutLink_clamps()
        {
            this.links[Op0] = new GridLinks(null, null, null, null);
            var nav = Navigator(col: 3);
            nav.Move(Vector2Int.right, isHolding: false);
            Assert.AreEqual(Op0, nav.Grid);
            Assert.AreEqual(new Vector2Int(3, 0), nav.Cell);
        }
    }
}

#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryBootstrapTests
    {
        private FakeRoster             roster    = null!;
        private InventoryService       inventory = null!;
        private InventoryStateRegistry registry  = null!;
        private ConsumableData         med       = null!;

        [SetUp]
        public void SetUp()
        {
            this.roster    = FakeRoster.WithOperators(4);
            this.inventory = new InventoryService(this.roster, FakeCombineService.None);
            this.registry  = new InventoryStateRegistry();
            this.med       = Consumable(id: "med");
        }

        private InventoryBootstrap Build() =>
            new InventoryBootstrap(
                ScriptableObject.CreateInstance<StartingLoadout>(),
                this.inventory, this.registry, this.roster, Database(this.med));

        [Test]
        public void Initialize_withSavedState_restoresInsteadOfLoadout()
        {
            this.registry.Save(new List<InventoryItemEntry>
            {
                new InventoryItemEntry { itemId = "med", containerIndex = 1, col = 2, row = 1 },
            });

            Build().Initialize();

            var placement = this.inventory.GetContainer(ContainerId.Operator(1)).Placements.Single();
            Assert.AreEqual(new Vector2Int(2, 1), placement.Origin);
        }

        [Test]
        public void Dispose_whileHolding_capturesHeldItemBackInPlace()
        {
            var bootstrap = Build();
            bootstrap.Initialize();
            this.inventory.TryAdd(this.med);
            this.inventory.TryPickUp(this.inventory.OperatorContainers[0].Placements.Single().Item);

            bootstrap.Dispose();

            Assert.IsNull(this.inventory.Held);
            var saved = this.registry.Load()!;
            Assert.AreEqual(1, saved.Count);
            Assert.AreEqual(0, saved[0].col);
            Assert.AreEqual(0, saved[0].row);
        }
    }
}

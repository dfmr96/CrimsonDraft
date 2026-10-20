#nullable enable

using NUnit.Framework;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryContentsChangedTests
    {
        [Test]
        public void TryAdd_raisesContentsChanged()
        {
            var inventory = new InventoryService(FakeRoster.WithOperators(1), FakeCombineService.None, FakeCorpseAccess.None);
            int raised = 0;
            inventory.ContentsChanged += () => raised++;

            Assert.IsTrue(inventory.TryAdd(Consumable(id: "herb")));

            Assert.GreaterOrEqual(raised, 1);
        }
    }
}

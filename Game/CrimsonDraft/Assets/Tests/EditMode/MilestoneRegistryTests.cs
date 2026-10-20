#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneRegistryTests
    {
        [Test]
        public void Complete_firstTimeTrue_secondTimeFalse_raisesOnce()
        {
            var registry = new MilestoneRegistry();
            int raised = 0;
            registry.Completed += _ => raised++;

            Assert.IsTrue(registry.Complete("m1"));
            Assert.IsFalse(registry.Complete("m1"));
            Assert.IsTrue(registry.IsCompleted("m1"));
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void Complete_emptyId_ignored()
        {
            var registry = new MilestoneRegistry();
            Assert.IsFalse(registry.Complete(""));
            Assert.AreEqual(0, registry.CompletedIds.Count);
        }

        [Test]
        public void LoadState_restoresWithoutRaising()
        {
            var registry = new MilestoneRegistry();
            int raised = 0;
            registry.Completed += _ => raised++;

            registry.LoadState(new[] { "m1", "m2" });

            Assert.IsTrue(registry.IsCompleted("m2"));
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void ClearAll_forgetsEverything()
        {
            var registry = new MilestoneRegistry();
            registry.Complete("m1");
            registry.ClearAll();
            Assert.IsFalse(registry.IsCompleted("m1"));
        }
    }
}

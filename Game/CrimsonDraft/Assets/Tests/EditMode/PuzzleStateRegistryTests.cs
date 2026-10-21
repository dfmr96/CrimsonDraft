#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class PuzzleStateRegistryTests
    {
        [Test]
        public void SetSolved_raisesOnce()
        {
            var registry = new PuzzleStateRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.SetSolved("music_box");
            registry.SetSolved("music_box");

            Assert.IsTrue(registry.IsSolved("music_box"));
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void SetSolved_emptyId_ignored()
        {
            var registry = new PuzzleStateRegistry();
            registry.SetSolved("");
            Assert.AreEqual(0, registry.SolvedIds.Count);
        }

        [Test]
        public void LoadStateAndClearAll()
        {
            var registry = new PuzzleStateRegistry();
            int raised = 0;
            registry.Changed += () => raised++;

            registry.LoadState(new[] { "generator" });
            Assert.IsTrue(registry.IsSolved("generator"));

            registry.ClearAll();
            Assert.IsFalse(registry.IsSolved("generator"));
            Assert.AreEqual(0, raised);
        }
    }
}

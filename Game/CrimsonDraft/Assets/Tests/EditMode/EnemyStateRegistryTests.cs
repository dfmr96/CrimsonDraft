#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyStateRegistryTests
    {
        [Test]
        public void LoadState_marksGivenKeysAsDefeated()
        {
            var registry = new EnemyStateRegistry();
            registry.LoadState(new List<string> { "enemy-a" });

            Assert.IsTrue(registry.IsDefeated("enemy-a"));
        }

        [Test]
        public void ClearAll_removesAllDefeated()
        {
            var registry = new EnemyStateRegistry();
            registry.SetDefeated("enemy-a");
            registry.ClearAll();

            Assert.IsFalse(registry.IsDefeated("enemy-a"));
        }
    }
}

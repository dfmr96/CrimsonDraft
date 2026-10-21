#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
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
            registry.SetDefeated("enemy-b", Vector3.one, Quaternion.identity);
            registry.ClearAll();

            Assert.IsFalse(registry.IsDefeated("enemy-a"));
            Assert.IsFalse(registry.IsDefeated("enemy-b"));
            Assert.IsFalse(registry.TryGetCorpsePose("enemy-b", out _));
        }

        [Test]
        public void SetDefeated_withPose_recordsCorpsePose()
        {
            var registry = new EnemyStateRegistry();
            var rotation = Quaternion.Euler(0f, 90f, 0f);
            registry.SetDefeated("enemy-a", new Vector3(1f, 0f, 2f), rotation);

            Assert.IsTrue(registry.IsDefeated("enemy-a"));
            Assert.IsTrue(registry.TryGetCorpsePose("enemy-a", out var pose));
            Assert.AreEqual(new Vector3(1f, 0f, 2f), pose.Position);
            Assert.AreEqual(rotation, pose.Rotation);
        }

        [Test]
        public void SetAlive_clearsDefeatedAndCorpsePose()
        {
            var registry = new EnemyStateRegistry();
            registry.SetDefeated("enemy-a", Vector3.one, Quaternion.identity);
            registry.SetAlive("enemy-a");

            Assert.IsFalse(registry.IsDefeated("enemy-a"));
            Assert.IsFalse(registry.TryGetCorpsePose("enemy-a", out _));
            CollectionAssert.IsEmpty(registry.GetCorpsePoses());
        }

        [Test]
        public void SetAlive_marksRevived_untilClearAll()
        {
            var registry = new EnemyStateRegistry();
            registry.SetAlive("enemy-a");
            Assert.IsTrue(registry.IsRevived("enemy-a"));

            registry.ClearAll();
            Assert.IsFalse(registry.IsRevived("enemy-a"));
        }

        [Test]
        public void SetDefeated_afterRevive_keepsRevived()
        {
            // A placed corpse that got up and was killed again: defeated wins on load, but it
            // must never fall back to the "placed corpse, no blood" state.
            var registry = new EnemyStateRegistry();
            registry.SetAlive("enemy-a");
            registry.SetDefeated("enemy-a", Vector3.zero, Quaternion.identity);

            Assert.IsTrue(registry.IsDefeated("enemy-a"));
            Assert.IsTrue(registry.IsRevived("enemy-a"));
        }

        [Test]
        public void LoadState_withRevived_restoresRevived()
        {
            var registry = new EnemyStateRegistry();
            registry.LoadState(new List<string>(), new EnemyStateRegistry.CorpsePose[0], new[] { "enemy-a" });

            Assert.IsTrue(registry.IsRevived("enemy-a"));
            Assert.IsFalse(registry.IsDefeated("enemy-a"));
        }

        [Test]
        public void SetDefeated_withoutPose_hasNoCorpsePose()
        {
            var registry = new EnemyStateRegistry();
            registry.SetDefeated("enemy-a");

            Assert.IsTrue(registry.IsDefeated("enemy-a"));
            Assert.IsFalse(registry.TryGetCorpsePose("enemy-a", out _));
        }

        [Test]
        public void LoadState_withPoses_marksPosedKeysAsDefeated()
        {
            var registry = new EnemyStateRegistry();
            registry.LoadState(
                new List<string>(),
                new[] { new EnemyStateRegistry.CorpsePose("enemy-a", Vector3.up, Quaternion.identity) });

            Assert.IsTrue(registry.IsDefeated("enemy-a"));
            Assert.IsTrue(registry.TryGetCorpsePose("enemy-a", out var pose));
            Assert.AreEqual(Vector3.up, pose.Position);
        }
    }
}

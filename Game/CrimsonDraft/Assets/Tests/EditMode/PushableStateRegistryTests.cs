#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class PushableStateRegistryTests
    {
        [Test]
        public void Record_thenTryGet_returnsPosition()
        {
            var registry = new PushableStateRegistry();
            registry.Record("crate-a", new Vector3(1f, 0f, 2f));

            Assert.IsTrue(registry.TryGet("crate-a", out var position));
            Assert.AreEqual(new Vector3(1f, 0f, 2f), position);
        }

        [Test]
        public void Record_calledTwice_keepsLatestPosition()
        {
            var registry = new PushableStateRegistry();
            registry.Record("crate-a", new Vector3(1f, 0f, 0f));
            registry.Record("crate-a", new Vector3(5f, 0f, 0f));

            registry.TryGet("crate-a", out var position);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), position);
        }

        [Test]
        public void Record_emptyId_isIgnored()
        {
            var registry = new PushableStateRegistry();
            registry.Record("", Vector3.one);

            Assert.AreEqual(0, registry.GetState().Count);
            Assert.IsFalse(registry.TryGet("", out _));
        }

        [Test]
        public void TryGet_unknownId_false()
        {
            Assert.IsFalse(new PushableStateRegistry().TryGet("missing", out _));
        }

        [Test]
        public void LoadState_replacesPreviousState()
        {
            var registry = new PushableStateRegistry();
            registry.Record("old", Vector3.one);

            registry.LoadState(new Dictionary<string, Vector3> { ["crate-b"] = new Vector3(3f, 0f, 4f) });

            Assert.IsFalse(registry.TryGet("old", out _));
            Assert.IsTrue(registry.TryGet("crate-b", out var position));
            Assert.AreEqual(new Vector3(3f, 0f, 4f), position);
        }

        [Test]
        public void ClearAll_forgetsEverything()
        {
            var registry = new PushableStateRegistry();
            registry.Record("crate-a", Vector3.one);

            registry.ClearAll();

            Assert.AreEqual(0, registry.GetState().Count);
        }
    }
}

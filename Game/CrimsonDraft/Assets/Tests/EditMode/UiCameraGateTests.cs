#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.UI;

namespace CrimsonDraft.Tests
{
    public sealed class UiCameraGateTests
    {
        [SetUp]
        public void SetUp() => UiCameraGate.ResetForTests();

        [TearDown]
        public void TearDown() => UiCameraGate.ResetForTests();

        [Test]
        public void NothingRequested_isOff()
            => Assert.IsFalse(UiCameraGate.IsRequested);

        [Test]
        public void StaysOnUntilTheLastOwnerReleases()
        {
            object inventory = new object(), dialogue = new object();

            UiCameraGate.Request(inventory);
            UiCameraGate.Request(dialogue);
            UiCameraGate.Release(inventory);
            Assert.IsTrue(UiCameraGate.IsRequested, "the dialogue still needs it");

            UiCameraGate.Release(dialogue);
            Assert.IsFalse(UiCameraGate.IsRequested);
        }

        [Test]
        public void RequestingTwice_isReleasedOnce()
        {
            var owner = new object();
            UiCameraGate.Request(owner);
            UiCameraGate.Request(owner);
            UiCameraGate.Release(owner);
            Assert.IsFalse(UiCameraGate.IsRequested);
        }

        [Test]
        public void ReleasingWithoutRequest_isHarmless()
        {
            UiCameraGate.Release(new object());
            Assert.IsFalse(UiCameraGate.IsRequested);
        }

        [Test]
        public void ChangedFires_onlyWhenTheStateFlips()
        {
            int changes = 0;
            UiCameraGate.Changed += () => changes++;
            object a = new object(), b = new object();

            UiCameraGate.Request(a);   // off -> on
            UiCameraGate.Request(b);   // still on
            UiCameraGate.Release(a);   // still on
            UiCameraGate.Release(b);   // on -> off

            Assert.AreEqual(2, changes);
        }

        [Test]
        public void ADestroyedOwner_doesNotKeepItOn()
        {
            var go = new GameObject("Panel");
            UiCameraGate.Request(go);
            Object.DestroyImmediate(go);

            Assert.IsFalse(UiCameraGate.IsRequested, "a panel destroyed without releasing must not pin the camera");
        }
    }
}

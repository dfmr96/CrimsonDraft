#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorCorpseTests
    {
        private GameObject corpseGo = null!;
        private GameObject playerGo = null!;
        private GameObject otherGo  = null!;
        private OperatorCorpse corpse = null!;
        private CorpseProximityTracker tracker = null!;

        [SetUp]
        public void SetUp()
        {
            this.corpseGo = new GameObject("Corpse", typeof(BoxCollider));
            this.corpse   = this.corpseGo.AddComponent<OperatorCorpse>();
            this.tracker  = new CorpseProximityTracker();
            this.corpse.Initialize(2, this.tracker);

            this.playerGo = new GameObject("Player", typeof(BoxCollider)) { tag = "Player" };
            this.otherGo  = new GameObject("Other", typeof(BoxCollider));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this.corpseGo);
            Object.DestroyImmediate(this.playerGo);
            Object.DestroyImmediate(this.otherGo);
        }

        private void Send(string message, GameObject who) =>
            typeof(OperatorCorpse)
                .GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this.corpse, new object[] { who.GetComponent<Collider>() });

        private void Disable() =>
            typeof(OperatorCorpse)
                .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this.corpse, null);

        [Test]
        public void Initialize_storesSlot() => Assert.AreEqual(2, this.corpse.Slot);

        [Test]
        public void PlayerEnter_grantsAccess()
        {
            Send("OnTriggerEnter", this.playerGo);
            Assert.IsTrue(this.tracker.CanAccess(2));
        }

        [Test]
        public void NonPlayerEnter_ignored()
        {
            Send("OnTriggerEnter", this.otherGo);
            Assert.IsFalse(this.tracker.CanAccess(2));
        }

        [Test]
        public void PlayerExit_revokesAccess()
        {
            Send("OnTriggerEnter", this.playerGo);
            Send("OnTriggerExit", this.playerGo);
            Assert.IsFalse(this.tracker.CanAccess(2));
        }

        [Test]
        public void TwoPlayerColliders_accessUntilLastLeaves()
        {
            var second = new GameObject("PlayerPart", typeof(BoxCollider)) { tag = "Player" };
            try
            {
                Send("OnTriggerEnter", this.playerGo);
                Send("OnTriggerEnter", second);
                Send("OnTriggerExit", this.playerGo);
                Assert.IsTrue(this.tracker.CanAccess(2));
                Send("OnTriggerExit", second);
                Assert.IsFalse(this.tracker.CanAccess(2));
            }
            finally { Object.DestroyImmediate(second); }
        }

        [Test]
        public void Disable_whileInside_revokesAccess()
        {
            Send("OnTriggerEnter", this.playerGo);
            Disable();
            Assert.IsFalse(this.tracker.CanAccess(2));
        }
    }
}

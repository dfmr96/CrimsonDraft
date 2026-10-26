#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Navigation.Gates;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    /// <summary>A beeper receiver is a gate: catching its code completes it, and the completion is
    /// saved (GateRegistry) and restored on load.</summary>
    public sealed class BeeperReceiverInteractableTests
    {
        private GameObject go = null!;
        private BeeperReceiverInteractable receiver = null!;
        private GateRegistry gates = null!;
        private int accepted;

        [SetUp]
        public void SetUp()
        {
            this.go = new GameObject("Receiver");
            this.go.AddComponent<BoxCollider>().isTrigger = true;
            this.receiver = this.go.AddComponent<BeeperReceiverInteractable>();
            SetString("expectedCode", "SOS");
            SetString("gateId", "beeper_safe");
            this.accepted = 0;
            this.receiver.OnCodeAccepted.AddListener(() => this.accepted++);
            this.gates = new GateRegistry();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.go);

        [Test]
        public void IsAGate()
        {
            Assert.IsInstanceOf<IGate>(this.receiver);
            Assert.AreEqual(GateIds.Generic("beeper_safe"), this.receiver.GateId);
        }

        [Test]
        public void CatchingTheCode_completesTheGate()
        {
            this.receiver.BindGates(this.gates);
            SetInRange(true);
            Send("sos"); // codes match case-insensitively
            Assert.IsTrue(this.gates.IsCompleted("beeper_safe"));
            Assert.AreEqual(1, this.accepted);
        }

        [Test]
        public void WrongCodeOrOutOfRange_leavesItOpen()
        {
            this.receiver.BindGates(this.gates);
            SetInRange(true);
            Send("HELP");
            SetInRange(false);
            Send("SOS");
            Assert.IsFalse(this.gates.IsCompleted("beeper_safe"));
            Assert.AreEqual(0, this.accepted);
        }

        [Test]
        public void CompletedInASave_replaysItsEffectsOnceAndStaysLatched()
        {
            // Loaded save: the safe was opened before. Binding brings the world back (onCodeAccepted
            // replays: the safe slides open) and the receiver no longer reacts to the code.
            this.gates.SetCompleted("beeper_safe");
            this.receiver.BindGates(this.gates);
            Assert.AreEqual(1, this.accepted);

            SetInRange(true);
            Send("SOS");
            Assert.AreEqual(1, this.accepted);
        }

        [Test]
        public void ReplayOnLoad_runsAsARestore()
        {
            // Effects see WorldRestore while replaying, so they snap to their end state (the safe is
            // simply open) instead of animating; outside the replay it is off again.
            bool sawRestore = false;
            this.receiver.OnCodeAccepted.AddListener(() => sawRestore = WorldRestore.IsActive);
            this.gates.SetCompleted("beeper_safe");

            this.receiver.BindGates(this.gates);

            Assert.IsTrue(sawRestore);
            Assert.IsFalse(WorldRestore.IsActive);
        }

        [Test]
        public void CatchingTheCodeLive_isNotARestore()
        {
            bool sawRestore = true;
            this.receiver.OnCodeAccepted.AddListener(() => sawRestore = WorldRestore.IsActive);
            this.receiver.BindGates(this.gates);
            SetInRange(true);
            Send("SOS");
            Assert.IsFalse(sawRestore);
        }

        [Test]
        public void NotCompleted_bindingReplaysNothing()
        {
            this.receiver.BindGates(this.gates);
            Assert.AreEqual(0, this.accepted);
        }

        [Test]
        public void RepeatableReceiver_isNotReplayedOnLoad()
        {
            // oneShot = false toggles something on every SEND: replaying it would flip it, not restore it.
            var so = new SerializedObject(this.receiver);
            so.FindProperty("oneShot").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            this.gates.SetCompleted("beeper_safe");

            this.receiver.BindGates(this.gates);

            Assert.AreEqual(0, this.accepted);
        }

        [Test]
        public void OldPuzzleIdField_isReadAsTheGateId()
        {
            // The field was briefly "puzzleId"; FormerlySerializedAs keeps prefabs saved with it.
            var field = typeof(BeeperReceiverInteractable).GetField("gateId", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var former = field.GetCustomAttribute<UnityEngine.Serialization.FormerlySerializedAsAttribute>();
            Assert.IsNotNull(former);
            Assert.AreEqual("puzzleId", former!.oldName);
        }

        private void SetString(string field, string value)
        {
            var so = new SerializedObject(this.receiver);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetInRange(bool value) => typeof(BeeperReceiverInteractable)
            .GetField("playerInRange", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this.receiver, value);

        private void Send(string code) => typeof(BeeperReceiverInteractable)
            .GetMethod("OnSignalSent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(this.receiver, new object[] { new BeeperSignalSentEvent { Code = code } });
    }
}

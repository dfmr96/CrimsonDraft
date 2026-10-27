#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Gates;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    /// <summary>A gate on one slot of an ItemSocketInteractable (e.g. "the yellow gem is in"), for sockets
    /// that hold several items and whose whole-socket gate waits for all of them.</summary>
    public sealed class SocketSlotGateTests
    {
        private GameObject go = null!;
        private ItemSocketInteractable socket = null!;
        private SocketSlotGate gate = null!;

        [SetUp]
        public void SetUp()
        {
            this.go = new GameObject("JewelryBox");
            this.socket = this.go.AddComponent<ItemSocketInteractable>();
            new SerializedObject(this.socket).Apply("socketId", "office_jewelry_box_gems");

            var child = new GameObject("Gate_YellowGem");
            child.transform.SetParent(this.go.transform);
            this.gate = child.AddComponent<SocketSlotGate>();
            var so = new SerializedObject(this.gate);
            so.FindProperty("socket").objectReferenceValue = this.socket;
            so.FindProperty("slotIndex").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.go);

        [Test]
        public void IsAGate_withASlotId()
        {
            Assert.IsInstanceOf<IGate>(this.gate);
            Assert.AreEqual(GateIds.SocketSlot("office_jewelry_box_gems", 1), this.gate.GateId);
        }

        [Test]
        public void WithoutASocket_hasNoValidGateId()
        {
            var so = new SerializedObject(this.gate);
            so.FindProperty("socket").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(GateIds.TryParse(this.gate.GateId, out _, out _));
        }

        [Test]
        public void Reset_findsTheSocketOnTheParent()
        {
            var fresh = new GameObject("Fresh");
            fresh.transform.SetParent(this.go.transform);
            var added = fresh.AddComponent<SocketSlotGate>();

            // AddComponent in the editor runs Reset; mirror that for a component created in a test.
            typeof(SocketSlotGate).GetMethod("Reset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(added, null);

            Assert.AreSame(this.socket, new SerializedObject(added).FindProperty("socket").objectReferenceValue);
        }
    }

    internal static class SerializedObjectExtensions
    {
        public static void Apply(this SerializedObject so, string field, string value)
        {
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

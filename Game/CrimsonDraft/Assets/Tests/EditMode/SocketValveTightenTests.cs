#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class SocketValveTightenTests
    {
        private GameObject?  go;
        private FakeDialogue general = null!;
        private FakeDialogue pickup  = null!;

        [SetUp]
        public void SetUp()
        {
            this.general = new FakeDialogue();
            this.pickup  = new FakeDialogue();
        }

        [TearDown]
        public void TearDown()
        {
            if (this.go != null) UnityEngine.Object.DestroyImmediate(this.go);
        }

        private static SocketItemData Valve()
        {
            var d  = ScriptableObject.CreateInstance<SocketItemData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue      = "KI_11";
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.SocketItem;
            so.FindProperty("displayName").stringValue = "Blue Valve";
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private ItemSocketInteractable BuildValveSocket(SocketItemData valve)
        {
            this.go = new GameObject();
            var socket = this.go.AddComponent<ItemSocketInteractable>();
            var so     = new UnityEditor.SerializedObject(socket);
            so.FindProperty("socketId").stringValue = "pipe";
            var arr = so.FindProperty("requiredItems");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = valve;
            so.ApplyModifiedPropertiesWithoutUndo();

            var tighten = this.go.AddComponent<SocketValveTighten>();
            tighten.Construct(this.general, this.pickup);
            socket.Construct(new ItemSocketStateRegistry());
            return socket;
        }

        [Test]
        public void InsertingTheValve_opensTheTurnPrompt_onceTheInsertLineCloses()
        {
            var valve  = Valve();
            var socket = BuildValveSocket(valve);

            Assert.IsTrue(socket.TryInsert(valve, this.general));
            Assert.AreEqual(0, this.pickup.Started.Count, "prompt waits for the insert line");

            this.general.LastOnComplete!.Invoke();

            CollectionAssert.AreEqual(new[] { "socket_pipe_valve_turn" }, this.pickup.Started);
        }

        private sealed class FakeDialogue : IPickupDialogueService
        {
            public readonly List<string> Started = new();
            public Action?               LastOnComplete { get; private set; }
            public bool                  IsRunning      => false;

            public void StartDialogue(
                string                               nodeName,
                IReadOnlyDictionary<string, object>? variables  = null,
                Action?                              onComplete = null,
                IReadOnlyDictionary<string, Action>? commands   = null)
            {
                this.Started.Add(nodeName);
                this.LastOnComplete = onComplete;
            }

            public void SetVariable(string name, object value) { }
        }
    }
}

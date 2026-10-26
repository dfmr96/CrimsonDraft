#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneTriggerEvaluatorTests
    {
        internal sealed class FakeWorld : IMilestoneWorldState
        {
            public readonly HashSet<string> Pickups = new(), Items = new(), Doors = new(), Notes = new(), Gates = new();
            public event Action<MilestoneSource>? SourceChanged;
            public void Raise(MilestoneSource source) => SourceChanged?.Invoke(source);
            public bool IsPickupCollected(string id) => this.Pickups.Contains(id);
            public bool HasItem(string itemId)       => this.Items.Contains(itemId);
            public bool IsDoorUnlocked(string id)    => this.Doors.Contains(id);
            public bool IsNoteRead(string id)        => this.Notes.Contains(id);
            public bool IsGateCompleted(string id)   => this.Gates.Contains(id);
            public void Dispose() { }
        }

        private static MilestoneTrigger T(MilestoneTriggerKind kind, string id = "") => new() { kind = kind, targetId = id };

        [Test]
        public void TextIdKinds_followTheirSource()
        {
            var world = new FakeWorld();
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PickupCollected, "p"), world));
            world.Pickups.Add("p"); world.Doors.Add("d"); world.Gates.Add("radio:r1");
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PickupCollected, "p"), world));
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.DoorUnlocked, "d"), world));
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.GateCompleted, "radio:r1"), world));
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.GateCompleted, "puzzle:other"), world));
        }

        [Test]
        public void ItemHeldAndNoteRead_useTheirAssets()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            var note = ScriptableObject.CreateInstance<DocumentData>();
            try
            {
                var itemSo = new SerializedObject(item); itemSo.FindProperty("itemId").stringValue = "sheep_key"; itemSo.ApplyModifiedPropertiesWithoutUndo();
                var noteSo = new SerializedObject(note); noteSo.FindProperty("noteId").stringValue = "beeper_file"; noteSo.ApplyModifiedPropertiesWithoutUndo();
                var world = new FakeWorld();
                var held = new MilestoneTrigger { kind = MilestoneTriggerKind.ItemHeld, item = item };
                var read = new MilestoneTrigger { kind = MilestoneTriggerKind.NoteRead, note = note };

                Assert.IsFalse(MilestoneTriggerEvaluator.Holds(held, world));
                world.Items.Add("sheep_key"); world.Notes.Add("beeper_file");
                Assert.IsTrue(MilestoneTriggerEvaluator.Holds(held, world));
                Assert.IsTrue(MilestoneTriggerEvaluator.Holds(read, world));
            }
            finally { UnityEngine.Object.DestroyImmediate(item); UnityEngine.Object.DestroyImmediate(note); }
        }

        // Review Focus 4: empty / unassigned targets never hold and never throw.
        [Test]
        public void MissingTargets_neverHold()
        {
            var world = new FakeWorld();
            world.Pickups.Add("");
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.None), world));
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PickupCollected, ""), world));
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(new MilestoneTrigger { kind = MilestoneTriggerKind.ItemHeld }, world));
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(new MilestoneTrigger { kind = MilestoneTriggerKind.NoteRead }, world));
        }

        [Test]
        public void SourceOf_mapsEveryKind()
        {
            Assert.AreEqual(MilestoneSource.Pickups,   MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.PickupCollected));
            Assert.AreEqual(MilestoneSource.Inventory, MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.ItemHeld));
            Assert.AreEqual(MilestoneSource.Doors,     MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.DoorUnlocked));
            Assert.AreEqual(MilestoneSource.Notes,     MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.NoteRead));
            Assert.AreEqual(MilestoneSource.Gates,     MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.GateCompleted));
            Assert.AreEqual(MilestoneSource.None,      MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.None));
        }

        // Graphs saved before gates had SocketCompleted (3) / PuzzleSolved (6): they load as GateCompleted.
        [Test]
        public void LegacySocketAndPuzzleTriggers_upgradeToGates()
        {
            var socket = new MilestoneTrigger { kind = (MilestoneTriggerKind)3, targetId = "fuse" };
            var puzzle = new MilestoneTrigger { kind = (MilestoneTriggerKind)6, targetId = "valves" };
            var door   = new MilestoneTrigger { kind = MilestoneTriggerKind.DoorUnlocked, targetId = "d" };

            Assert.IsTrue(socket.UpgradeLegacy());
            Assert.IsTrue(puzzle.UpgradeLegacy());
            Assert.IsFalse(door.UpgradeLegacy());

            Assert.AreEqual((MilestoneTriggerKind.GateCompleted, "socket:fuse"), (socket.kind, socket.targetId));
            Assert.AreEqual((MilestoneTriggerKind.GateCompleted, "puzzle:valves"), (puzzle.kind, puzzle.targetId));
            Assert.AreEqual((MilestoneTriggerKind.DoorUnlocked, "d"), (door.kind, door.targetId));
        }

        [Test]
        public void LegacyTriggerWithoutId_upgradesToAnEmptyGate()
        {
            var socket = new MilestoneTrigger { kind = (MilestoneTriggerKind)3, targetId = "" };
            Assert.IsTrue(socket.UpgradeLegacy());
            Assert.AreEqual((MilestoneTriggerKind.GateCompleted, ""), (socket.kind, socket.targetId));
        }

        [Test]
        public void LoadingAGraph_upgradesItsLegacyTriggers()
        {
            var graph = ScriptableObject.CreateInstance<MilestoneGraph>();
            try
            {
                var node = graph.AddNode("Fuse in", Vector2.zero);
                node.trigger = new MilestoneTrigger { kind = (MilestoneTriggerKind)3, targetId = "fuse" };
                // Round-trip through serialization, as when the asset is loaded from disk.
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(graph), graph);

                Assert.AreEqual(MilestoneTriggerKind.GateCompleted, graph.Nodes[0].trigger.kind);
                Assert.AreEqual("socket:fuse", graph.Nodes[0].trigger.targetId);
            }
            finally { UnityEngine.Object.DestroyImmediate(graph); }
        }
    }
}
